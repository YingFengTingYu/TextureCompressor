using System.CommandLine;
using System.Buffers.Binary;
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
        root.Subcommands.Add(CreateAssembleCommand());
        root.Subcommands.Add(CreateQualityCommand());
        root.Subcommands.Add(CreateFormatsCommand());
        root.Subcommands.Add(CreateInfoCommand("info", "Print metadata for a supported texture or image container."));
        root.Subcommands.Add(CreateInfoCommand("inspect", "Alias for info; print metadata for a supported texture or image container."));
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
        var formatOption = CreateFormatOption();
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
            Description = "KTX version to write. Defaults to 2 for .ktx2 outputs and 1 otherwise.",
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
        var mipmapsOption = new Option<MipmapMode>("--mipmaps")
        {
            Description = "Mip-map handling for texture outputs.",
            DefaultValueFactory = _ => MipmapMode.None
        };
        var mipOption = CreateMipOption();
        var layerOption = CreateLayerOption();
        var faceOption = CreateFaceOption();

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
        command.Options.Add(mipmapsOption);
        command.Options.Add(mipOption);
        command.Options.Add(layerOption);
        command.Options.Add(faceOption);
        command.SetAction(parseResult => RunCommand(() =>
        {
            var inputPath = RequireFile(parseResult.GetValue(inputArgument), "input").FullName;
            var outputPath = RequireFile(parseResult.GetValue(outputArgument), "output").FullName;
            var requestedFormat = TextureFormatCatalog.Get(parseResult.GetValue(formatOption) ?? nameof(TextureFormats.Rgba8UNorm));
            var formatWasSpecified = IsOptionExplicit(parseResult, formatOption);
            var outputKind = parseResult.GetValue(containerOption) ?? GetContainer(outputPath);
            var ktxVersion = IsOptionExplicit(parseResult, ktxVersionOption)
                ? parseResult.GetValue(ktxVersionOption)
                : GetDefaultKtxVersion(outputPath);
            var jpegQuality = parseResult.GetValue(jpegQualityOption);
            var quality = parseResult.GetValue(qualityOption);
            var mipmaps = parseResult.GetValue(mipmapsOption);
            var selection = GetSubresourceSelection(parseResult, mipOption, layerOption, faceOption);
            var hasSubresourceSelection = IsOptionExplicit(parseResult, mipOption)
                || IsOptionExplicit(parseResult, layerOption)
                || IsOptionExplicit(parseResult, faceOption);
            var colorSpaces = new ImageColorSpaces(
                parseResult.GetValue(pngColorSpaceOption),
                parseResult.GetValue(jpgColorSpaceOption),
                parseResult.GetValue(gifColorSpaceOption));
            var printMetrics = parseResult.GetValue(metricsOption);

            var inputKind = GetContainer(inputPath);
            if (IsStructuredTextureContainer(inputKind) && IsStructuredTextureContainer(outputKind))
            {
                var texture = ReadStructuredTexture(inputPath, inputKind);
                var format = formatWasSpecified ? requestedFormat : texture.Format;
                if (!hasSubresourceSelection && mipmaps == MipmapMode.None)
                {
                    WriteStructuredTexture(texture, outputPath, outputKind, format, ktxVersion, quality);
                    Console.WriteLine($"wrote {outputPath}");

                    if (printMetrics)
                    {
                        var decoded = Decode(outputPath, colorSpaces);
                        PrintQuality(BitmapQuality.Compare(DecodeSubresource(texture.Format, texture.GetSubresource(default)), decoded));
                    }

                    return 0;
                }

                var selectedSource = DecodeSubresource(texture.Format, texture.GetSubresource(selection));
                Encode(selectedSource, outputPath, outputKind, format, ktxVersion, jpegQuality, quality, mipmaps, colorSpaces);
                Console.WriteLine($"wrote {outputPath}");

                if (printMetrics)
                {
                    var decoded = Decode(outputPath, colorSpaces);
                    PrintQuality(BitmapQuality.Compare(selectedSource, decoded));
                }

                return 0;
            }

            var source = Decode(inputPath, colorSpaces, selection);
            Encode(source, outputPath, outputKind, requestedFormat, ktxVersion, jpegQuality, quality, mipmaps, colorSpaces);
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
        var mipOption = CreateMipOption();
        var layerOption = CreateLayerOption();
        var faceOption = CreateFaceOption();
        var expectedMipOption = CreateMipOption("--expected-mip", "Mip level to decode from the expected texture.");
        var expectedLayerOption = CreateLayerOption("--expected-layer", "Array layer to decode from the expected texture.");
        var expectedFaceOption = CreateFaceOption("--expected-face", "Cube-map face to decode from the expected texture.");
        var actualMipOption = CreateMipOption("--actual-mip", "Mip level to decode from the actual texture.");
        var actualLayerOption = CreateLayerOption("--actual-layer", "Array layer to decode from the actual texture.");
        var actualFaceOption = CreateFaceOption("--actual-face", "Cube-map face to decode from the actual texture.");

        var command = new Command("quality", "Compare two decoded images and print quality metrics.");
        command.Arguments.Add(expectedArgument);
        command.Arguments.Add(actualArgument);
        command.Options.Add(ignoreAlphaOption);
        command.Options.Add(pngColorSpaceOption);
        command.Options.Add(jpgColorSpaceOption);
        command.Options.Add(gifColorSpaceOption);
        command.Options.Add(mipOption);
        command.Options.Add(layerOption);
        command.Options.Add(faceOption);
        command.Options.Add(expectedMipOption);
        command.Options.Add(expectedLayerOption);
        command.Options.Add(expectedFaceOption);
        command.Options.Add(actualMipOption);
        command.Options.Add(actualLayerOption);
        command.Options.Add(actualFaceOption);
        command.SetAction(parseResult => RunCommand(() =>
        {
            var expectedPath = RequireFile(parseResult.GetValue(expectedArgument), "expected").FullName;
            var actualPath = RequireFile(parseResult.GetValue(actualArgument), "actual").FullName;
            var includeAlpha = !parseResult.GetValue(ignoreAlphaOption);
            var commonSelection = GetSubresourceSelection(parseResult, mipOption, layerOption, faceOption);
            var expectedSelection = GetSubresourceSelection(parseResult, expectedMipOption, expectedLayerOption, expectedFaceOption, commonSelection);
            var actualSelection = GetSubresourceSelection(parseResult, actualMipOption, actualLayerOption, actualFaceOption, commonSelection);
            var colorSpaces = new ImageColorSpaces(
                parseResult.GetValue(pngColorSpaceOption),
                parseResult.GetValue(jpgColorSpaceOption),
                parseResult.GetValue(gifColorSpaceOption));

            var expected = Decode(expectedPath, colorSpaces, expectedSelection);
            var actual = Decode(actualPath, colorSpaces, actualSelection);
            PrintQuality(BitmapQuality.Compare(expected, actual, includeAlpha));

            return 0;
        }));

        return command;
    }

    private static Command CreateInfoCommand(string name, string description)
    {
        var inputArgument = new Argument<FileInfo>("input")
        {
            Description = "Input texture or image file."
        };
        var subresourcesOption = new Option<bool>("--subresources")
        {
            Description = "Print per-subresource metadata for texture containers."
        };

        var command = new Command(name, description);
        command.Arguments.Add(inputArgument);
        command.Options.Add(subresourcesOption);
        command.SetAction(parseResult => RunCommand(() =>
        {
            var inputPath = RequireFile(parseResult.GetValue(inputArgument), "input").FullName;
            PrintInfo(inputPath, parseResult.GetValue(subresourcesOption));
            return 0;
        }));

        return command;
    }

    private static Command CreateAssembleCommand()
    {
        var outputArgument = new Argument<FileInfo>("output")
        {
            Description = "Output DDS, KTX, or PVR texture file."
        };
        var layersOption = new Option<FileInfo[]>("--layers")
        {
            Description = "Input images to assemble as 2D array layers.",
            Arity = ArgumentArity.OneOrMore,
            AllowMultipleArgumentsPerToken = true
        };
        var cubeOption = new Option<FileInfo[]>("--cube")
        {
            Description = "Six input images for cube faces in PositiveX NegativeX PositiveY NegativeY PositiveZ NegativeZ order.",
            Arity = new ArgumentArity(6, 6),
            AllowMultipleArgumentsPerToken = true
        };
        var mipsOption = new Option<FileInfo[]>("--mips")
        {
            Description = "Input images to assemble as an explicit mip-map chain.",
            Arity = ArgumentArity.OneOrMore,
            AllowMultipleArgumentsPerToken = true
        };
        var formatOption = CreateFormatOption();
        var containerOption = new Option<TextureContainer?>("--container", "-c")
        {
            Description = "Output container. Defaults to the output file extension."
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
        var ktxVersionOption = new Option<int>("--ktx-version")
        {
            Description = "KTX version to write. Defaults to 2 for .ktx2 outputs and 1 otherwise.",
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
        var qualityOption = new Option<TextureCompressionLevel?>("--quality")
        {
            Description = "Built-in texture compression quality."
        };

        var command = new Command("assemble", "Assemble multiple image files into a DDS, KTX, or PVR texture.");
        command.Arguments.Add(outputArgument);
        command.Options.Add(layersOption);
        command.Options.Add(cubeOption);
        command.Options.Add(mipsOption);
        command.Options.Add(formatOption);
        command.Options.Add(containerOption);
        command.Options.Add(pngColorSpaceOption);
        command.Options.Add(jpgColorSpaceOption);
        command.Options.Add(gifColorSpaceOption);
        command.Options.Add(ktxVersionOption);
        command.Options.Add(qualityOption);
        command.Validators.Add(result =>
        {
            var modeCount = 0;
            if (result.GetResult(layersOption) is { Implicit: false })
            {
                modeCount++;
            }

            if (result.GetResult(cubeOption) is { Implicit: false })
            {
                modeCount++;
            }

            if (result.GetResult(mipsOption) is { Implicit: false })
            {
                modeCount++;
            }

            if (modeCount != 1)
            {
                result.AddError("Specify exactly one of --layers, --cube, or --mips.");
            }
        });
        command.SetAction(parseResult => RunCommand(() =>
        {
            var outputPath = RequireFile(parseResult.GetValue(outputArgument), "output").FullName;
            var outputKind = parseResult.GetValue(containerOption) ?? GetContainer(outputPath);
            if (!IsStructuredTextureContainer(outputKind))
            {
                throw new NotSupportedException("Assemble output must be DDS, KTX, or PVR.");
            }

            var format = TextureFormatCatalog.Get(parseResult.GetValue(formatOption) ?? nameof(TextureFormats.Rgba8UNorm));
            var ktxVersion = IsOptionExplicit(parseResult, ktxVersionOption)
                ? parseResult.GetValue(ktxVersionOption)
                : GetDefaultKtxVersion(outputPath);
            var colorSpaces = new ImageColorSpaces(
                parseResult.GetValue(pngColorSpaceOption),
                parseResult.GetValue(jpgColorSpaceOption),
                parseResult.GetValue(gifColorSpaceOption));
            var quality = parseResult.GetValue(qualityOption);

            using var compressionRegistration = CreateTextureCompressionRegistration(format, quality);
            var texture = CreateAssembledTexture(
                format,
                colorSpaces,
                parseResult.GetValue(layersOption) ?? [],
                parseResult.GetValue(cubeOption) ?? [],
                parseResult.GetValue(mipsOption) ?? []);
            WriteStructuredTexture(texture, outputPath, outputKind, format, ktxVersion, quality: null);
            Console.WriteLine($"wrote {outputPath}");
            return 0;
        }));

        return command;
    }

    private static Option<string> CreateFormatOption()
    {
        var option = new Option<string>("--format", "-f")
        {
            Description = "TextureFormats field name or texture format name. Use `formats <query>` to search.",
            DefaultValueFactory = _ => nameof(TextureFormats.Rgba8UNorm)
        };
        option.Validators.Add(result =>
        {
            var value = result.GetValueOrDefault<string>();
            if (value is not null && !TextureFormatCatalog.TryGet(value, out _))
            {
                result.AddError(BuildUnknownFormatMessage(value));
            }
        });

        return option;
    }

    private static Option<int> CreateMipOption(string name = "--mip", string? description = null)
    {
        var option = new Option<int>(name)
        {
            Description = description ?? "Mip level to decode from texture inputs.",
            DefaultValueFactory = _ => 0
        };
        option.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int>() < 0)
            {
                result.AddError($"{name} must be zero or greater.");
            }
        });

        return option;
    }

    private static Option<int> CreateLayerOption(string name = "--layer", string? description = null)
    {
        var option = new Option<int>(name)
        {
            Description = description ?? "Array layer to decode from texture inputs.",
            DefaultValueFactory = _ => 0
        };
        option.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int>() < 0)
            {
                result.AddError($"{name} must be zero or greater.");
            }
        });

        return option;
    }

    private static Option<TextureCubeFace?> CreateFaceOption(string name = "--face", string? description = null) =>
        new(name)
        {
            Description = description ?? "Cube-map face to decode from texture inputs."
        };

    private static TextureSubresourceSelection GetSubresourceSelection(
        ParseResult parseResult,
        Option<int> mipOption,
        Option<int> layerOption,
        Option<TextureCubeFace?> faceOption,
        TextureSubresourceSelection? fallback = null)
    {
        var mipLevel = parseResult.GetValue(mipOption);
        var arrayLayer = parseResult.GetValue(layerOption);
        var face = parseResult.GetValue(faceOption);
        if (mipLevel == 0 && arrayLayer == 0 && face is null && fallback is { } fallbackSelection)
        {
            return fallbackSelection;
        }

        return new TextureSubresourceSelection(mipLevel, arrayLayer, face);
    }

    private static FileInfo RequireFile(FileInfo? file, string argumentName) =>
        file ?? throw new ArgumentException($"Missing required argument '{argumentName}'.");

    private static bool IsOptionExplicit(ParseResult parseResult, Option option) =>
        parseResult.GetResult(option) is { Implicit: false };

    private static int GetDefaultKtxVersion(string outputPath) =>
        string.Equals(Path.GetExtension(outputPath), ".ktx2", StringComparison.OrdinalIgnoreCase) ? 2 : 1;

    private static void PrintInfo(string path, bool printSubresources)
    {
        var fileBytes = new FileInfo(path).Length;
        var container = GetContainer(path);

        switch (container)
        {
            case TextureContainer.Png:
                PrintImageInfo(container, PngCodec.Decode(path), fileBytes);
                break;
            case TextureContainer.Jpeg:
                PrintImageInfo(container, JpegCodec.Decode(path), fileBytes);
                break;
            case TextureContainer.Gif:
                PrintImageInfo(container, GifCodec.Decode(path), fileBytes);
                break;
            case TextureContainer.Dds:
                PrintDdsInfo(DdsCodec.Read(path), fileBytes, printSubresources);
                break;
            case TextureContainer.Ktx:
                PrintKtxInfo(KtxCodec.Read(path), ReadKtxInfo(path), fileBytes, printSubresources);
                break;
            case TextureContainer.Pvr:
                PrintPvrInfo(PvrCodec.Read(path), ReadPvrInfo(path), fileBytes, printSubresources);
                break;
            case TextureContainer.Astc:
                PrintAstcInfo(AstcCodec.Read(path), fileBytes);
                break;
            default:
                throw new NotSupportedException($"Unsupported input extension '{Path.GetExtension(path)}'.");
        }
    }

    private static void PrintImageInfo(TextureContainer container, IBitmap<Rgba8UNorm> bitmap, long fileBytes)
    {
        PrintInfoLine("Container", FormatContainer(container));
        PrintInfoLine("Size", FormatSize(bitmap.Width, bitmap.Height));
        PrintInfoLine("Decoded format", nameof(TextureFormats.Rgba8UNorm));
        PrintInfoLine("File bytes", FormatInvariant(fileBytes));
    }

    private static void PrintDdsInfo(DdsTexture texture, long fileBytes, bool printSubresources)
    {
        PrintTextureInfo(TextureContainer.Dds, texture.Format, texture.Width, texture.Height, texture.MipLevelCount, texture.ArrayLayerCount, texture.FaceCount, GetPayloadByteCount(texture.Subresources), fileBytes);
        PrintInfoLine("Header", texture.HeaderKind);
        if (texture.DxgiFormat is not null)
        {
            PrintInfoLine("DXGI format", texture.DxgiFormat);
            PrintInfoLine("Alpha mode", texture.AlphaMode);
        }

        if (texture.LegacyPixelFormat is not null)
        {
            PrintInfoLine("Legacy pixel format", texture.LegacyPixelFormat);
        }

        if (printSubresources)
        {
            PrintSubresources(texture.Subresources, texture.FaceCount);
        }
    }

    private static void PrintKtxInfo(KtxTexture texture, KtxInfo info, long fileBytes, bool printSubresources)
    {
        PrintTextureInfo(TextureContainer.Ktx, texture.Format, texture.Width, texture.Height, texture.MipLevelCount, texture.ArrayLayerCount, texture.FaceCount, GetPayloadByteCount(texture.Subresources), fileBytes);
        PrintInfoLine("Version", info.Version);
        if (texture.VkFormat is not null)
        {
            PrintInfoLine("VK format", texture.VkFormat);
        }

        if (texture.GlType is not null)
        {
            PrintInfoLine("GL type", texture.GlType);
        }

        if (texture.GlFormat is not null)
        {
            PrintInfoLine("GL format", texture.GlFormat);
        }

        if (texture.GlInternalFormat is not null)
        {
            PrintInfoLine("GL internal format", texture.GlInternalFormat);
        }

        if (info.SupercompressionScheme is not null)
        {
            PrintInfoLine("Supercompression", info.SupercompressionScheme);
        }

        if (info.KeyValueBytes != 0)
        {
            PrintInfoLine("Key/value bytes", FormatInvariant(info.KeyValueBytes));
        }

        if (info.SupercompressionGlobalDataBytes != 0)
        {
            PrintInfoLine("Supercompression global data bytes", FormatInvariant(info.SupercompressionGlobalDataBytes));
        }

        if (printSubresources)
        {
            PrintSubresources(texture.Subresources, texture.FaceCount);
        }
    }

    private static void PrintPvrInfo(PvrTexture texture, PvrInfo info, long fileBytes, bool printSubresources)
    {
        PrintTextureInfo(TextureContainer.Pvr, texture.Format, texture.Width, texture.Height, texture.MipLevelCount, texture.ArrayLayerCount, texture.FaceCount, GetPayloadByteCount(texture.Subresources), fileBytes);
        PrintInfoLine("Version", info.Version);
        if (info.PixelFormat is not null)
        {
            PrintInfoLine("PVR pixel format", $"0x{info.PixelFormat.Value:x16}");
        }

        if (info.ColourSpace is not null)
        {
            PrintInfoLine("Colour space", FormatPvrColourSpace(info.ColourSpace.Value));
        }

        if (info.ChannelType is not null)
        {
            PrintInfoLine("Channel type", FormatInvariant(info.ChannelType.Value));
        }

        if (info.LegacyPixelType is not null)
        {
            PrintInfoLine("Legacy pixel type", $"0x{info.LegacyPixelType.Value:x2}");
        }

        if (info.LegacyBitCount is not null)
        {
            PrintInfoLine("Legacy bit count", FormatInvariant(info.LegacyBitCount.Value));
        }

        if (texture.Metadata.Count != 0)
        {
            PrintInfoLine("Metadata entries", FormatInvariant(texture.Metadata.Count));
        }

        if (info.MetadataBytes != 0)
        {
            PrintInfoLine("Metadata bytes", FormatInvariant(info.MetadataBytes));
        }

        if (printSubresources)
        {
            PrintSubresources(texture.Subresources, texture.FaceCount);
        }
    }

    private static void PrintAstcInfo(AstcTexture texture, long fileBytes)
    {
        PrintTextureInfo(
            TextureContainer.Astc,
            texture.Format,
            texture.Width,
            texture.Height,
            mipLevelCount: 1,
            arrayLayerCount: 1,
            faceCount: 1,
            payloadBytes: texture.Payload.Length,
            fileBytes);
    }

    private static void PrintTextureInfo(
        TextureContainer container,
        TextureFormat format,
        int width,
        int height,
        IReadOnlyList<TextureMipLevel> mipLevels,
        long fileBytes) =>
        PrintTextureInfo(container, format, width, height, mipLevels.Count, arrayLayerCount: 1, faceCount: 1, GetPayloadByteCount(mipLevels), fileBytes);

    private static void PrintTextureInfo(
        TextureContainer container,
        TextureFormat format,
        int width,
        int height,
        int mipLevelCount,
        int arrayLayerCount,
        int faceCount,
        long payloadBytes,
        long fileBytes)
    {
        PrintInfoLine("Container", FormatContainer(container));
        PrintInfoLine("Format", FormatTextureFormat(format));
        PrintInfoLine("Kind", format.Kind);
        PrintInfoLine("Value kind", format.ValueKind);
        PrintInfoLine("Size", FormatSize(width, height));
        PrintInfoLine("Mip levels", FormatInvariant(mipLevelCount));
        if (arrayLayerCount > 1)
        {
            PrintInfoLine("Array layers", FormatInvariant(arrayLayerCount));
        }

        if (faceCount > 1)
        {
            PrintInfoLine("Faces", FormatInvariant(faceCount));
        }

        PrintInfoLine("Payload bytes", FormatInvariant(payloadBytes));
        PrintInfoLine("File bytes", FormatInvariant(fileBytes));

        if (format.IsCompressed)
        {
            PrintInfoLine("Block", FormatSize(format.BlockWidth, format.BlockHeight));
            PrintInfoLine("Bits per block", FormatInvariant(format.BitsPerBlock));
        }
        else
        {
            PrintInfoLine("Bits per texel", FormatInvariant(format.BitsPerTexel));
        }
    }

    private static void PrintSubresources(IReadOnlyList<TextureSubresource> subresources, int faceCount)
    {
        Console.WriteLine("Subresources:");
        foreach (var subresource in subresources)
        {
            var face = faceCount == 6
                ? ((TextureCubeFace)subresource.FaceIndex).ToString()
                : FormatInvariant(subresource.FaceIndex);
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"  mip={subresource.MipLevel} layer={subresource.ArrayLayer} face={face} size={subresource.Width}x{subresource.Height} payload={subresource.Payload.Length}"));
        }
    }

    private static KtxInfo ReadKtxInfo(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> identifier = stackalloc byte[12];
        stream.ReadExactly(identifier);

        if (IsKtxIdentifier(identifier, majorVersionByte: 0x31, minorVersionByte: 0x31))
        {
            Span<byte> header = stackalloc byte[52];
            stream.ReadExactly(header);
            return new KtxInfo(
                Version: 1,
                SupercompressionScheme: null,
                KeyValueBytes: BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(48, 4)),
                SupercompressionGlobalDataBytes: 0);
        }

        if (IsKtxIdentifier(identifier, majorVersionByte: 0x32, minorVersionByte: 0x30))
        {
            Span<byte> header = stackalloc byte[68];
            stream.ReadExactly(header);
            return new KtxInfo(
                Version: 2,
                (KtxSupercompressionScheme)BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(32, 4)),
                BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(48, 4)),
                BinaryPrimitives.ReadUInt64LittleEndian(header.Slice(60, 8)));
        }

        throw new InvalidDataException("The stream is not a KTX file.");
    }

    private static PvrInfo ReadPvrInfo(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> firstWordBuffer = stackalloc byte[4];
        stream.ReadExactly(firstWordBuffer);
        var firstWord = BinaryPrimitives.ReadUInt32LittleEndian(firstWordBuffer);

        if (firstWord == 0x03525650)
        {
            Span<byte> header = stackalloc byte[52];
            firstWordBuffer.CopyTo(header);
            stream.ReadExactly(header[4..]);
            return new PvrInfo(
                Version: 3,
                PixelFormat: BinaryPrimitives.ReadUInt64LittleEndian(header.Slice(8, 8)),
                ColourSpace: BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(16, 4)),
                ChannelType: BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(20, 4)),
                MetadataBytes: BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(48, 4)),
                LegacyPixelType: null,
                LegacyBitCount: null);
        }

        if (firstWord is 44 or 52)
        {
            Span<byte> header = stackalloc byte[52];
            firstWordBuffer.CopyTo(header);
            stream.ReadExactly(header.Slice(4, checked((int)firstWord - 4)));
            return new PvrInfo(
                Version: firstWord == 44 ? 1 : 2,
                PixelFormat: null,
                ColourSpace: null,
                ChannelType: null,
                MetadataBytes: 0,
                LegacyPixelType: BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(16, 4)) & 0xff,
                LegacyBitCount: BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(24, 4)));
        }

        throw new InvalidDataException("The stream is not a supported PVR file.");
    }

    private static bool IsKtxIdentifier(ReadOnlySpan<byte> identifier, byte majorVersionByte, byte minorVersionByte) =>
        identifier.Length == 12
        && identifier[0] == 0xab
        && identifier[1] == 0x4b
        && identifier[2] == 0x54
        && identifier[3] == 0x58
        && identifier[4] == 0x20
        && identifier[5] == majorVersionByte
        && identifier[6] == minorVersionByte
        && identifier[7] == 0xbb
        && identifier[8] == 0x0d
        && identifier[9] == 0x0a
        && identifier[10] == 0x1a
        && identifier[11] == 0x0a;

    private static long GetPayloadByteCount(IReadOnlyList<TextureMipLevel> mipLevels)
    {
        long byteCount = 0;
        foreach (var mipLevel in mipLevels)
        {
            byteCount = checked(byteCount + mipLevel.Payload.Length);
        }

        return byteCount;
    }

    private static long GetPayloadByteCount(IReadOnlyList<TextureSubresource> subresources)
    {
        long byteCount = 0;
        foreach (var subresource in subresources)
        {
            byteCount = checked(byteCount + subresource.Payload.Length);
        }

        return byteCount;
    }

    private static string FormatContainer(TextureContainer container) =>
        container switch
        {
            TextureContainer.Png => "PNG",
            TextureContainer.Jpeg => "JPEG",
            TextureContainer.Gif => "GIF",
            TextureContainer.Dds => "DDS",
            TextureContainer.Ktx => "KTX",
            TextureContainer.Pvr => "PVR",
            TextureContainer.Astc => "ASTC",
            _ => container.ToString()
        };

    private static string FormatTextureFormat(TextureFormat format)
    {
        var fieldName = TextureFormatCatalog.GetFieldName(format);
        return string.Equals(fieldName, format.Name, StringComparison.Ordinal)
            ? fieldName
            : $"{fieldName} ({format.Name})";
    }

    private static string FormatPvrColourSpace(uint colourSpace) =>
        colourSpace switch
        {
            0 => "Linear (0)",
            1 => "sRGB (1)",
            _ => FormatInvariant(colourSpace)
        };

    private static string FormatSize(int width, int height) =>
        string.Create(CultureInfo.InvariantCulture, $"{width}x{height}");

    private static string FormatInvariant(long value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string FormatInvariant(ulong value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static void PrintInfoLine(string label, object? value) =>
        Console.WriteLine($"{label}: {value}");

    private static bool IsStructuredTextureContainer(TextureContainer container) =>
        container is TextureContainer.Dds or TextureContainer.Ktx or TextureContainer.Pvr;

    private static TexturePayload ReadStructuredTexture(string path, TextureContainer container) =>
        container switch
        {
            TextureContainer.Dds => FromTexture(DdsCodec.Read(path)),
            TextureContainer.Ktx => FromTexture(KtxCodec.Read(path)),
            TextureContainer.Pvr => FromTexture(PvrCodec.Read(path)),
            _ => throw new NotSupportedException($"'{FormatContainer(container)}' is not a structured texture container.")
        };

    private static TexturePayload FromTexture(DdsTexture texture) =>
        new(texture.Format, texture.Subresources, texture.MipLevelCount, texture.ArrayLayerCount, texture.FaceCount);

    private static TexturePayload FromTexture(KtxTexture texture) =>
        new(texture.Format, texture.Subresources, texture.MipLevelCount, texture.ArrayLayerCount, texture.FaceCount);

    private static TexturePayload FromTexture(PvrTexture texture) =>
        new(texture.Format, texture.Subresources, texture.MipLevelCount, texture.ArrayLayerCount, texture.FaceCount);

    private static TexturePayload CreateAssembledTexture(
        TextureFormat format,
        ImageColorSpaces colorSpaces,
        IReadOnlyList<FileInfo> layerFiles,
        IReadOnlyList<FileInfo> cubeFiles,
        IReadOnlyList<FileInfo> mipFiles)
    {
        if (layerFiles.Count != 0)
        {
            return CreateArrayLayerTexture(format, DecodeImageFiles(layerFiles, colorSpaces));
        }

        if (cubeFiles.Count != 0)
        {
            return CreateCubeTexture(format, DecodeImageFiles(cubeFiles, colorSpaces));
        }

        if (mipFiles.Count != 0)
        {
            return CreateMipChainTexture(format, DecodeImageFiles(mipFiles, colorSpaces));
        }

        throw new ArgumentException("Specify exactly one of --layers, --cube, or --mips.");
    }

    private static TexturePayload CreateArrayLayerTexture(TextureFormat format, IReadOnlyList<ArrayBitmap<Rgba8UNorm>> images)
    {
        EnsureImageCount(images, minimumCount: 1, "--layers");
        EnsureSameDimensions(images, "--layers");

        var subresources = new TextureSubresource[images.Count];
        for (var layer = 0; layer < images.Count; layer++)
        {
            var image = images[layer];
            subresources[layer] = EncodeSubresource(format, image, mipLevel: 0, arrayLayer: layer, faceIndex: 0);
        }

        return new TexturePayload(format, subresources, MipLevelCount: 1, ArrayLayerCount: images.Count, FaceCount: 1);
    }

    private static TexturePayload CreateCubeTexture(TextureFormat format, IReadOnlyList<ArrayBitmap<Rgba8UNorm>> images)
    {
        if (images.Count != 6)
        {
            throw new ArgumentException("--cube requires exactly six input images.");
        }

        EnsureSameDimensions(images, "--cube");
        if (images[0].Width != images[0].Height)
        {
            throw new ArgumentException("--cube input images must be square.");
        }

        var subresources = new TextureSubresource[images.Count];
        for (var face = 0; face < images.Count; face++)
        {
            var image = images[face];
            subresources[face] = EncodeSubresource(format, image, mipLevel: 0, arrayLayer: 0, face);
        }

        return new TexturePayload(format, subresources, MipLevelCount: 1, ArrayLayerCount: 1, FaceCount: 6);
    }

    private static TexturePayload CreateMipChainTexture(TextureFormat format, IReadOnlyList<ArrayBitmap<Rgba8UNorm>> images)
    {
        EnsureImageCount(images, minimumCount: 1, "--mips");
        var fullMipLevelCount = TextureMipLevel.GetFullMipLevelCount(images[0].Width, images[0].Height);
        if (images.Count > fullMipLevelCount)
        {
            throw new ArgumentException("--mips contains more images than the full mip chain for the base dimensions.");
        }

        var subresources = new TextureSubresource[images.Count];
        for (var mipLevel = 0; mipLevel < images.Count; mipLevel++)
        {
            var image = images[mipLevel];
            var expectedWidth = TextureMipLevel.GetDimension(images[0].Width, mipLevel);
            var expectedHeight = TextureMipLevel.GetDimension(images[0].Height, mipLevel);
            if (image.Width != expectedWidth || image.Height != expectedHeight)
            {
                throw new ArgumentException(
                    $"--mips image {mipLevel} is {image.Width}x{image.Height}, but {expectedWidth}x{expectedHeight} was expected.");
            }

            subresources[mipLevel] = EncodeSubresource(format, image, mipLevel, arrayLayer: 0, faceIndex: 0);
        }

        return new TexturePayload(format, subresources, images.Count, ArrayLayerCount: 1, FaceCount: 1);
    }

    private static ArrayBitmap<Rgba8UNorm>[] DecodeImageFiles(IReadOnlyList<FileInfo> files, ImageColorSpaces colorSpaces)
    {
        var images = new ArrayBitmap<Rgba8UNorm>[files.Count];
        for (var i = 0; i < files.Count; i++)
        {
            images[i] = DecodeAssembleImage(files[i], colorSpaces);
        }

        return images;
    }

    private static ArrayBitmap<Rgba8UNorm> DecodeAssembleImage(FileInfo file, ImageColorSpaces colorSpaces)
    {
        var path = file.FullName;
        var container = GetContainer(path);
        if (container is not (TextureContainer.Png or TextureContainer.Jpeg or TextureContainer.Gif))
        {
            throw new NotSupportedException("Assemble inputs must be PNG, JPEG, or GIF images.");
        }

        return Decode(path, colorSpaces);
    }

    private static TextureSubresource EncodeSubresource(
        TextureFormat format,
        IBitmap<Rgba8UNorm> image,
        int mipLevel,
        int arrayLayer,
        int faceIndex)
    {
        var coder = TextureCoderManager.Global.GetCoder(format);
        var payload = new byte[coder.GetEncodedByteCount(image.Width, image.Height)];
        coder.Encode(image.AsView(), payload);
        return new TextureSubresource(mipLevel, arrayLayer, faceIndex, image.Width, image.Height, payload);
    }

    private static void EnsureImageCount(IReadOnlyList<ArrayBitmap<Rgba8UNorm>> images, int minimumCount, string optionName)
    {
        if (images.Count < minimumCount)
        {
            throw new ArgumentException($"{optionName} requires at least {minimumCount} input image(s).");
        }
    }

    private static void EnsureSameDimensions(IReadOnlyList<ArrayBitmap<Rgba8UNorm>> images, string optionName)
    {
        var width = images[0].Width;
        var height = images[0].Height;
        for (var i = 1; i < images.Count; i++)
        {
            if (images[i].Width != width || images[i].Height != height)
            {
                throw new ArgumentException(
                    $"{optionName} image {i} is {images[i].Width}x{images[i].Height}, but {width}x{height} was expected.");
            }
        }
    }

    private static void WriteStructuredTexture(
        TexturePayload texture,
        string path,
        TextureContainer container,
        TextureFormat format,
        int ktxVersion,
        TextureCompressionLevel? quality)
    {
        using var compressionRegistration = CreateTextureCompressionRegistration(format, quality);
        var output = texture.Format == format && quality is null
            ? texture
            : ReencodeStructuredTexture(texture, format);

        switch (container)
        {
            case TextureContainer.Dds:
                DdsCodec.Write(
                    new DdsTexture(output.Format, output.Subresources, output.ArrayLayerCount, output.FaceCount),
                    path);
                break;
            case TextureContainer.Ktx:
                KtxCodec.Write(
                    new KtxTexture(output.Format, output.Subresources, output.ArrayLayerCount, output.FaceCount),
                    path,
                    new KtxEncodingOptions { Version = ktxVersion == 2 ? KtxVersion.Version2 : KtxVersion.Version1 });
                break;
            case TextureContainer.Pvr:
                PvrCodec.Write(
                    new PvrTexture(output.Format, output.Subresources, output.ArrayLayerCount, output.FaceCount),
                    path);
                break;
            default:
                throw new NotSupportedException($"Unsupported structured texture output container '{container}'.");
        }
    }

    private static TexturePayload ReencodeStructuredTexture(TexturePayload texture, TextureFormat format)
    {
        var sourceCoder = TextureCoderManager.Global.GetCoder(texture.Format);
        var targetCoder = TextureCoderManager.Global.GetCoder(format);
        var subresources = new TextureSubresource[texture.Subresources.Count];
        for (var i = 0; i < texture.Subresources.Count; i++)
        {
            var source = texture.Subresources[i];
            var bitmap = new ArrayBitmap<Rgba8UNorm>(source.Width, source.Height);
            sourceCoder.Decode(source.Payload, bitmap.AsView());

            var payload = new byte[targetCoder.GetEncodedByteCount(source.Width, source.Height)];
            targetCoder.Encode(bitmap.AsView(), payload);
            subresources[i] = new TextureSubresource(
                source.MipLevel,
                source.ArrayLayer,
                source.FaceIndex,
                source.Width,
                source.Height,
                payload);
        }

        return new TexturePayload(format, subresources, texture.MipLevelCount, texture.ArrayLayerCount, texture.FaceCount);
    }

    private static ArrayBitmap<Rgba8UNorm> Decode(
        string path,
        ImageColorSpaces? imageColorSpaces = null,
        TextureSubresourceSelection selection = default)
    {
        var container = GetContainer(path);
        var bitmap = container switch
        {
            TextureContainer.Png => DecodeImage(PngCodec.Decode(path), selection),
            TextureContainer.Jpeg => DecodeImage(JpegCodec.Decode(path), selection),
            TextureContainer.Gif => DecodeImage(GifCodec.Decode(path), selection),
            TextureContainer.Dds => DecodeTexture(DdsCodec.Read(path), selection),
            TextureContainer.Ktx => DecodeTexture(KtxCodec.Read(path), selection),
            TextureContainer.Pvr => DecodeTexture(PvrCodec.Read(path), selection),
            TextureContainer.Astc => DecodeAstc(AstcCodec.Read(path), selection),
            _ => throw new NotSupportedException($"Unsupported input extension '{Path.GetExtension(path)}'.")
        };

        return ApplyInputImageColorSpace(bitmap, container, GetImageColorSpace(container, imageColorSpaces));
    }

    private static ArrayBitmap<Rgba8UNorm> DecodeImage(
        ArrayBitmap<Rgba8UNorm> bitmap,
        TextureSubresourceSelection selection)
    {
        EnsureDefaultSelection(selection, "Image containers");
        return bitmap;
    }

    private static ArrayBitmap<Rgba8UNorm> DecodeAstc(AstcTexture texture, TextureSubresourceSelection selection)
    {
        EnsureDefaultSelection(selection, "ASTC files");
        return AstcCodec.Decode<Rgba8UNorm>(texture);
    }

    private static ArrayBitmap<Rgba8UNorm> DecodeTexture(DdsTexture texture, TextureSubresourceSelection selection)
    {
        var subresource = GetSubresource(texture, selection);
        return DecodeSubresource(texture.Format, subresource);
    }

    private static ArrayBitmap<Rgba8UNorm> DecodeTexture(KtxTexture texture, TextureSubresourceSelection selection)
    {
        var subresource = GetSubresource(texture, selection);
        return DecodeSubresource(texture.Format, subresource);
    }

    private static ArrayBitmap<Rgba8UNorm> DecodeTexture(PvrTexture texture, TextureSubresourceSelection selection)
    {
        var subresource = GetSubresource(texture, selection);
        return DecodeSubresource(texture.Format, subresource);
    }

    private static TextureSubresource GetSubresource(DdsTexture texture, TextureSubresourceSelection selection)
    {
        ValidateSelection(texture.MipLevelCount, texture.ArrayLayerCount, texture.FaceCount, selection);
        return texture.GetSubresource(selection.MipLevel, selection.ArrayLayer, selection.FaceIndex);
    }

    private static TextureSubresource GetSubresource(KtxTexture texture, TextureSubresourceSelection selection)
    {
        ValidateSelection(texture.MipLevelCount, texture.ArrayLayerCount, texture.FaceCount, selection);
        return texture.GetSubresource(selection.MipLevel, selection.ArrayLayer, selection.FaceIndex);
    }

    private static TextureSubresource GetSubresource(PvrTexture texture, TextureSubresourceSelection selection)
    {
        ValidateSelection(texture.MipLevelCount, texture.ArrayLayerCount, texture.FaceCount, selection);
        return texture.GetSubresource(selection.MipLevel, selection.ArrayLayer, selection.FaceIndex);
    }

    private static ArrayBitmap<Rgba8UNorm> DecodeSubresource(TextureFormat format, TextureSubresource subresource)
    {
        var bitmap = new ArrayBitmap<Rgba8UNorm>(subresource.Width, subresource.Height);
        var coder = TextureCoderManager.Global.GetCoder(format);
        coder.Decode(subresource.Payload, bitmap.AsView());
        return bitmap;
    }

    private static void ValidateSelection(
        int mipLevelCount,
        int arrayLayerCount,
        int faceCount,
        TextureSubresourceSelection selection)
    {
        if (selection.MipLevel >= mipLevelCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(selection),
                $"Mip level {selection.MipLevel} is outside the texture mip level count {mipLevelCount}.");
        }

        if (selection.ArrayLayer >= arrayLayerCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(selection),
                $"Array layer {selection.ArrayLayer} is outside the texture array layer count {arrayLayerCount}.");
        }

        if (selection.HasFace && faceCount != 6)
        {
            throw new ArgumentOutOfRangeException(nameof(selection), "Face selection requires a cube-map texture.");
        }

        if (selection.FaceIndex >= faceCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(selection),
                $"Face index {selection.FaceIndex} is outside the texture face count {faceCount}.");
        }
    }

    private static void EnsureDefaultSelection(TextureSubresourceSelection selection, string containerDescription)
    {
        if (!selection.IsDefault)
        {
            throw new NotSupportedException($"{containerDescription} do not support subresource selection.");
        }
    }

    private static void Encode(
        IBitmap<Rgba8UNorm> bitmap,
        string path,
        TextureContainer container,
        TextureFormat format,
        int ktxVersion,
        int jpegQuality,
        TextureCompressionLevel? quality,
        MipmapMode mipmaps,
        ImageColorSpaces? imageColorSpaces)
    {
        var imageColorSpace = GetImageColorSpace(container, imageColorSpaces);
        if (imageColorSpace == ImageColorSpace.Srgb)
        {
            EnsureImageColorSpaceContainer(container);
        }

        using var compressionRegistration = CreateTextureCompressionRegistration(format, quality);

        switch (container)
        {
            case TextureContainer.Png:
                EnsureNoMipmaps(mipmaps, container);
                PngCodec.Encode(ApplyOutputImageColorSpace(bitmap, container, imageColorSpace), path);
                break;
            case TextureContainer.Jpeg:
                EnsureNoMipmaps(mipmaps, container);
                JpegCodec.Encode(ApplyOutputImageColorSpace(bitmap, container, imageColorSpace), path, new JpegEncodingOptions { Quality = jpegQuality });
                break;
            case TextureContainer.Gif:
                EnsureNoMipmaps(mipmaps, container);
                GifCodec.Encode(ApplyOutputImageColorSpace(bitmap, container, imageColorSpace), path);
                break;
            case TextureContainer.Dds:
                if (mipmaps == MipmapMode.Generate)
                {
                    DdsCodec.EncodeMipChain(BitmapMipChain.Generate(bitmap), path, new DdsEncodingOptions { TextureFormat = format });
                }
                else
                {
                    DdsCodec.Encode(bitmap, path, new DdsEncodingOptions { TextureFormat = format });
                }

                break;
            case TextureContainer.Ktx:
                var ktxOptions = new KtxEncodingOptions
                {
                    TextureFormat = format,
                    Version = ktxVersion == 2 ? KtxVersion.Version2 : KtxVersion.Version1
                };
                if (mipmaps == MipmapMode.Generate)
                {
                    KtxCodec.EncodeMipChain(BitmapMipChain.Generate(bitmap), path, ktxOptions);
                }
                else
                {
                    KtxCodec.Encode(bitmap, path, ktxOptions);
                }

                break;
            case TextureContainer.Pvr:
                if (mipmaps == MipmapMode.Generate)
                {
                    PvrCodec.EncodeMipChain(BitmapMipChain.Generate(bitmap), path, new PvrEncodingOptions { TextureFormat = format });
                }
                else
                {
                    PvrCodec.Encode(bitmap, path, new PvrEncodingOptions { TextureFormat = format });
                }

                break;
            case TextureContainer.Astc:
                EnsureNoMipmaps(mipmaps, container);
                AstcCodec.Encode(bitmap, path, new AstcEncodingOptions { TextureFormat = format });
                break;
            default:
                throw new NotSupportedException($"Unsupported output container '{container}'.");
        }
    }

    private static void EnsureNoMipmaps(MipmapMode mipmaps, TextureContainer container)
    {
        if (mipmaps != MipmapMode.None)
        {
            throw new NotSupportedException($"Mip-map generation is not supported for '{container}' output.");
        }
    }

    private static IDisposable? CreateTextureCompressionRegistration(TextureFormat format, TextureCompressionLevel? quality)
    {
        if (quality is null)
        {
            return null;
        }

        var options = new TextureCompressionOptions { CompressionMode = quality.Value };
        if (S3tcTextureCoder.IsSupported(format))
        {
            return TextureCoderManager.Global.Register(format, new S3tcTextureCoder(format, options));
        }

        if (FxtcTextureCoder.IsSupported(format))
        {
            return TextureCoderManager.Global.Register(format, new FxtcTextureCoder(format, options));
        }

        if (EtcTextureCoder.IsSupported(format))
        {
            return TextureCoderManager.Global.Register(format, new EtcTextureCoder(format, options));
        }

        if (AtcTextureCoder.IsSupported(format))
        {
            return TextureCoderManager.Global.Register(format, new AtcTextureCoder(format, options));
        }

        if (RgtcLatcTextureCoder.IsSupported(format))
        {
            return TextureCoderManager.Global.Register(format, new RgtcLatcTextureCoder(format, options));
        }

        if (BptcTextureCoder.IsSupported(format))
        {
            return TextureCoderManager.Global.Register(format, new BptcTextureCoder(format, options));
        }

        if (PvrtcTextureCoder.IsSupported(format))
        {
            return TextureCoderManager.Global.Register(format, new PvrtcTextureCoder(format, options));
        }

        return AstcTextureCoder.IsSupported(format)
            ? TextureCoderManager.Global.Register(format, new AstcTextureCoder(format, options))
            : null;
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

internal enum MipmapMode
{
    None,
    Generate
}

internal readonly record struct TextureSubresourceSelection(int MipLevel, int ArrayLayer, TextureCubeFace? Face)
{
    public int FaceIndex => Face is { } face ? (int)face : 0;

    public bool HasFace => Face is not null;

    public bool IsDefault => MipLevel == 0 && ArrayLayer == 0 && Face is null;
}

internal sealed record TexturePayload(
    TextureFormat Format,
    IReadOnlyList<TextureSubresource> Subresources,
    int MipLevelCount,
    int ArrayLayerCount,
    int FaceCount)
{
    public TextureSubresource GetSubresource(TextureSubresourceSelection selection)
    {
        if (selection.MipLevel >= MipLevelCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(selection),
                $"Mip level {selection.MipLevel} is outside the texture mip level count {MipLevelCount}.");
        }

        if (selection.ArrayLayer >= ArrayLayerCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(selection),
                $"Array layer {selection.ArrayLayer} is outside the texture array layer count {ArrayLayerCount}.");
        }

        if (selection.HasFace && FaceCount != 6)
        {
            throw new ArgumentOutOfRangeException(nameof(selection), "Face selection requires a cube-map texture.");
        }

        if (selection.FaceIndex >= FaceCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(selection),
                $"Face index {selection.FaceIndex} is outside the texture face count {FaceCount}.");
        }

        return Subresources[checked((((selection.ArrayLayer * FaceCount) + selection.FaceIndex) * MipLevelCount) + selection.MipLevel)];
    }
}

internal sealed record ImageColorSpaces(ImageColorSpace Png, ImageColorSpace Jpeg, ImageColorSpace Gif)
{
    public static ImageColorSpaces Default { get; } = new(
        ImageColorSpace.Linear,
        ImageColorSpace.Linear,
        ImageColorSpace.Linear);
}

internal sealed record KtxInfo(
    int Version,
    KtxSupercompressionScheme? SupercompressionScheme,
    uint KeyValueBytes,
    ulong SupercompressionGlobalDataBytes);

internal sealed record PvrInfo(
    int Version,
    ulong? PixelFormat,
    uint? ColourSpace,
    uint? ChannelType,
    uint MetadataBytes,
    uint? LegacyPixelType,
    uint? LegacyBitCount);

internal sealed record FormatEntry(string FieldName, TextureFormat Format);
