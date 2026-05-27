using System.Diagnostics.CodeAnalysis;
using TextureCompressor.Formats;

namespace TextureCompressor.Codecs;

public sealed class TextureCoderManager
{
    private static readonly Lazy<TextureCoderManager> SGlobal = new(() => new TextureCoderManager());

    private readonly Dictionary<TextureFormat, ITextureCoder> _builtInCoders = [];
    private readonly List<CoderEntry> _coders = [];
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

    private void Unregister(CoderEntry entry)
    {
        lock (_sync)
        {
            _coders.Remove(entry);
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

    private static bool TryCreateBuiltInCoder(TextureFormat format, [NotNullWhen(true)] out ITextureCoder? coder)
    {
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

        if (PackedIntegerTextureCoder.IsSupported(format))
        {
            coder = new PackedIntegerTextureCoder(format);
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

    private sealed class CoderEntry(TextureFormat format, ITextureCoder coder)
    {
        public TextureFormat Format { get; } = format;

        public ITextureCoder Coder { get; } = coder;
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
}
