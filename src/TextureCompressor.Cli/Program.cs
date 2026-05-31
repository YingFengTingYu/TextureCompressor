using System.CommandLine;
using System.Globalization;
using TextureCompressor.Analysis;
using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;
using TextureCompressor.Codecs;
using TextureCompressor.FileFormats.Astc;
using TextureCompressor.FileFormats.Dds;
using TextureCompressor.FileFormats.Gif;
using TextureCompressor.FileFormats.Jpeg;
using TextureCompressor.FileFormats.Ktx;
using TextureCompressor.FileFormats.Png;
using TextureCompressor.FileFormats.Pvr;
using TextureCompressor.Formats;
using TextureCompressor.Registry;
using TextureCompressor.Options;

var root = Cli.CreateRootCommand();
return root.Parse(args).Invoke();

internal static class Cli
{
    public static RootCommand CreateRootCommand()
    {
        var root = new RootCommand("TextureCompressor development CLI.");
        root.Subcommands.Add(CreateConvertCommand());
        root.Subcommands.Add(CreateQualityCommand());
        root.Subcommands.Add(CreateFormatsCommand());
        return root;
    }

    private static Command CreateConvertCommand()
    {
        var inputArgument = new Argument<FileInfo>("input")
        {
            Description = "Input texture or image file."
        };
        var outputArgument = new Argument<FileInfo>("output")
        {
            Description = "Output texture or image file."
        };
        var formatOption = new Option<string>("--format", "-f")
        {
            Description = "TextureFormats field name or texture format name. Use `formats <query>` to search.",
            DefaultValueFactory = _ => nameof(TextureFormats.Rgba8UNorm)
        };
        formatOption.Validators.Add(result =>
        {
            var value = result.GetValueOrDefault<string>();
            if (value is not null && !TextureFormatCatalog.TryGet(value, out _))
            {
                result.AddError(BuildUnknownFormatMessage(value));
            }
        });
        var containerOption = new Option<TextureContainer?>("--container", "-c")
        {
            Description = "Output container. Defaults to the output file extension."
        };
        var metricsOption = new Option<bool>("--metrics", "-m")
        {
            Description = "Decode the written output and print quality metrics."
        };
        var pngColorSpaceOption = new Option<ImageColorSpace>("--png-color-space")
        {
            Description = "How to interpret and write PNG RGB values.",
            DefaultValueFactory = _ => ImageColorSpace.Linear
        };
        var jpgColorSpaceOption = new Option<ImageColorSpace>("--jpg-color-space")
        {
            Description = "How to interpret and write JPEG RGB values.",
            DefaultValueFactory = _ => ImageColorSpace.Linear
        };
        var gifColorSpaceOption = new Option<ImageColorSpace>("--gif-color-space")
        {
            Description = "How to interpret and write GIF RGB values.",
            DefaultValueFactory = _ => ImageColorSpace.Linear
        };
        var ktxVersionOption = new Option<int>("--ktx-version")
        {
            Description = "KTX version to write.",
            DefaultValueFactory = _ => 1
        };
        ktxVersionOption.Validators.Add(result =>
        {
            var version = result.GetValueOrDefault<int>();
            if (version is not (1 or 2))
            {
                result.AddError("--ktx-version must be 1 or 2.");
            }
        });
        var jpegQualityOption = new Option<int>("--jpeg-quality")
        {
            Description = "JPEG output quality from 1 to 100.",
            DefaultValueFactory = _ => 90
        };
        jpegQualityOption.Validators.Add(result =>
        {
            var quality = result.GetValueOrDefault<int>();
            if (quality is < 1 or > 100)
            {
                result.AddError("--jpeg-quality must be between 1 and 100.");
            }
        });
        var qualityOption = new Option<TextureCompressionLevel?>("--quality")
        {
            Description = "Built-in texture compression quality."
        };

        var command = new Command("convert", "Convert between supported texture and image containers.");
        command.Arguments.Add(inputArgument);
        command.Arguments.Add(outputArgument);
        command.Options.Add(formatOption);
        command.Options.Add(containerOption);
        command.Options.Add(metricsOption);
        command.Options.Add(pngColorSpaceOption);
        command.Options.Add(jpgColorSpaceOption);
        command.Options.Add(gifColorSpaceOption);
        command.Options.Add(ktxVersionOption);
        command.Options.Add(jpegQualityOption);
        command.Options.Add(qualityOption);
        command.SetAction(parseResult => RunCommand(() =>
        {
            var inputPath = RequireFile(parseResult.GetValue(inputArgument), "input").FullName;
            var outputPath = RequireFile(parseResult.GetValue(outputArgument), "output").FullName;
            var format = TextureFormatCatalog.Get(parseResult.GetValue(formatOption) ?? nameof(TextureFormats.Rgba8UNorm));
            var outputKind = parseResult.GetValue(containerOption) ?? GetContainer(outputPath);
            var ktxVersion = parseResult.GetValue(ktxVersionOption);
            var jpegQuality = parseResult.GetValue(jpegQualityOption);
            var quality = parseResult.GetValue(qualityOption);
            var colorSpaces = new ImageColorSpaces(
                parseResult.GetValue(pngColorSpaceOption),
                parseResult.GetValue(jpgColorSpaceOption),
                parseResult.GetValue(gifColorSpaceOption));
            var printMetrics = parseResult.GetValue(metricsOption);

            var source = Decode(inputPath, colorSpaces);
            Encode(source, outputPath, outputKind, format, ktxVersion, jpegQuality, quality, colorSpaces);
            Console.WriteLine($"wrote {outputPath}");

            if (printMetrics)
            {
                var decoded = Decode(outputPath, colorSpaces);
                PrintQuality(BitmapQuality.Compare(source, decoded));
            }

            return 0;
        }));

        return command;
    }

