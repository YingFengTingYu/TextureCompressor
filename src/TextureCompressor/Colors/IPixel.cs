namespace TextureCompressor.Colors;

public interface IPixel<TSelf>
    where TSelf : unmanaged, IPixel<TSelf>
{
    static abstract Rgba8UNorm ToRgba8UNorm(TSelf value);

    static abstract TSelf FromRgba8UNorm(Rgba8UNorm value);

    static virtual Rgba8SNorm ToRgba8SNorm(TSelf value) =>
        Rgba8UNorm.ToRgba8SNorm(TSelf.ToRgba8UNorm(value));

    static virtual TSelf FromRgba8SNorm(Rgba8SNorm value) =>
        TSelf.FromRgba8UNorm(Rgba8UNorm.FromRgba8SNorm(value));

    static virtual Rgba16UNorm ToRgba16UNorm(TSelf value) =>
        Rgba8UNorm.ToRgba16UNorm(TSelf.ToRgba8UNorm(value));

    static virtual TSelf FromRgba16UNorm(Rgba16UNorm value) =>
        TSelf.FromRgba8UNorm(Rgba8UNorm.FromRgba16UNorm(value));

    static virtual Rgba16SNorm ToRgba16SNorm(TSelf value) =>
        Rgba8UNorm.ToRgba16SNorm(TSelf.ToRgba8UNorm(value));

    static virtual TSelf FromRgba16SNorm(Rgba16SNorm value) =>
        TSelf.FromRgba8UNorm(Rgba8UNorm.FromRgba16SNorm(value));

    static virtual Rgba32UNorm ToRgba32UNorm(TSelf value) =>
        Rgba8UNorm.ToRgba32UNorm(TSelf.ToRgba8UNorm(value));

    static virtual TSelf FromRgba32UNorm(Rgba32UNorm value) =>
        TSelf.FromRgba8UNorm(Rgba8UNorm.FromRgba32UNorm(value));

    static virtual Rgba32SNorm ToRgba32SNorm(TSelf value) =>
        Rgba8UNorm.ToRgba32SNorm(TSelf.ToRgba8UNorm(value));

    static virtual TSelf FromRgba32SNorm(Rgba32SNorm value) =>
        TSelf.FromRgba8UNorm(Rgba8UNorm.FromRgba32SNorm(value));

    static virtual Rgba16Float ToRgba16Float(TSelf value) =>
        Rgba8UNorm.ToRgba16Float(TSelf.ToRgba8UNorm(value));

    static virtual TSelf FromRgba16Float(Rgba16Float value) =>
        TSelf.FromRgba8UNorm(Rgba8UNorm.FromRgba16Float(value));

    static virtual Rgba32Float ToRgba32Float(TSelf value) =>
        Rgba8UNorm.ToRgba32Float(TSelf.ToRgba8UNorm(value));

    static virtual TSelf FromRgba32Float(Rgba32Float value) =>
        TSelf.FromRgba8UNorm(Rgba8UNorm.FromRgba32Float(value));

    static virtual Rgba64UNorm ToRgba64UNorm(TSelf value) =>
        Rgba8UNorm.ToRgba64UNorm(TSelf.ToRgba8UNorm(value));

    static virtual TSelf FromRgba64UNorm(Rgba64UNorm value) =>
        TSelf.FromRgba8UNorm(Rgba8UNorm.FromRgba64UNorm(value));

    static virtual Rgba64SNorm ToRgba64SNorm(TSelf value) =>
        Rgba8UNorm.ToRgba64SNorm(TSelf.ToRgba8UNorm(value));

    static virtual TSelf FromRgba64SNorm(Rgba64SNorm value) =>
        TSelf.FromRgba8UNorm(Rgba8UNorm.FromRgba64SNorm(value));

    static virtual Rgba64Float ToRgba64Float(TSelf value) =>
        Rgba8UNorm.ToRgba64Float(TSelf.ToRgba8UNorm(value));

    static virtual TSelf FromRgba64Float(Rgba64Float value) =>
        TSelf.FromRgba8UNorm(Rgba8UNorm.FromRgba64Float(value));
}
