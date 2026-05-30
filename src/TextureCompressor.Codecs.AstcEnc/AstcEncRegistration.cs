using TextureCompressor.Codecs;
using TextureCompressor.Formats;

namespace TextureCompressor.Codecs.AstcEnc;

public static class AstcEncRegistration
{
    public static IDisposable RegisterAstcEncCoder(
        this TextureCoderManager manager,
        TextureFormat format,
        AstcEncCoderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(manager);

        return manager.Register(format, new AstcEncTextureCoder(format, options));
    }

    public static IDisposable RegisterAstcEncCoders(
        this TextureCoderManager manager,
        IEnumerable<TextureFormat> formats,
        AstcEncCoderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(formats);

        var registrations = new List<IDisposable>();
        try
        {
            foreach (var format in formats)
            {
                registrations.Add(manager.RegisterAstcEncCoder(format, options));
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

    public static IDisposable RegisterAstcEncCoders(
        this TextureCoderManager manager,
        AstcEncCoderOptions? options = null)
    {
        return manager.RegisterAstcEncCoders(AstcEncTextureCoder.SupportedFormats.ToArray(), options);
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
