using System.Diagnostics.CodeAnalysis;
using TextureCompressor.Formats;
using TextureCompressor.Codecs;
using TextureCompressor.Options;

namespace TextureCompressor.Registry;

public sealed class TextureCoderManager
{
    private static readonly Lazy<TextureCoderManager> SGlobal = new(() => new TextureCoderManager());

    private readonly Dictionary<TextureFormat, ITextureCoder> _builtInCoders = [];
    private readonly Dictionary<TextureFormat, ITextureCoder3D> _builtInCoders3D = [];
    private readonly List<CoderEntry> _coders = [];
    private readonly List<Coder3DEntry> _coders3D = [];
    private readonly Lock _sync = new();

    public static TextureCoderManager Global => SGlobal.Value;

    public IDisposable Register(TextureFormat format, ITextureCoder coder)
    {
        ArgumentNullException.ThrowIfNull(coder);

        var entry = new CoderEntry(format, coder);
        lock (_sync)
        {
            _coders.Add(entry);
        }

        return new Registration(this, entry);
    }

    public IDisposable Register(IEnumerable<TextureFormat> formats, Func<TextureFormat, ITextureCoder> coderFactory)
    {
        ArgumentNullException.ThrowIfNull(formats);
        ArgumentNullException.ThrowIfNull(coderFactory);

        var registrations = new List<IDisposable>();
        try
        {
            foreach (var format in formats)
            {
                registrations.Add(Register(format, coderFactory(format)));
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

        return Combine(registrations);
    }

    public IDisposable Register3D(TextureFormat format, ITextureCoder3D coder)
    {
        ArgumentNullException.ThrowIfNull(coder);

        var entry = new Coder3DEntry(format, coder);
        lock (_sync)
        {
            _coders3D.Add(entry);
        }

        return new Registration3D(this, entry);
    }

    public IDisposable Register3D(IEnumerable<TextureFormat> formats, Func<TextureFormat, ITextureCoder3D> coderFactory)
    {
        ArgumentNullException.ThrowIfNull(formats);
        ArgumentNullException.ThrowIfNull(coderFactory);

        var registrations = new List<IDisposable>();
        try
        {
            foreach (var format in formats)
            {
                registrations.Add(Register3D(format, coderFactory(format)));
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

        return Combine(registrations);
    }

    public static IDisposable Combine(params ReadOnlySpan<IDisposable> registrations) =>
        new CompositeRegistration(registrations);

    public static IDisposable Combine(IEnumerable<IDisposable> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        return new CompositeRegistration(registrations);
    }

    public bool TryGetCoder(TextureFormat format, [NotNullWhen(true)] out ITextureCoder? coder)
    {
        lock (_sync)
        {
            if (TryGetRegisteredCoder(format, out coder)
                || _builtInCoders.TryGetValue(format, out coder))
            {
                return true;
            }
        }

        if (!TryCreateBuiltInCoder(format, out var builtInCoder))
        {
            coder = null;
            return false;
        }

        lock (_sync)
        {
            if (TryGetRegisteredCoder(format, out coder))
            {
                return true;
            }

            if (!_builtInCoders.TryGetValue(format, out coder))
            {
                _builtInCoders.Add(format, builtInCoder);
                coder = builtInCoder;
            }

            return true;
        }
    }

    public ITextureCoder GetCoder(TextureFormat format) =>
        TryGetCoder(format, out var coder)
            ? coder
            : throw new NotSupportedException($"No texture coder is registered for texture format '{format.Name}'.");

    public bool TryGetCoder3D(TextureFormat format, [NotNullWhen(true)] out ITextureCoder3D? coder)
    {
        lock (_sync)
        {
            if (TryGetRegisteredCoder3D(format, out coder))
            {
                return true;
            }

            if (TryGetRegisteredCoder(format, out var registeredTextureCoder))
            {
                if (registeredTextureCoder is ITextureCoder3D registeredTextureCoder3D)
                {
                    coder = registeredTextureCoder3D;
                    return true;
                }

                coder = registeredTextureCoder is IPitchTextureCoder registeredPitchTextureCoder
                    ? new PitchTextureArrayCoder(registeredPitchTextureCoder)
                    : new TextureArrayCoder(registeredTextureCoder);
                return true;
            }

            if (_builtInCoders3D.TryGetValue(format, out coder))
            {
                return true;
            }
        }

        if (TryCreateBuiltInCoder3D(format, out var builtInCoder3D))
        {
            lock (_sync)
            {
                if (TryGetRegisteredCoder3D(format, out coder))
                {
                    return true;
                }

                if (!_builtInCoders3D.TryGetValue(format, out coder))
                {
                    _builtInCoders3D.Add(format, builtInCoder3D);
                    coder = builtInCoder3D;
                }

                return true;
            }
        }

        if (!TryGetCoder(format, out var textureCoder))
        {
            coder = null;
            return false;
        }

        if (textureCoder is ITextureCoder3D textureCoder3D)
        {
            coder = textureCoder3D;
            return true;
        }

        coder = textureCoder is IPitchTextureCoder pitchTextureCoder
            ? new PitchTextureArrayCoder(pitchTextureCoder)
            : new TextureArrayCoder(textureCoder);
        return true;
    }

    public ITextureCoder3D GetCoder3D(TextureFormat format) =>
        TryGetCoder3D(format, out var coder)
            ? coder
            : throw new NotSupportedException($"No 3D texture coder is registered for texture format '{format.Name}'.");

    private void Unregister(CoderEntry entry)
    {
        lock (_sync)
        {
            _coders.Remove(entry);
        }
    }

    private void Unregister(Coder3DEntry entry)
    {
        lock (_sync)
        {
            _coders3D.Remove(entry);
        }
    }

    private bool TryGetRegisteredCoder(TextureFormat format, [NotNullWhen(true)] out ITextureCoder? coder)
    {
        for (var i = _coders.Count - 1; i >= 0; i--)
        {
            var entry = _coders[i];
            if (entry.Format == format)
            {
                coder = entry.Coder;
                return true;
            }
        }

        coder = null;
        return false;
    }

    private bool TryGetRegisteredCoder3D(TextureFormat format, [NotNullWhen(true)] out ITextureCoder3D? coder)
    {
        for (var i = _coders3D.Count - 1; i >= 0; i--)
        {
            var entry = _coders3D[i];
            if (entry.Format == format)
            {
                coder = entry.Coder;
                return true;
            }
        }

        coder = null;
        return false;
    }

    private static bool TryCreateBuiltInCoder(TextureFormat format, [NotNullWhen(true)] out ITextureCoder? coder)
    {
        if (BitPackedUNormTextureCoder.IsSupported(format))
        {
            coder = new BitPackedUNormTextureCoder(format);
            return true;
        }

        if (PackedUNormTextureCoder.IsSupported(format))
        {
            coder = new PackedUNormTextureCoder(format);
            return true;
        }

        if (PackedFloatTextureCoder.IsSupported(format))
        {
            coder = new PackedFloatTextureCoder(format);
            return true;
        }

        if (PackedSNormTextureCoder.IsSupported(format))
        {
            coder = new PackedSNormTextureCoder(format);
            return true;
        }

        if (PackedIntegerTextureCoder.IsSupported(format))
        {
            coder = new PackedIntegerTextureCoder(format);
            return true;
        }

        if (XrTextureCoder.IsSupported(format))
        {
            coder = new XrTextureCoder(format);
            return true;
        }

        if (DepthStencilTextureCoder.IsSupported(format))
        {
            coder = new DepthStencilTextureCoder(format);
            return true;
        }

        if (PackedRgb422TextureCoder.IsSupported(format))
        {
            coder = new PackedRgb422TextureCoder(format);
            return true;
        }

        if (PackedYuv422TextureCoder.IsSupported(format))
        {
            coder = new PackedYuv422TextureCoder(format);
            return true;
        }

        if (PackedYuva444TextureCoder.IsSupported(format))
        {
            coder = new PackedYuva444TextureCoder(format);
            return true;
        }

        if (Nv11TextureCoder.IsSupported(format))
        {
            coder = new Nv11TextureCoder(format);
            return true;
        }

        if (PlanarYuvTextureCoder.IsSupported(format))
        {
            coder = new PlanarYuvTextureCoder(format);
            return true;
        }

        if (RgbmTextureCoder.IsSupported(format))
        {
            coder = new RgbmTextureCoder(format);
            return true;
        }

        if (IndexedTextureCoder.IsSupported(format))
        {
            coder = new IndexedTextureCoder(format);
            return true;
        }

        if (PalettedTextureCoder.IsSupported(format))
        {
            coder = new PalettedTextureCoder(format);
            return true;
        }

        if (S3tcTextureCoder.IsSupported(format))
        {
            coder = new S3tcTextureCoder(format);
            return true;
        }

        if (FxtcTextureCoder.IsSupported(format))
        {
            coder = new FxtcTextureCoder(format);
            return true;
        }

        if (AtcTextureCoder.IsSupported(format))
        {
            coder = new AtcTextureCoder(format);
            return true;
        }

        if (RgtcLatcTextureCoder.IsSupported(format))
        {
            coder = new RgtcLatcTextureCoder(format);
            return true;
        }

        if (BptcTextureCoder.IsSupported(format))
        {
            coder = new BptcTextureCoder(format);
            return true;
        }

        if (AstcTextureCoder.IsSupported(format))
        {
            coder = new AstcTextureCoder(format);
            return true;
        }

        if (EtcTextureCoder.IsSupported(format))
        {
            coder = new EtcTextureCoder(format);
            return true;
        }

        if (PvrtcTextureCoder.IsSupported(format))
        {
            coder = new PvrtcTextureCoder(format);
            return true;
        }

        if (SequentialUncompressedTextureCoder.IsSupported(format))
        {
            coder = new SequentialUncompressedTextureCoder(format);
            return true;
        }

        coder = null;
        return false;
    }

    private static bool TryCreateBuiltInCoder3D(TextureFormat format, [NotNullWhen(true)] out ITextureCoder3D? coder)
    {
        if (Astc3DTextureCoder.IsSupported(format))
        {
            coder = new Astc3DTextureCoder(format);
            return true;
        }

        coder = null;
        return false;
    }

    private sealed class CoderEntry(TextureFormat format, ITextureCoder coder)
    {
        public TextureFormat Format { get; } = format;

        public ITextureCoder Coder { get; } = coder;
    }

    private sealed class Coder3DEntry(TextureFormat format, ITextureCoder3D coder)
    {
        public TextureFormat Format { get; } = format;

        public ITextureCoder3D Coder { get; } = coder;
    }

    private sealed class Registration(TextureCoderManager manager, CoderEntry entry) : IDisposable
    {
        private TextureCoderManager? _manager = manager;

        public void Dispose()
        {
            var manager = Interlocked.Exchange(ref _manager, null);
            manager?.Unregister(entry);
        }
    }

    private sealed class Registration3D(TextureCoderManager manager, Coder3DEntry entry) : IDisposable
    {
        private TextureCoderManager? _manager = manager;

        public void Dispose()
        {
            var manager = Interlocked.Exchange(ref _manager, null);
            manager?.Unregister(entry);
        }
    }

    private sealed class CompositeRegistration(IReadOnlyList<IDisposable> registrations) : IDisposable
    {
        private IReadOnlyList<IDisposable>? _registrations = registrations ?? throw new ArgumentNullException(nameof(registrations));

        public CompositeRegistration(ReadOnlySpan<IDisposable> registrations)
            : this((IReadOnlyList<IDisposable>)[.. registrations])
        {
        }

        public CompositeRegistration(IEnumerable<IDisposable> registrations)
            : this((IReadOnlyList<IDisposable>)[.. registrations])
        {
        }

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