    private static Command CreateFormatsCommand()
    {
        var queryArgument = new Argument<string?>("query")
        {
            Description = "Optional text to search in field names or texture format names.",
            Arity = ArgumentArity.ZeroOrOne
        };
        var compressedOption = new Option<bool>("--compressed")
        {
            Description = "Show only compressed texture formats."
        };
        var uncompressedOption = new Option<bool>("--uncompressed")
        {
            Description = "Show only uncompressed texture formats."
        };

        var command = new Command("formats", "List and search texture formats accepted by --format.");
        command.Arguments.Add(queryArgument);
        command.Options.Add(compressedOption);
        command.Options.Add(uncompressedOption);
        command.Validators.Add(result =>
        {
            var compressed = result.GetValue(compressedOption);
            var uncompressed = result.GetValue(uncompressedOption);
            if (compressed && uncompressed)
            {
                result.AddError("Use either --compressed or --uncompressed, not both.");
            }
        });
        command.SetAction(parseResult => RunCommand(() =>
        {
            var query = parseResult.GetValue(queryArgument);
            var compressed = parseResult.GetValue(compressedOption);
            var uncompressed = parseResult.GetValue(uncompressedOption);
            var formats = GetFormatEntries(query, compressed, uncompressed).ToArray();

            if (formats.Length == 0)
            {
                Console.WriteLine("No matching texture formats.");
                return 0;
            }

            PrintFormats(formats);
            return 0;
        }));

        return command;
    }

    private static Command CreateQualityCommand()
    {
        var expectedArgument = new Argument<FileInfo>("expected")
        {
            Description = "Expected/reference texture or image file."
        };
        var actualArgument = new Argument<FileInfo>("actual")
        {
            Description = "Actual texture or image file to compare."
        };
        var ignoreAlphaOption = new Option<bool>("--ignore-alpha")
        {
            Description = "Ignore alpha channel differences."
        };
        var pngColorSpaceOption = new Option<ImageColorSpace>("--png-color-space")
        {
            Description = "How to interpret PNG RGB values.",
            DefaultValueFactory = _ => ImageColorSpace.Linear
        };
        var jpgColorSpaceOption = new Option<ImageColorSpace>("--jpg-color-space")
        {
            Description = "How to interpret JPEG RGB values.",
            DefaultValueFactory = _ => ImageColorSpace.Linear
        };
        var gifColorSpaceOption = new Option<ImageColorSpace>("--gif-color-space")
        {
            Description = "How to interpret GIF RGB values.",
            DefaultValueFactory = _ => ImageColorSpace.Linear
        };

        var command = new Command("quality", "Compare two decoded images and print quality metrics.");
        command.Arguments.Add(expectedArgument);
        command.Arguments.Add(actualArgument);
        command.Options.Add(ignoreAlphaOption);
        command.Options.Add(pngColorSpaceOption);
        command.Options.Add(jpgColorSpaceOption);
        command.Options.Add(gifColorSpaceOption);
        command.SetAction(parseResult => RunCommand(() =>
        {
            var expectedPath = RequireFile(parseResult.GetValue(expectedArgument), "expected").FullName;
            var actualPath = RequireFile(parseResult.GetValue(actualArgument), "actual").FullName;
            var includeAlpha = !parseResult.GetValue(ignoreAlphaOption);
            var colorSpaces = new ImageColorSpaces(
                parseResult.GetValue(pngColorSpaceOption),
                parseResult.GetValue(jpgColorSpaceOption),
                parseResult.GetValue(gifColorSpaceOption));

            var expected = Decode(expectedPath, colorSpaces);
            var actual = Decode(actualPath, colorSpaces);
            PrintQuality(BitmapQuality.Compare(expected, actual, includeAlpha));

            return 0;
        }));

        return command;
    }

