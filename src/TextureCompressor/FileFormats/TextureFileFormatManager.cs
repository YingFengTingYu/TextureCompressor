using System.Diagnostics.CodeAnalysis;
using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;
using TextureCompressor.Formats;

namespace TextureCompressor.FileFormats;

public sealed class TextureFileFormatManager
{
    private const int ProbeByteCount = 64;

    private static readonly Lazy<TextureFileFormatManager> SGlobal = new(() => new TextureFileFormatManager());

    private readonly List<IFileFormat> _formats = [];
    private readonly Lock _sync = new();

    public static TextureFileFormatManager Global => SGlobal.Value;

    public IReadOnlyList<IFileFormat> Formats
    {
        get
        {
            lock (_sync)
            {
                return [.. _formats];
            }
        }
    }

    public IDisposable Register(IFileFormat format)
    {
        ArgumentNullException.ThrowIfNull(format);

        lock (_sync)
        {
            _formats.Add(format);
        }

        return new Registration(this, format);
    }

    public IDisposable Register(IEnumerable<IFileFormat> formats)
    {
        ArgumentNullException.ThrowIfNull(formats);

        var registrations = new List<IDisposable>();
        try
        {
            foreach (var format in formats)
            {
                registrations.Add(Register(format));
            }
        }
        catch
        {
            foreach (var registration in registrations)
            {
                registration.Dispose();
            }

            throw;
        }

        return new CompositeRegistration(registrations);
    }

