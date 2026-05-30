using TextureCompressor.Formats;

namespace TextureCompressor.Codecs.BCnEncoder;

public static class BCnEncoderRegistration
{
    public static IDisposable RegisterBCnEncoderCoder(
        this TextureCoderManager manager,
        TextureFormat format,
        BCnEncoderCoderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(manager);

        return manager.Register(format, new BCnEncoderTextureCoder(format, options));
    }

    public static IDisposable RegisterBCnEncoderCoders(
        this TextureCoderManager manager,
        IEnumerable<TextureFormat> formats,
        BCnEncoderCoderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(formats);

        var registrations = new List<IDisposable>();
        try
        {
            foreach (var format in formats)
            {
                registrations.Add(manager.RegisterBCnEncoderCoder(format, options));
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

    public static IDisposable RegisterBCnEncoderCoders(
        this TextureCoderManager manager,
        BCnEncoderCoderOptions? options = null)
    {
        return manager.RegisterBCnEncoderCoders(BCnEncoderTextureCoder.SupportedFormats.ToArray(), options);
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
