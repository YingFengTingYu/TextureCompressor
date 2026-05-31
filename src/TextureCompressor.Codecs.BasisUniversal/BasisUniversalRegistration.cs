using TextureCompressor.Codecs;
using TextureCompressor.Formats;
using TextureCompressor.Registry;

namespace TextureCompressor.Codecs.BasisUniversal;

public static class BasisUniversalRegistration
{
    public static IDisposable RegisterBasisUniversalCoder(
        this TextureCoderManager manager,
        TextureFormat format,
        BasisUniversalCoderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(manager);

        return manager.Register(format, new BasisUniversalTextureCoder(format, options));
    }

    public static IDisposable RegisterBasisUniversalCoders(
        this TextureCoderManager manager,
        IEnumerable<TextureFormat> formats,
        BasisUniversalCoderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(formats);

        var registrations = new List<IDisposable>();
        try
        {
            foreach (var format in formats)
            {
                registrations.Add(manager.RegisterBasisUniversalCoder(format, options));
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

    public static IDisposable RegisterBasisUniversalCoders(
        this TextureCoderManager manager,
        BasisUniversalCoderOptions? options = null)
    {
        return manager.RegisterBasisUniversalCoders(BasisUniversalTextureCoder.SupportedFormats.ToArray(), options);
    }

    private sealed class CompositeRegistration(IReadOnlyList<IDisposable> registrations) : IDisposable
    {
        private IReadOnlyList<IDisposable>? _registrations = registrations;

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