    public ArrayBitmap<TPixel> ReadImage<TPixel>(string path, IFileFormatOptions? options = null)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = File.OpenRead(path);
        return ReadImage<TPixel>(stream, path, options);
    }

    public ArrayBitmap<TPixel> ReadImage<TPixel>(Stream stream, IFileFormatOptions? options = null)
        where TPixel : unmanaged, IPixel<TPixel> =>
        ReadImage<TPixel>(stream, pathOrExtension: null, options);

    public ArrayBitmap<TPixel> ReadImage<TPixel>(Stream stream, string? pathOrExtension, IFileFormatOptions? options = null)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(stream);

        var extension = NormalizeExtension(pathOrExtension);
        var header = ReadProbeHeader(stream);
        var format = GetReadableFormat<IImageFileFormat>(header, extension, "image");
        return format.ReadImage<TPixel>(stream, options);
    }

    public void WriteImage<TPixel>(IBitmap<TPixel> image, string path, IFileFormatOptions? options = null)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = File.Create(path);
        WriteImage(image, stream, path, options);
    }

    public void WriteImage<TPixel>(IBitmap<TPixel> image, Stream stream, string pathOrExtension, IFileFormatOptions? options = null)
        where TPixel : unmanaged, IPixel<TPixel>
    {
        ArgumentNullException.ThrowIfNull(image);
        ArgumentNullException.ThrowIfNull(stream);

        var format = GetWritableFormat<IImageFileFormat>(pathOrExtension, "image");
        format.WriteImage(image, stream, options);
    }

    public ITextureFile ReadTexture(string path, IFileFormatOptions? options = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = File.OpenRead(path);
        return ReadTexture(stream, path, options);
    }

    public ITextureFile ReadTexture(Stream stream, IFileFormatOptions? options = null) =>
        ReadTexture(stream, pathOrExtension: null, options);

    public ITextureFile ReadTexture(Stream stream, string? pathOrExtension, IFileFormatOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(stream);

        var extension = NormalizeExtension(pathOrExtension);
        var header = ReadProbeHeader(stream);
        var format = GetReadableFormat<ITextureFileFormat>(header, extension, "texture");
        return format.ReadTexture(stream, options);
    }

    public void WriteTexture(ITextureFile textureFile, string path, IFileFormatOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(textureFile);

        WriteTexture(textureFile.Texture, path, options);
    }

    public void WriteTexture(TextureImage texture, string path, IFileFormatOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var stream = File.Create(path);
        WriteTexture(texture, stream, path, options);
    }

    public void WriteTexture(ITextureFile textureFile, Stream stream, string pathOrExtension, IFileFormatOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(textureFile);

        WriteTexture(textureFile.Texture, stream, pathOrExtension, options);
    }

    public void WriteTexture(TextureImage texture, Stream stream, string pathOrExtension, IFileFormatOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(texture);
        ArgumentNullException.ThrowIfNull(stream);

        var format = GetWritableFormat<ITextureFileFormat>(pathOrExtension, "texture");
        format.WriteTexture(texture, stream, options);
    }

    public bool TryGetImageFormat(string pathOrExtension, [NotNullWhen(true)] out IImageFileFormat? format) =>
        TryGetWritableFormat(pathOrExtension, out format);

    public IImageFileFormat GetImageFormat(string pathOrExtension) =>
        GetWritableFormat<IImageFileFormat>(pathOrExtension, "image");

    public bool TryGetTextureFormat(string pathOrExtension, [NotNullWhen(true)] out ITextureFileFormat? format) =>
        TryGetWritableFormat(pathOrExtension, out format);

    public ITextureFileFormat GetTextureFormat(string pathOrExtension) =>
        GetWritableFormat<ITextureFileFormat>(pathOrExtension, "texture");

    private TFormat GetReadableFormat<TFormat>(ReadOnlySpan<byte> header, string? extension, string kind)
        where TFormat : class, IFileFormat =>
        TryGetReadableFormat(header, extension, out TFormat? format)
            ? format
            : throw new NotSupportedException(CreateUnsupportedReadMessage(kind, extension));

    private bool TryGetReadableFormat<TFormat>(
        ReadOnlySpan<byte> header,
        string? extension,
        [NotNullWhen(true)] out TFormat? format)
        where TFormat : class, IFileFormat
    {
        var formats = GetFormatsSnapshot();
        if (!header.IsEmpty)
        {
            for (var i = formats.Length - 1; i >= 0; i--)
            {
                if (formats[i] is TFormat candidate && candidate.CanRead(header, extension: null))
                {
                    format = candidate;
                    return true;
                }
            }
        }

        if (extension is not null)
        {
            for (var i = formats.Length - 1; i >= 0; i--)
            {
                if (formats[i] is TFormat candidate && ExtensionMatches(candidate, extension))
                {
                    format = candidate;
                    return true;
                }
            }
        }

        if (!header.IsEmpty && extension is not null)
        {
            for (var i = formats.Length - 1; i >= 0; i--)
            {
                if (formats[i] is TFormat candidate && candidate.CanRead(header, extension))
                {
                    format = candidate;
                    return true;
                }
            }
        }

        format = null;
        return false;
    }

    private TFormat GetWritableFormat<TFormat>(string pathOrExtension, string kind)
        where TFormat : class, IFileFormat =>
        TryGetWritableFormat(pathOrExtension, out TFormat? format)
            ? format
            : throw new NotSupportedException($"No {kind} file format is registered for extension '{pathOrExtension}'.");

    private bool TryGetWritableFormat<TFormat>(string pathOrExtension, [NotNullWhen(true)] out TFormat? format)
        where TFormat : class, IFileFormat
    {
        var extension = NormalizeExtension(pathOrExtension);
        if (extension is null)
        {
            format = null;
            return false;
        }

        var formats = GetFormatsSnapshot();
        for (var i = formats.Length - 1; i >= 0; i--)
        {
            if (formats[i] is TFormat candidate && ExtensionMatches(candidate, extension))
            {
                format = candidate;
                return true;
            }
        }

        format = null;
        return false;
    }

    private IFileFormat[] GetFormatsSnapshot()
    {
        lock (_sync)
        {
            return _formats.ToArray();
        }
    }

    private void Unregister(IFileFormat format)
    {
        lock (_sync)
        {
            _formats.Remove(format);
        }
    }

    private static byte[] ReadProbeHeader(Stream stream)
    {
        if (!stream.CanRead)
        {
            throw new ArgumentException("Stream must be readable.", nameof(stream));
        }

        if (!stream.CanSeek)
        {
            return [];
        }

        var position = stream.Position;
        var buffer = new byte[ProbeByteCount];
        var totalRead = 0;
        try
        {
            while (totalRead < buffer.Length)
            {
                var read = stream.Read(buffer.AsSpan(totalRead));
                if (read == 0)
                {
                    break;
                }

                totalRead += read;
            }
        }
        finally
        {
            stream.Position = position;
        }

        if (totalRead == buffer.Length)
        {
            return buffer;
        }

        Array.Resize(ref buffer, totalRead);
        return buffer;
    }

    private static bool ExtensionMatches(IFileFormat format, string extension)
    {
        foreach (var candidate in format.Extensions)
        {
            if (string.Equals(NormalizeExtension(candidate), extension, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string? NormalizeExtension(string? pathOrExtension)
    {
        if (string.IsNullOrWhiteSpace(pathOrExtension))
        {
            return null;
        }

        var value = pathOrExtension.Trim();
        var extension = Path.GetExtension(value);
        if (string.IsNullOrEmpty(extension))
        {
            if (value.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar, Path.VolumeSeparatorChar]) >= 0)
            {
                return null;
            }

            extension = value.StartsWith('.') ? value : "." + value;
        }

        return extension.ToLowerInvariant();
    }

    private static string CreateUnsupportedReadMessage(string kind, string? extension) =>
        extension is null
            ? $"No registered {kind} file format could read the stream."
            : $"No registered {kind} file format could read the stream or extension '{extension}'.";

    private sealed class Registration(TextureFileFormatManager manager, IFileFormat format) : IDisposable
    {
        private TextureFileFormatManager? _manager = manager;

        public void Dispose()
        {
            var manager = Interlocked.Exchange(ref _manager, null);
            manager?.Unregister(format);
        }
    }

    private sealed class CompositeRegistration(IReadOnlyList<IDisposable> registrations) : IDisposable
    {
        private IReadOnlyList<IDisposable>? _registrations = registrations ?? throw new ArgumentNullException(nameof(registrations));

        public void Dispose()
        {
            var registrations = Interlocked.Exchange(ref _registrations, null);
            if (registrations is null)
            {
                return;
            }

            for (var i = registrations.Count - 1; i >= 0; i--)
            {
                registrations[i].Dispose();
            }
        }
    }
}