    private static FileInfo RequireFile(FileInfo? file, string argumentName) =>
        file ?? throw new ArgumentException($"Missing required argument '{argumentName}'.");

    private static ArrayBitmap<Rgba8UNorm> Decode(string path, ImageColorSpaces? imageColorSpaces = null)
    {
        var container = GetContainer(path);
        var bitmap = container switch
        {
            TextureContainer.Png => PngCodec.Decode(path),
            TextureContainer.Jpeg => JpegCodec.Decode(path),
            TextureContainer.Gif => GifCodec.Decode(path),
            TextureContainer.Dds => DdsCodec.Decode(path),
            TextureContainer.Ktx => KtxCodec.Decode(path),
            TextureContainer.Pvr => PvrCodec.Decode(path),
            TextureContainer.Astc => AstcCodec.Decode(path),
            _ => throw new NotSupportedException($"Unsupported input extension '{Path.GetExtension(path)}'.")
        };

        return ApplyInputImageColorSpace(bitmap, container, GetImageColorSpace(container, imageColorSpaces));
    }

    private static void Encode(
        IBitmap<Rgba8UNorm> bitmap,
        string path,
        TextureContainer container,
        TextureFormat format,
        int ktxVersion,
        int jpegQuality,
        TextureCompressionLevel? quality,
        ImageColorSpaces? imageColorSpaces)
    {
        var imageColorSpace = GetImageColorSpace(container, imageColorSpaces);
        if (imageColorSpace == ImageColorSpace.Srgb)
        {
            EnsureImageColorSpaceContainer(container);
        }

        using var s3tcRegistration = CreateS3tcRegistration(format, quality);
        using var fxtcRegistration = CreateFxtcRegistration(format, quality);
        using var etcRegistration = CreateEtcRegistration(format, quality);
        using var atcRegistration = CreateAtcRegistration(format, quality);
        using var rgtcRegistration = CreateRgtcRegistration(format, quality);
        using var bptcRegistration = CreateBptcRegistration(format, quality);
        using var pvrtcRegistration = CreatePvrtcRegistration(format, quality);
        using var astcRegistration = CreateAstcRegistration(format, quality);

        switch (container)
        {
            case TextureContainer.Png:
                PngCodec.Encode(ApplyOutputImageColorSpace(bitmap, container, imageColorSpace), path);
                break;
            case TextureContainer.Jpeg:
                JpegCodec.Encode(ApplyOutputImageColorSpace(bitmap, container, imageColorSpace), path, new JpegEncodingOptions { Quality = jpegQuality });
                break;
            case TextureContainer.Gif:
                GifCodec.Encode(ApplyOutputImageColorSpace(bitmap, container, imageColorSpace), path);
                break;
            case TextureContainer.Dds:
                DdsCodec.Encode(bitmap, path, new DdsEncodingOptions { TextureFormat = format });
                break;
            case TextureContainer.Ktx:
                KtxCodec.Encode(bitmap, path, new KtxEncodingOptions
                {
                    TextureFormat = format,
                    Version = ktxVersion == 2 ? KtxVersion.Version2 : KtxVersion.Version1
                });
                break;
            case TextureContainer.Pvr:
                PvrCodec.Encode(bitmap, path, new PvrEncodingOptions { TextureFormat = format });
                break;
            case TextureContainer.Astc:
                AstcCodec.Encode(bitmap, path, new AstcEncodingOptions { TextureFormat = format });
                break;
            default:
                throw new NotSupportedException($"Unsupported output container '{container}'.");
        }
    }

