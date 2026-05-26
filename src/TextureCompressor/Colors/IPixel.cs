using TextureCompressor.Formats;

namespace TextureCompressor.Colors;

public interface IPixel<TSelf>
    where TSelf : unmanaged, IPixel<TSelf>
{
    static abstract TextureFormat Format { get; }

    static virtual Rgba8UNorm ToRgba8UNorm(TSelf value) => throw CreateUnsupportedConversionException(nameof(ToRgba8UNorm));

    static virtual TSelf FromRgba8UNorm(Rgba8UNorm value) => throw CreateUnsupportedConversionException(nameof(FromRgba8UNorm));

    static virtual Rgba8SNorm ToRgba8SNorm(TSelf value) => throw CreateUnsupportedConversionException(nameof(ToRgba8SNorm));

    static virtual TSelf FromRgba8SNorm(Rgba8SNorm value) => throw CreateUnsupportedConversionException(nameof(FromRgba8SNorm));

    static virtual Rgba16UNorm ToRgba16UNorm(TSelf value) => throw CreateUnsupportedConversionException(nameof(ToRgba16UNorm));

    static virtual TSelf FromRgba16UNorm(Rgba16UNorm value) => throw CreateUnsupportedConversionException(nameof(FromRgba16UNorm));

    static virtual Rgba16SNorm ToRgba16SNorm(TSelf value) => throw CreateUnsupportedConversionException(nameof(ToRgba16SNorm));

    static virtual TSelf FromRgba16SNorm(Rgba16SNorm value) => throw CreateUnsupportedConversionException(nameof(FromRgba16SNorm));

    static virtual Rgba32UNorm ToRgba32UNorm(TSelf value) => throw CreateUnsupportedConversionException(nameof(ToRgba32UNorm));

    static virtual TSelf FromRgba32UNorm(Rgba32UNorm value) => throw CreateUnsupportedConversionException(nameof(FromRgba32UNorm));

    static virtual Rgba32SNorm ToRgba32SNorm(TSelf value) => throw CreateUnsupportedConversionException(nameof(ToRgba32SNorm));

    static virtual TSelf FromRgba32SNorm(Rgba32SNorm value) => throw CreateUnsupportedConversionException(nameof(FromRgba32SNorm));

    static virtual Rgba16Float ToRgba16Float(TSelf value) => throw CreateUnsupportedConversionException(nameof(ToRgba16Float));

    static virtual TSelf FromRgba16Float(Rgba16Float value) => throw CreateUnsupportedConversionException(nameof(FromRgba16Float));

    static virtual Rgba32Float ToRgba32Float(TSelf value) => throw CreateUnsupportedConversionException(nameof(ToRgba32Float));

    static virtual TSelf FromRgba32Float(Rgba32Float value) => throw CreateUnsupportedConversionException(nameof(FromRgba32Float));

    private static NotSupportedException CreateUnsupportedConversionException(string methodName) =>
        new($"Pixel type '{typeof(TSelf).Name}' does not support conversion '{methodName}'.");
}
