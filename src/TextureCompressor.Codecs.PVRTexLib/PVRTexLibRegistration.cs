using TextureCompressor.Formats;

namespace TextureCompressor.Codecs.PVRTexLib;

public static class PVRTexLibRegistration
{
    public static IDisposable RegisterPVRTexLibCoder(
        this TextureCoderManager manager,
        TextureFormat format,
        PVRTexLibCompressorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(manager);

        return manager.Register(format, new PVRTexLibTextureCoder(format, options));
    }

    public static IDisposable RegisterPVRTexLibCoders(
        this TextureCoderManager manager,
        IEnumerable<TextureFormat> formats,
        PVRTexLibCompressorOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(formats);

        var registrations = new List<IDisposable>();
        try
        {
            foreach (var format in formats)
            {
                registrations.Add(manager.RegisterPVRTexLibCoder(format, options));
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

    public static IDisposable RegisterPVRTexLibCoders(
        this TextureCoderManager manager,
        PVRTexLibCompressorOptions? options = null)
    {
        return manager.RegisterPVRTexLibCoders(PVRTexLibTextureCoder.SupportedFormats.ToArray(), options);
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