    private static IDisposable? CreateS3tcRegistration(TextureFormat format, TextureCompressionLevel? quality)
    {
        if (quality is null || !S3tcTextureCoder.IsSupported(format))
        {
            return null;
        }

        var options = new S3tcCoderOptions { CompressionMode = quality.Value };
        return TextureCoderManager.Global.Register(format, new S3tcTextureCoder(format, options));
    }

    private static IDisposable? CreateFxtcRegistration(TextureFormat format, TextureCompressionLevel? quality)
    {
        if (quality is null || !FxtcTextureCoder.IsSupported(format))
        {
            return null;
        }

        var options = new FxtcCoderOptions { CompressionMode = quality.Value };
        return TextureCoderManager.Global.Register(format, new FxtcTextureCoder(format, options));
    }

    private static IDisposable? CreateEtcRegistration(TextureFormat format, TextureCompressionLevel? quality)
    {
        if (quality is null || !EtcTextureCoder.IsSupported(format))
        {
            return null;
        }

        var options = new EtcCoderOptions { CompressionMode = quality.Value };
        return TextureCoderManager.Global.Register(format, new EtcTextureCoder(format, options));
    }

    private static IDisposable? CreateAtcRegistration(TextureFormat format, TextureCompressionLevel? quality)
    {
        if (quality is null || !AtcTextureCoder.IsSupported(format))
        {
            return null;
        }

        var options = new AtcCoderOptions { CompressionMode = quality.Value };
        return TextureCoderManager.Global.Register(format, new AtcTextureCoder(format, options));
    }

    private static IDisposable? CreateRgtcRegistration(TextureFormat format, TextureCompressionLevel? quality)
    {
        if (quality is null || !RgtcLatcTextureCoder.IsSupported(format))
        {
            return null;
        }

        var options = new RgtcLatcCoderOptions { CompressionMode = quality.Value };
        return TextureCoderManager.Global.Register(format, new RgtcLatcTextureCoder(format, options));
    }

    private static IDisposable? CreateBptcRegistration(TextureFormat format, TextureCompressionLevel? quality)
    {
        if (quality is null || !BptcTextureCoder.IsSupported(format))
        {
            return null;
        }

        var options = new BptcCoderOptions { CompressionMode = quality.Value };
        return TextureCoderManager.Global.Register(format, new BptcTextureCoder(format, options));
    }

    private static IDisposable? CreatePvrtcRegistration(TextureFormat format, TextureCompressionLevel? quality)
    {
        if (quality is null || !PvrtcTextureCoder.IsSupported(format))
        {
            return null;
        }

        var options = new PvrtcCoderOptions { CompressionMode = quality.Value };
        return TextureCoderManager.Global.Register(format, new PvrtcTextureCoder(format, options));
    }

    private static IDisposable? CreateAstcRegistration(TextureFormat format, TextureCompressionLevel? quality)
    {
        if (quality is null || !AstcTextureCoder.IsSupported(format))
        {
            return null;
        }

        var options = new AstcCoderOptions { CompressionMode = quality.Value };
        return TextureCoderManager.Global.Register(format, new AstcTextureCoder(format, options));
    }

    private static ArrayBitmap<Rgba8UNorm> ApplyInputImageColorSpace(
        ArrayBitmap<Rgba8UNorm> bitmap,
        TextureContainer container,
        ImageColorSpace imageColorSpace)
    {
        if (imageColorSpace == ImageColorSpace.Linear)
        {
            return bitmap;
        }

        EnsureImageColorSpaceContainer(container);
        return TransformRgb(bitmap, RgbaColorConversions.Srgb8ToLinearUNorm8);
    }

    private static IBitmap<Rgba8UNorm> ApplyOutputImageColorSpace(
        IBitmap<Rgba8UNorm> bitmap,
        TextureContainer container,
        ImageColorSpace imageColorSpace)
    {
        if (imageColorSpace == ImageColorSpace.Linear)
        {
            return bitmap;
        }

        EnsureImageColorSpaceContainer(container);
        return TransformRgb(bitmap, RgbaColorConversions.LinearUNorm8ToSrgb8);
    }

