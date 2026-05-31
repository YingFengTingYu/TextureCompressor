using TextureCompressor.Formats;
using TextureCompressor.Registry;

namespace TextureCompressor.Codecs.DirectXTex;

public static class DirectXTexRegistration
{
    public static IDisposable RegisterDirectXTexCoder(
        this TextureCoderManager manager,
        TextureFormat format,
        DirectXTexCoderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(manager);

        return manager.Register(format, new DirectXTexTextureCoder(format, options));
    }

    public static IDisposable RegisterDirectXTexCoders(
        this TextureCoderManager manager,
        IEnumerable<TextureFormat> formats,
        DirectXTexCoderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(manager);
        ArgumentNullException.ThrowIfNull(formats);

        var registrations = new List<IDisposable>();
        try
        {
            foreach (var format in formats)
            {
                registrations.Add(manager.RegisterDirectXTexCoder(format, options));
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

    public static IDisposable RegisterDirectXTexCoders(
        this TextureCoderManager manager,
        DirectXTexCoderOptions? options = null)
    {
        return manager.RegisterDirectXTexCoders(DirectXTexTextureCoder.SupportedFormats.ToArray(), options);
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