    private static ArrayBitmap<Rgba8UNorm> TransformRgb(
        IBitmap<Rgba8UNorm> bitmap,
        Func<byte, byte> transform)
    {
        var source = bitmap.AsView().Pixels;
        var pixels = new Rgba8UNorm[source.Length];
        for (var i = 0; i < pixels.Length; i++)
        {
            var pixel = source[i];
            pixels[i] = new Rgba8UNorm(
                transform(pixel.Red),
                transform(pixel.Green),
                transform(pixel.Blue),
                pixel.Alpha);
        }

        return new ArrayBitmap<Rgba8UNorm>(bitmap.Width, bitmap.Height, pixels);
    }

    private static void EnsureImageColorSpaceContainer(TextureContainer container)
    {
        if (container is not (TextureContainer.Png or TextureContainer.Jpeg or TextureContainer.Gif))
        {
            throw new NotSupportedException("Image color space conversion applies only to PNG, JPEG, and GIF containers.");
        }
    }

    private static ImageColorSpace GetImageColorSpace(TextureContainer container, ImageColorSpaces? imageColorSpaces)
    {
        var colorSpaces = imageColorSpaces ?? ImageColorSpaces.Default;
        return container switch
        {
            TextureContainer.Png => colorSpaces.Png,
            TextureContainer.Jpeg => colorSpaces.Jpeg,
            TextureContainer.Gif => colorSpaces.Gif,
            _ => ImageColorSpace.Linear
        };
    }

    private static TextureContainer GetContainer(string path)
    {
        var extension = Path.GetExtension(path).ToLowerInvariant();
        return extension switch
        {
            ".png" => TextureContainer.Png,
            ".jpg" or ".jpeg" => TextureContainer.Jpeg,
            ".gif" => TextureContainer.Gif,
            ".dds" => TextureContainer.Dds,
            ".ktx" or ".ktx2" => TextureContainer.Ktx,
            ".pvr" => TextureContainer.Pvr,
            ".astc" => TextureContainer.Astc,
            _ => throw new NotSupportedException($"Unsupported file extension '{extension}'.")
        };
    }

    private static IEnumerable<FormatEntry> GetFormatEntries(string? query, bool compressedOnly, bool uncompressedOnly)
    {
        var normalizedQuery = NormalizeFormatName(query);
        foreach (var format in TextureFormatCatalog.All)
        {
            if (compressedOnly && !format.IsCompressed)
            {
                continue;
            }

            if (uncompressedOnly && format.IsCompressed)
            {
                continue;
            }

            var fieldName = TextureFormatCatalog.GetFieldName(format);
            if (normalizedQuery.Length > 0
                && !NormalizeFormatName(fieldName).Contains(normalizedQuery, StringComparison.Ordinal)
                && !NormalizeFormatName(format.Name).Contains(normalizedQuery, StringComparison.Ordinal))
            {
                continue;
            }

            yield return new FormatEntry(fieldName, format);
        }
    }

    private static void PrintFormats(IReadOnlyList<FormatEntry> formats)
    {
        var fieldWidth = Math.Max("Field name".Length, formats.Max(static item => item.FieldName.Length));
        var formatWidth = Math.Max("Format name".Length, formats.Max(static item => item.Format.Name.Length));

        Console.WriteLine($"{Pad("Field name", fieldWidth)}  {Pad("Format name", formatWidth)}  Kind");
        Console.WriteLine($"{new string('-', fieldWidth)}  {new string('-', formatWidth)}  ----");
        foreach (var entry in formats.OrderBy(static item => item.FieldName, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"{Pad(entry.FieldName, fieldWidth)}  {Pad(entry.Format.Name, formatWidth)}  {entry.Format.Kind}");
        }
    }

    private static string BuildUnknownFormatMessage(string value)
    {
        var suggestions = GetFormatSuggestions(value, maxCount: 6).ToArray();
        if (suggestions.Length == 0)
        {
            return $"Unknown texture format '{value}'. Use `formats <query>` to search available formats.";
        }

        return string.Join(
            Environment.NewLine,
            [$"Unknown texture format '{value}'. Did you mean one of these?", .. suggestions.Select(static item => $"  {item.FieldName} ({item.Format.Name})")]);
    }

    private static IEnumerable<FormatEntry> GetFormatSuggestions(string query, int maxCount)
    {
        var normalizedQuery = NormalizeFormatName(query);
        if (normalizedQuery.Length == 0)
        {
            return [];
        }

        return TextureFormatCatalog.All
            .Select(static format => new FormatEntry(TextureFormatCatalog.GetFieldName(format), format))
            .Select(entry => new
            {
                Entry = entry,
                Score = GetSuggestionScore(normalizedQuery, NormalizeFormatName(entry.FieldName), NormalizeFormatName(entry.Format.Name))
            })
            .Where(static item => item.Score < int.MaxValue)
            .OrderBy(static item => item.Score)
            .ThenBy(static item => item.Entry.FieldName, StringComparer.OrdinalIgnoreCase)
            .Take(maxCount)
            .Select(static item => item.Entry);
    }

    private static int GetSuggestionScore(string query, string fieldName, string formatName)
    {
        if (fieldName.Equals(query, StringComparison.Ordinal) || formatName.Equals(query, StringComparison.Ordinal))
        {
            return 0;
        }

        if (fieldName.StartsWith(query, StringComparison.Ordinal) || formatName.StartsWith(query, StringComparison.Ordinal))
        {
            return 1;
        }

        if (fieldName.Contains(query, StringComparison.Ordinal) || formatName.Contains(query, StringComparison.Ordinal))
        {
            return 2;
        }

        var distance = Math.Min(GetPrefixDistance(query, fieldName), GetPrefixDistance(query, formatName));
        var threshold = Math.Max(2, query.Length / 3);
        return distance <= threshold ? 10 + distance : int.MaxValue;
    }

    private static int GetPrefixDistance(string query, string candidate)
    {
        var prefixLength = Math.Min(query.Length, candidate.Length);
        var prefix = candidate[..prefixLength];
        return GetLevenshteinDistance(query, prefix);
    }

    private static int GetLevenshteinDistance(string left, string right)
    {
        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var j = 0; j <= right.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= left.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= right.Length; j++)
            {
                var cost = left[i - 1] == right[j - 1] ? 0 : 1;
                current[j] = Math.Min(
                    Math.Min(previous[j] + 1, current[j - 1] + 1),
                    previous[j - 1] + cost);
            }

            var swap = previous;
            previous = current;
            current = swap;
        }

        return previous[right.Length];
    }

    private static string NormalizeFormatName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value
            .Where(static c => char.IsLetterOrDigit(c))
            .Select(static c => char.ToUpper(c, CultureInfo.InvariantCulture))
            .ToArray());
    }

    private static string Pad(string value, int width) => value.PadRight(width);

    private static void PrintQuality(BitmapQualityResult quality)
    {
        Console.WriteLine($"size: {quality.Width}x{quality.Height}");
        Console.WriteLine($"mse: {quality.MeanSquaredError:F6}");
        Console.WriteLine($"rmse: {quality.RootMeanSquaredError:F6}");
        Console.WriteLine($"psnr: {FormatDecibels(quality.PeakSignalToNoiseRatio)}");
        Console.WriteLine($"r: rmse={quality.Red.RootMeanSquaredError:F6}, psnr={FormatDecibels(quality.Red.PeakSignalToNoiseRatio)}");
        Console.WriteLine($"g: rmse={quality.Green.RootMeanSquaredError:F6}, psnr={FormatDecibels(quality.Green.PeakSignalToNoiseRatio)}");
        Console.WriteLine($"b: rmse={quality.Blue.RootMeanSquaredError:F6}, psnr={FormatDecibels(quality.Blue.PeakSignalToNoiseRatio)}");
        if (quality.Alpha is not null)
        {
            Console.WriteLine($"a: rmse={quality.Alpha.RootMeanSquaredError:F6}, psnr={FormatDecibels(quality.Alpha.PeakSignalToNoiseRatio)}");
        }
    }

    private static string FormatDecibels(double value) =>
        double.IsPositiveInfinity(value) ? "inf" : $"{value:F3} dB";

    private static int RunCommand(Func<int> action)
    {
        try
        {
            return action();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }
}

internal enum TextureContainer
{
    Png,
    Jpeg,
    Gif,
    Dds,
    Ktx,
    Pvr,
    Astc
}

internal enum ImageColorSpace
{
    Linear,
    Srgb
}

internal sealed record ImageColorSpaces(ImageColorSpace Png, ImageColorSpace Jpeg, ImageColorSpace Gif)
{
    public static ImageColorSpaces Default { get; } = new(
        ImageColorSpace.Linear,
        ImageColorSpace.Linear,
        ImageColorSpace.Linear);
}

internal sealed record FormatEntry(string FieldName, TextureFormat Format);
