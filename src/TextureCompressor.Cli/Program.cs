using System.CommandLine;
using System.Buffers.Binary;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using TextureCompressor.Analysis;
using TextureCompressor.Bitmaps;
using TextureCompressor.Colors;
using TextureCompressor.Codecs;
using TextureCompressor.Conversion;
using TextureCompressor.FileFormats.Astc;
using TextureCompressor.FileFormats.Dds;
using TextureCompressor.FileFormats.Gif;
using TextureCompressor.FileFormats.Hdr;
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
        root.Subcommands.Add(CreateExtractCommand());
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
        var jsonOption = new Option<bool>("--json")
        {
            Description = "Print conversion result and optional metrics as JSON."
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
        command.Options.Add(jsonOption);
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
            var printJson = parseResult.GetValue(jsonOption);

            var inputKind = GetContainer(inputPath);
            if (IsStructuredTextureContainer(inputKind) && IsStructuredTextureContainer(outputKind))
            {
                var texture = ReadStructuredTexture(inputPath, inputKind);
                var format = formatWasSpecified ? requestedFormat : texture.Format;
                var extractor = new TextureExtractor();
                if (!hasSubresourceSelection && mipmaps == MipmapMode.None)
                {
                    WriteStructuredTexture(texture, outputPath, outputKind, format, ktxVersion, quality);
                    BitmapQualityResult? structuredMetrics = null;
                    if (printMetrics)
                    {
                        var decoded = Decode(outputPath, colorSpaces);
                        structuredMetrics = BitmapQuality.Compare(extractor.Decode(texture), decoded);
                    }

                    PrintConvertResult(outputPath, structuredMetrics, printJson);
                    return 0;
                }

                var selectedSource = extractor.Decode(texture, selection);
                Encode(selectedSource, outputPath, outputKind, format, ktxVersion, jpegQuality, quality, mipmaps, colorSpaces);
                BitmapQualityResult? selectedMetrics = null;
                if (printMetrics)
                {
                    var decoded = Decode(outputPath, colorSpaces);
                    selectedMetrics = BitmapQuality.Compare(selectedSource, decoded);
                }

                PrintConvertResult(outputPath, selectedMetrics, printJson);
                return 0;
            }

            var source = Decode(inputPath, colorSpaces, selection);
            Encode(source, outputPath, outputKind, requestedFormat, ktxVersion, jpegQuality, quality, mipmaps, colorSpaces);
            BitmapQualityResult? imageMetrics = null;
            if (printMetrics)
            {
                var decoded = Decode(outputPath, colorSpaces);
                imageMetrics = BitmapQuality.Compare(source, decoded);
            }

            PrintConvertResult(outputPath, imageMetrics, printJson);
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
        var jsonOption = new Option<bool>("--json")
        {
            Description = "Print matching formats as JSON."
        };

        var command = new Command("formats", "List and search texture formats accepted by --format.");
        command.Arguments.Add(queryArgument);
        command.Options.Add(compressedOption);
        command.Options.Add(uncompressedOption);
        command.Options.Add(jsonOption);
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
            if (parseResult.GetValue(jsonOption))
            {
                PrintFormatsJson(query, compressed, uncompressed, formats);
                return 0;
            }

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
        var jsonOption = new Option<bool>("--json")
        {
            Description = "Print quality metrics as JSON."
        };

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
        command.Options.Add(jsonOption);
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
            PrintQuality(BitmapQuality.Compare(expected, actual, includeAlpha), parseResult.GetValue(jsonOption));

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
        var jsonOption = new Option<bool>("--json")
        {
            Description = "Print metadata as JSON."
        };

        var command = new Command(name, description);
        command.Arguments.Add(inputArgument);
        command.Options.Add(subresourcesOption);
        command.Options.Add(jsonOption);
        command.SetAction(parseResult => RunCommand(() =>
        {
            var inputPath = RequireFile(parseResult.GetValue(inputArgument), "input").FullName;
            PrintInfo(inputPath, parseResult.GetValue(subresourcesOption), parseResult.GetValue(jsonOption));
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
        var manifestOption = new Option<FileInfo?>("--manifest")
        {
            Description = "Extract manifest.json to rebuild a full texture topology."
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
        command.Options.Add(manifestOption);
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

            if (result.GetResult(manifestOption) is { Implicit: false })
            {
                modeCount++;
            }

            if (modeCount != 1)
            {
                result.AddError("Specify exactly one of --layers, --cube, --mips, or --manifest.");
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

            var ktxVersion = IsOptionExplicit(parseResult, ktxVersionOption)
                ? parseResult.GetValue(ktxVersionOption)
                : GetDefaultKtxVersion(outputPath);
            var colorSpaces = new ImageColorSpaces(
                parseResult.GetValue(pngColorSpaceOption),
                parseResult.GetValue(jpgColorSpaceOption),
                parseResult.GetValue(gifColorSpaceOption));
            var quality = parseResult.GetValue(qualityOption);
            var manifestFile = parseResult.GetValue(manifestOption);
            var manifest = manifestFile is null
                ? null
                : ReadExtractManifest(RequireFile(manifestFile, "--manifest").FullName);
            var format = ResolveAssembleFormat(parseResult, formatOption, manifest);

            using var compressionRegistration = CreateTextureCompressionRegistration(format, quality);
            var texture = manifest is null
                ? CreateAssembledTexture(
                    format,
                    colorSpaces,
                    parseResult.GetValue(layersOption) ?? [],
                    parseResult.GetValue(cubeOption) ?? [],
                    parseResult.GetValue(mipsOption) ?? [])
                : CreateManifestTexture(format, colorSpaces, manifestFile!.FullName, manifest);
            WriteStructuredTexture(texture, outputPath, outputKind, format, ktxVersion, quality: null);
            Console.WriteLine($"wrote {outputPath}");
            return 0;
        }));

        return command;
    }

    private static Command CreateExtractCommand()
    {
        var inputArgument = new Argument<FileInfo>("input")
        {
            Description = "Input DDS, KTX, or PVR texture file."
        };
        var outputDirectoryArgument = new Argument<DirectoryInfo>("output-directory")
        {
            Description = "Directory where extracted images are written."
        };
        var containerOption = new Option<TextureContainer>("--container", "-c")
        {
            Description = "Output image container. Supported values are Png, Jpeg, Gif, and Hdr.",
            DefaultValueFactory = _ => TextureContainer.Png
        };
        containerOption.Validators.Add(result =>
        {
            var container = result.GetValueOrDefault<TextureContainer>();
            if (!IsImageContainer(container))
            {
                result.AddError("--container for extract must be Png, Jpeg, Gif, or Hdr.");
            }
        });
        var patternOption = new Option<string>("--pattern")
        {
            Description = "Output file name pattern without extension. Supports {mip}, {layer}, {face}, {faceIndex}, {width}, and {height}.",
            DefaultValueFactory = _ => "mip{mip}_layer{layer}_face{face}"
        };
        var manifestOption = new Option<bool>("--manifest")
        {
            Description = "Write manifest.json with extracted subresource metadata."
        };
        var pngColorSpaceOption = new Option<ImageColorSpace>("--png-color-space")
        {
            Description = "How to write PNG RGB values.",
            DefaultValueFactory = _ => ImageColorSpace.Linear
        };
        var jpgColorSpaceOption = new Option<ImageColorSpace>("--jpg-color-space")
        {
            Description = "How to write JPEG RGB values.",
            DefaultValueFactory = _ => ImageColorSpace.Linear
        };
        var gifColorSpaceOption = new Option<ImageColorSpace>("--gif-color-space")
        {
            Description = "How to write GIF RGB values.",
            DefaultValueFactory = _ => ImageColorSpace.Linear
        };
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
        var mipOption = CreateOptionalIndexOption("--mip", "Only extract this mip level.");
        var layerOption = CreateOptionalIndexOption("--layer", "Only extract this array layer.");
        var faceOption = CreateFaceOption("--face", "Only extract this cube-map face.");

        var command = new Command("extract", "Extract DDS, KTX, or PVR subresources into image files.");
        command.Arguments.Add(inputArgument);
        command.Arguments.Add(outputDirectoryArgument);
        command.Options.Add(containerOption);
        command.Options.Add(patternOption);
        command.Options.Add(manifestOption);
        command.Options.Add(pngColorSpaceOption);
        command.Options.Add(jpgColorSpaceOption);
        command.Options.Add(gifColorSpaceOption);
        command.Options.Add(jpegQualityOption);
        command.Options.Add(mipOption);
        command.Options.Add(layerOption);
        command.Options.Add(faceOption);
        command.SetAction(parseResult => RunCommand(() =>
        {
            var inputPath = RequireFile(parseResult.GetValue(inputArgument), "input").FullName;
            var outputDirectory = RequireDirectory(parseResult.GetValue(outputDirectoryArgument), "output-directory").FullName;
            var inputKind = GetContainer(inputPath);
            if (!IsStructuredTextureContainer(inputKind))
            {
                throw new NotSupportedException("Extract input must be DDS, KTX, or PVR.");
            }

            var imageKind = parseResult.GetValue(containerOption);
            var pattern = parseResult.GetValue(patternOption) ?? "mip{mip}_layer{layer}_face{face}";
            var jpegQuality = parseResult.GetValue(jpegQualityOption);
            var colorSpaces = new ImageColorSpaces(
                parseResult.GetValue(pngColorSpaceOption),
                parseResult.GetValue(jpgColorSpaceOption),
                parseResult.GetValue(gifColorSpaceOption));
            var texture = ReadStructuredTexture(inputPath, inputKind);
            var extracted = new TextureExtractor().Extract(
                texture,
                new TextureSubresourceFilter(
                    parseResult.GetValue(mipOption),
                    parseResult.GetValue(layerOption),
                    parseResult.GetValue(faceOption)));

            Directory.CreateDirectory(outputDirectory);
            var manifestEntries = ExtractSubresources(
                extracted,
                texture.FaceCount,
                outputDirectory,
                imageKind,
                pattern,
                jpegQuality,
                colorSpaces);
            Console.WriteLine($"wrote {manifestEntries.Count} image(s) to {outputDirectory}");

            if (parseResult.GetValue(manifestOption))
            {
                var manifestPath = Path.Combine(outputDirectory, "manifest.json");
                WriteExtractManifest(inputPath, inputKind, texture, imageKind, manifestPath, manifestEntries);
                Console.WriteLine($"wrote {manifestPath}");
            }

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

    private static Option<int?> CreateOptionalIndexOption(string name, string description)
    {
        var option = new Option<int?>(name)
        {
            Description = description
        };
        option.Validators.Add(result =>
        {
            var value = result.GetValueOrDefault<int?>();
            if (value is < 0)
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

    private static DirectoryInfo RequireDirectory(DirectoryInfo? directory, string argumentName) =>
        directory ?? throw new ArgumentException($"Missing required argument '{argumentName}'.");

    private static bool IsOptionExplicit(ParseResult parseResult, Option option) =>
        parseResult.GetResult(option) is { Implicit: false };

    private static int GetDefaultKtxVersion(string outputPath) =>
        string.Equals(Path.GetExtension(outputPath), ".ktx2", StringComparison.OrdinalIgnoreCase) ? 2 : 1;

    private static void PrintInfo(string path, bool printSubresources, bool printJson)
    {
        if (printJson)
        {
            var json = JsonSerializer.Serialize(BuildInfoDocument(path, printSubresources), CreateJsonOptions(writeIndented: true));
            Console.WriteLine(json);
            return;
        }

        PrintTextInfo(path, printSubresources);
    }

    private static void PrintTextInfo(string path, bool printSubresources)
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
            case TextureContainer.Hdr:
                PrintImageInfo(container, HdrCodec.Decode(path), fileBytes, nameof(TextureFormats.Rgba32Float));
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

    private static InfoDocument BuildInfoDocument(string path, bool includeSubresources)
    {
        var fileBytes = new FileInfo(path).Length;
        var container = GetContainer(path);

        return container switch
        {
            TextureContainer.Png => BuildImageInfoDocument(container, PngCodec.Decode(path), fileBytes),
            TextureContainer.Jpeg => BuildImageInfoDocument(container, JpegCodec.Decode(path), fileBytes),
            TextureContainer.Gif => BuildImageInfoDocument(container, GifCodec.Decode(path), fileBytes),
            TextureContainer.Hdr => BuildImageInfoDocument(container, HdrCodec.Decode(path), fileBytes, nameof(TextureFormats.Rgba32Float)),
            TextureContainer.Dds => BuildDdsInfoDocument(DdsCodec.Read(path), fileBytes, includeSubresources),
            TextureContainer.Ktx => BuildKtxInfoDocument(KtxCodec.Read(path), ReadKtxInfo(path), fileBytes, includeSubresources),
            TextureContainer.Pvr => BuildPvrInfoDocument(PvrCodec.Read(path), ReadPvrInfo(path), fileBytes, includeSubresources),
            TextureContainer.Astc => BuildAstcInfoDocument(AstcCodec.Read(path), fileBytes),
            _ => throw new NotSupportedException($"Unsupported input extension '{Path.GetExtension(path)}'.")
        };
    }

    private static InfoDocument BuildImageInfoDocument<TPixel>(
        TextureContainer container,
        IBitmap<TPixel> bitmap,
        long fileBytes,
        string decodedFormat = nameof(TextureFormats.Rgba8UNorm))
        where TPixel : unmanaged, IPixel<TPixel> =>
        new(FormatContainer(container), bitmap.Width, bitmap.Height, fileBytes)
        {
            DecodedFormat = decodedFormat
        };

    private static InfoDocument BuildDdsInfoDocument(DdsTexture texture, long fileBytes, bool includeSubresources) =>
        BuildTextureInfoDocument(
            TextureContainer.Dds,
            texture.Texture.Format,
            texture.Texture.Width,
            texture.Texture.Height,
            texture.Texture.MipLevelCount,
            texture.Texture.ArrayLayerCount,
            texture.Texture.FaceCount,
            GetPayloadByteCount(texture.Texture.Subresources),
            fileBytes,
            includeSubresources ? BuildSubresourceInfo(texture.Texture.Subresources, texture.Texture.FaceCount) : null)
        with
        {
            Dds = new DdsInfoDocument(
                texture.HeaderKind.ToString(),
                texture.DxgiFormat?.ToString(),
                texture.DxgiFormat is null ? null : texture.AlphaMode.ToString(),
                texture.LegacyPixelFormat?.ToString())
        };

    private static InfoDocument BuildKtxInfoDocument(KtxTexture texture, KtxInfo info, long fileBytes, bool includeSubresources) =>
        BuildTextureInfoDocument(
            TextureContainer.Ktx,
            texture.Texture.Format,
            texture.Texture.Width,
            texture.Texture.Height,
            texture.Texture.MipLevelCount,
            texture.Texture.ArrayLayerCount,
            texture.Texture.FaceCount,
            GetPayloadByteCount(texture.Texture.Subresources),
            fileBytes,
            includeSubresources ? BuildSubresourceInfo(texture.Texture.Subresources, texture.Texture.FaceCount) : null)
        with
        {
            Ktx = new KtxInfoDocument(
                info.Version,
                texture.VkFormat?.ToString(),
                texture.GlType?.ToString(),
                texture.GlFormat?.ToString(),
                texture.GlInternalFormat?.ToString(),
                info.SupercompressionScheme?.ToString(),
                info.KeyValueBytes,
                info.SupercompressionGlobalDataBytes)
        };

    private static InfoDocument BuildPvrInfoDocument(PvrTexture texture, PvrInfo info, long fileBytes, bool includeSubresources) =>
        BuildTextureInfoDocument(
            TextureContainer.Pvr,
            texture.Texture.Format,
            texture.Texture.Width,
            texture.Texture.Height,
            texture.Texture.MipLevelCount,
            texture.Texture.ArrayLayerCount,
            texture.Texture.FaceCount,
            GetPayloadByteCount(texture.Texture.Subresources),
            fileBytes,
            includeSubresources ? BuildSubresourceInfo(texture.Texture.Subresources, texture.Texture.FaceCount) : null)
        with
        {
            Pvr = new PvrInfoDocument(
                info.Version,
                info.PixelFormat is { } pixelFormat ? $"0x{pixelFormat:x16}" : null,
                info.ColourSpace,
                info.ColourSpace is { } colourSpace ? FormatPvrColourSpace(colourSpace) : null,
                info.ChannelType,
                info.MetadataBytes,
                texture.Metadata.Count == 0 ? null : texture.Metadata.Count,
                info.LegacyPixelType is { } legacyPixelType ? $"0x{legacyPixelType:x2}" : null,
                info.LegacyBitCount)
        };

    private static InfoDocument BuildAstcInfoDocument(AstcTexture texture, long fileBytes) =>
        BuildTextureInfoDocument(
            TextureContainer.Astc,
            texture.Format,
            texture.Width,
            texture.Height,
            mipLevelCount: 1,
            arrayLayerCount: 1,
            faceCount: 1,
            payloadBytes: texture.Payload.Length,
            fileBytes,
            subresources: null);

    private static InfoDocument BuildTextureInfoDocument(
        TextureContainer container,
        TextureFormat format,
        int width,
        int height,
        int mipLevelCount,
        int arrayLayerCount,
        int faceCount,
        long payloadBytes,
        long fileBytes,
        IReadOnlyList<InfoSubresourceDocument>? subresources)
    {
        var document = new InfoDocument(FormatContainer(container), width, height, fileBytes)
        {
            Format = TextureFormatCatalog.GetFieldName(format),
            FormatName = format.Name,
            Kind = format.Kind.ToString(),
            ValueKind = format.ValueKind.ToString(),
            MipLevels = mipLevelCount,
            ArrayLayers = arrayLayerCount,
            Faces = faceCount,
            PayloadBytes = payloadBytes,
            Subresources = subresources
        };

        return format.IsCompressed
            ? document with
            {
                BlockWidth = format.BlockWidth,
                BlockHeight = format.BlockHeight,
                BitsPerBlock = format.BitsPerBlock
            }
            : document with
            {
                BitsPerTexel = format.BitsPerTexel
            };
    }

    private static IReadOnlyList<InfoSubresourceDocument> BuildSubresourceInfo(
        IReadOnlyList<TextureSubresource> subresources,
        int faceCount)
    {
        var items = new InfoSubresourceDocument[subresources.Count];
        for (var i = 0; i < subresources.Count; i++)
        {
            var subresource = subresources[i];
            items[i] = new InfoSubresourceDocument(
                subresource.MipLevel,
                subresource.ArrayLayer,
                FormatFace(subresource.FaceIndex, faceCount),
                subresource.FaceIndex,
                subresource.Width,
                subresource.Height,
                subresource.Payload.Length);
        }

        return items;
    }

    private static void PrintImageInfo<TPixel>(
        TextureContainer container,
        IBitmap<TPixel> bitmap,
        long fileBytes,
        string decodedFormat = nameof(TextureFormats.Rgba8UNorm))
        where TPixel : unmanaged, IPixel<TPixel>
    {
        PrintInfoLine("Container", FormatContainer(container));
        PrintInfoLine("Size", FormatSize(bitmap.Width, bitmap.Height));
        PrintInfoLine("Decoded format", decodedFormat);
        PrintInfoLine("File bytes", FormatInvariant(fileBytes));
    }

    private static void PrintDdsInfo(DdsTexture texture, long fileBytes, bool printSubresources)
    {
        PrintTextureInfo(
            TextureContainer.Dds,
            texture.Texture.Format,
            texture.Texture.Width,
            texture.Texture.Height,
            texture.Texture.MipLevelCount,
            texture.Texture.ArrayLayerCount,
            texture.Texture.FaceCount,
            GetPayloadByteCount(texture.Texture.Subresources),
            fileBytes);
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
            PrintSubresources(texture.Texture.Subresources, texture.Texture.FaceCount);
        }
    }

    private static void PrintKtxInfo(KtxTexture texture, KtxInfo info, long fileBytes, bool printSubresources)
    {
        PrintTextureInfo(
            TextureContainer.Ktx,
            texture.Texture.Format,
            texture.Texture.Width,
            texture.Texture.Height,
            texture.Texture.MipLevelCount,
            texture.Texture.ArrayLayerCount,
            texture.Texture.FaceCount,
            GetPayloadByteCount(texture.Texture.Subresources),
            fileBytes);
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
            PrintSubresources(texture.Texture.Subresources, texture.Texture.FaceCount);
        }
    }

    private static void PrintPvrInfo(PvrTexture texture, PvrInfo info, long fileBytes, bool printSubresources)
    {
        PrintTextureInfo(
            TextureContainer.Pvr,
            texture.Texture.Format,
            texture.Texture.Width,
            texture.Texture.Height,
            texture.Texture.MipLevelCount,
            texture.Texture.ArrayLayerCount,
            texture.Texture.FaceCount,
            GetPayloadByteCount(texture.Texture.Subresources),
            fileBytes);
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
            PrintSubresources(texture.Texture.Subresources, texture.Texture.FaceCount);
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
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"  mip={subresource.MipLevel} layer={subresource.ArrayLayer} face={FormatFace(subresource.FaceIndex, faceCount)} size={subresource.Width}x{subresource.Height} payload={subresource.Payload.Length}"));
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
            TextureContainer.Hdr => "HDR",
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

    private static string FormatFace(int faceIndex, int faceCount) =>
        faceCount == 6
            ? ((TextureCubeFace)faceIndex).ToString()
            : FormatInvariant(faceIndex);

    private static string FormatInvariant(long value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string FormatInvariant(ulong value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static void PrintInfoLine(string label, object? value) =>
        Console.WriteLine($"{label}: {value}");

    private static bool IsStructuredTextureContainer(TextureContainer container) =>
        container is TextureContainer.Dds or TextureContainer.Ktx or TextureContainer.Pvr;

    private static bool IsImageContainer(TextureContainer container) =>
        container is TextureContainer.Png or TextureContainer.Jpeg or TextureContainer.Gif or TextureContainer.Hdr;

    private static string GetImageExtension(TextureContainer container) =>
        container switch
        {
            TextureContainer.Png => ".png",
            TextureContainer.Jpeg => ".jpg",
            TextureContainer.Gif => ".gif",
            TextureContainer.Hdr => ".hdr",
            _ => throw new NotSupportedException($"'{FormatContainer(container)}' is not an image container.")
        };

    private static TextureImage ReadStructuredTexture(string path, TextureContainer container) =>
        container switch
        {
            TextureContainer.Dds => FromTexture(DdsCodec.Read(path)),
            TextureContainer.Ktx => FromTexture(KtxCodec.Read(path)),
            TextureContainer.Pvr => FromTexture(PvrCodec.Read(path)),
            _ => throw new NotSupportedException($"'{FormatContainer(container)}' is not a structured texture container.")
        };

    private static TextureImage FromTexture(DdsTexture texture) =>
        texture.Texture;

    private static TextureImage FromTexture(KtxTexture texture) =>
        texture.Texture;

    private static TextureImage FromTexture(PvrTexture texture) =>
        texture.Texture;

    private static TextureFormat ResolveAssembleFormat(
        ParseResult parseResult,
        Option<string> formatOption,
        ExtractManifest? manifest)
    {
        if (IsOptionExplicit(parseResult, formatOption) || manifest is null)
        {
            return TextureFormatCatalog.Get(parseResult.GetValue(formatOption) ?? nameof(TextureFormats.Rgba8UNorm));
        }

        var formatName = GetManifestFormatName(manifest);
        return TextureFormatCatalog.TryGet(formatName, out var format)
            ? format
            : throw new NotSupportedException($"Manifest source format '{manifest.SourceFormat}' is not recognized. Pass --format to choose an output format.");
    }

    private static string GetManifestFormatName(ExtractManifest manifest)
    {
        if (!string.IsNullOrWhiteSpace(manifest.SourceFormatName))
        {
            return manifest.SourceFormatName;
        }

        var sourceFormat = manifest.SourceFormat?.Trim() ?? string.Empty;
        if (sourceFormat.Length == 0)
        {
            throw new InvalidDataException("Manifest sourceFormat must not be empty. Pass --format to choose an output format.");
        }

        var separatorIndex = sourceFormat.IndexOf(' ', StringComparison.Ordinal);
        return separatorIndex < 0
            ? sourceFormat
            : sourceFormat[..separatorIndex];
    }

    private static ExtractManifest ReadExtractManifest(string path)
    {
        var manifest = JsonSerializer.Deserialize<ExtractManifest>(
            File.ReadAllText(path),
            CreateJsonOptions(writeIndented: false));
        return manifest ?? throw new InvalidDataException($"Manifest '{path}' is empty.");
    }

    private static TextureImage CreateManifestTexture(
        TextureFormat format,
        ImageColorSpaces colorSpaces,
        string manifestPath,
        ExtractManifest manifest)
    {
        ValidateManifestTopology(manifest);

        var manifestDirectory = Path.GetDirectoryName(Path.GetFullPath(manifestPath))
            ?? throw new InvalidOperationException("Manifest path has no parent directory.");
        var subresources = new TextureSubresource[checked(manifest.MipLevels * manifest.ArrayLayers * manifest.Faces)];
        foreach (var image in manifest.Images)
        {
            ValidateManifestImage(manifest, image);
            var imagePath = ResolveManifestImagePath(manifestDirectory, image.File);
            var bitmap = DecodeAssembleImage(new FileInfo(imagePath), colorSpaces);
            var expectedWidth = TextureImage.GetMipDimension(manifest.Width, image.Mip);
            var expectedHeight = TextureImage.GetMipDimension(manifest.Height, image.Mip);
            if (bitmap.Width != expectedWidth || bitmap.Height != expectedHeight)
            {
                throw new InvalidDataException(
                    $"Manifest image '{image.File}' is {bitmap.Width}x{bitmap.Height}, but {expectedWidth}x{expectedHeight} was expected for mip {image.Mip}.");
            }

            var subresource = EncodeSubresource(format, bitmap, image.Mip, image.Layer, image.FaceIndex);
            var index = TextureImage.GetSubresourceIndex(image.Mip, image.Layer, image.FaceIndex, manifest.MipLevels, manifest.ArrayLayers, manifest.Faces);
            if (subresources[index] is not null)
            {
                throw new InvalidDataException(
                    $"Manifest contains duplicate subresource mip={image.Mip} layer={image.Layer} face={FormatFace(image.FaceIndex, manifest.Faces)}.");
            }

            subresources[index] = subresource;
        }

        for (var layer = 0; layer < manifest.ArrayLayers; layer++)
        {
            for (var face = 0; face < manifest.Faces; face++)
            {
                for (var mip = 0; mip < manifest.MipLevels; mip++)
                {
                    var index = TextureImage.GetSubresourceIndex(mip, layer, face, manifest.MipLevels, manifest.ArrayLayers, manifest.Faces);
                    if (subresources[index] is null)
                    {
                        throw new InvalidDataException(
                            $"Manifest is missing subresource mip={mip} layer={layer} face={FormatFace(face, manifest.Faces)}.");
                    }
                }
            }
        }

        return new TextureImage(format, subresources, manifest.ArrayLayers, manifest.Faces);
    }

    private static void ValidateManifestTopology(ExtractManifest manifest)
    {
        if (manifest.Width <= 0 || manifest.Height <= 0)
        {
            throw new InvalidDataException("Manifest width and height must be greater than zero.");
        }

        if (manifest.MipLevels <= 0)
        {
            throw new InvalidDataException("Manifest mipLevels must be greater than zero.");
        }

        if (manifest.ArrayLayers <= 0)
        {
            throw new InvalidDataException("Manifest arrayLayers must be greater than zero.");
        }

        if (manifest.Faces is not (1 or 6))
        {
            throw new InvalidDataException("Manifest faces must be 1 or 6.");
        }

        if (manifest.Faces == 6 && manifest.Width != manifest.Height)
        {
            throw new InvalidDataException("Cube-map manifests must have square base dimensions.");
        }

        if (manifest.Images is null || manifest.Images.Count == 0)
        {
            throw new InvalidDataException("Manifest must contain at least one image.");
        }
    }

    private static void ValidateManifestImage(ExtractManifest manifest, ExtractManifestImage image)
    {
        if (image.Mip < 0 || image.Mip >= manifest.MipLevels)
        {
            throw new InvalidDataException($"Manifest image mip {image.Mip} is outside mipLevels {manifest.MipLevels}.");
        }

        if (image.Layer < 0 || image.Layer >= manifest.ArrayLayers)
        {
            throw new InvalidDataException($"Manifest image layer {image.Layer} is outside arrayLayers {manifest.ArrayLayers}.");
        }

        if (image.FaceIndex < 0 || image.FaceIndex >= manifest.Faces)
        {
            throw new InvalidDataException($"Manifest image faceIndex {image.FaceIndex} is outside faces {manifest.Faces}.");
        }

        if (string.IsNullOrWhiteSpace(image.File))
        {
            throw new InvalidDataException("Manifest image file must not be empty.");
        }

        var expectedWidth = TextureImage.GetMipDimension(manifest.Width, image.Mip);
        var expectedHeight = TextureImage.GetMipDimension(manifest.Height, image.Mip);
        if (image.Width != expectedWidth || image.Height != expectedHeight)
        {
            throw new InvalidDataException(
                $"Manifest image '{image.File}' declares {image.Width}x{image.Height}, but {expectedWidth}x{expectedHeight} was expected for mip {image.Mip}.");
        }
    }

    private static string ResolveManifestImagePath(string manifestDirectory, string imageFile) =>
        Path.IsPathRooted(imageFile)
            ? imageFile
            : Path.GetFullPath(Path.Combine(manifestDirectory, imageFile));

    private static IReadOnlyList<ExtractManifestImage> ExtractSubresources(
        IReadOnlyList<TextureExtractedImage<Rgba8UNorm>> images,
        int faceCount,
        string outputDirectory,
        TextureContainer imageContainer,
        string pattern,
        int jpegQuality,
        ImageColorSpaces colorSpaces)
    {
        var entries = new List<ExtractManifestImage>(images.Count);
        var usedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var image in images)
        {
            var fileName = BuildExtractFileName(image, faceCount, imageContainer, pattern);
            if (!usedFileNames.Add(fileName))
            {
                throw new InvalidOperationException($"Pattern produced duplicate output file '{fileName}'.");
            }

            var outputPath = Path.Combine(outputDirectory, fileName);
            Encode(image.Image, outputPath, imageContainer, TextureFormats.Rgba8UNorm, 1, jpegQuality, null, MipmapMode.None, colorSpaces);
            entries.Add(new ExtractManifestImage(
                image.MipLevel,
                image.ArrayLayer,
                FormatFace(image.FaceIndex, faceCount),
                image.FaceIndex,
                image.Image.Width,
                image.Image.Height,
                fileName));
        }

        return entries;
    }

    private static string BuildExtractFileName(
        TextureExtractedImage<Rgba8UNorm> image,
        int faceCount,
        TextureContainer imageContainer,
        string pattern)
    {
        var face = FormatFace(image.FaceIndex, faceCount);
        var stem = pattern
            .Replace("{mip}", FormatInvariant(image.MipLevel), StringComparison.OrdinalIgnoreCase)
            .Replace("{layer}", FormatInvariant(image.ArrayLayer), StringComparison.OrdinalIgnoreCase)
            .Replace("{face}", face, StringComparison.OrdinalIgnoreCase)
            .Replace("{faceIndex}", FormatInvariant(image.FaceIndex), StringComparison.OrdinalIgnoreCase)
            .Replace("{width}", FormatInvariant(image.Image.Width), StringComparison.OrdinalIgnoreCase)
            .Replace("{height}", FormatInvariant(image.Image.Height), StringComparison.OrdinalIgnoreCase);
        stem = SanitizeFileName(stem);
        if (string.IsNullOrWhiteSpace(stem))
        {
            throw new ArgumentException("--pattern produced an empty output file name.");
        }

        return stem + GetImageExtension(imageContainer);
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = fileName.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalidChars, chars[i]) >= 0)
            {
                chars[i] = '_';
            }
        }

        var sanitized = new string(chars).Trim();
        return sanitized is "." or ".." ? "_" + sanitized : sanitized;
    }

    private static void WriteExtractManifest(
        string inputPath,
        TextureContainer sourceContainer,
        TextureImage texture,
        TextureContainer imageContainer,
        string manifestPath,
        IReadOnlyList<ExtractManifestImage> images)
    {
        var baseSubresource = texture.GetSubresource(0, 0, 0);
        var manifest = new ExtractManifest(
            inputPath,
            FormatContainer(sourceContainer),
            FormatTextureFormat(texture.Format),
            FormatContainer(imageContainer),
            baseSubresource.Width,
            baseSubresource.Height,
            texture.MipLevelCount,
            texture.ArrayLayerCount,
            texture.FaceCount,
            images)
        {
            SourceFormatName = TextureFormatCatalog.GetFieldName(texture.Format)
        };
        var json = JsonSerializer.Serialize(
            manifest,
            CreateJsonOptions(writeIndented: true));
        File.WriteAllText(manifestPath, json + Environment.NewLine);
    }

    private static JsonSerializerOptions CreateJsonOptions(bool writeIndented) =>
        new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = writeIndented
        };

    private static TextureImage CreateAssembledTexture(
        TextureFormat format,
        ImageColorSpaces colorSpaces,
        IReadOnlyList<FileInfo> layerFiles,
        IReadOnlyList<FileInfo> cubeFiles,
        IReadOnlyList<FileInfo> mipFiles)
    {
        var assembler = new TextureAssembler();
        if (layerFiles.Count != 0)
        {
            return assembler.CreateArray(format, DecodeImageFiles(layerFiles, colorSpaces));
        }

        if (cubeFiles.Count != 0)
        {
            return assembler.CreateCube(format, DecodeImageFiles(cubeFiles, colorSpaces));
        }

        if (mipFiles.Count != 0)
        {
            return assembler.CreateMipChain(format, DecodeImageFiles(mipFiles, colorSpaces));
        }

        throw new ArgumentException("Specify exactly one of --layers, --cube, or --mips.");
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
        if (!IsImageContainer(container))
        {
            throw new NotSupportedException("Assemble inputs must be PNG, JPEG, GIF, or HDR images.");
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

    private static void WriteStructuredTexture(
        TextureImage texture,
        string path,
        TextureContainer container,
        TextureFormat format,
        int ktxVersion,
        TextureCompressionLevel? quality)
    {
        var output = new TextureConverter().TranscodeTexture(texture, format, quality);

        switch (container)
        {
            case TextureContainer.Dds:
                DdsCodec.Write(new DdsTexture(output), path);
                break;
            case TextureContainer.Ktx:
                KtxCodec.Write(
                    new KtxTexture(output),
                    path,
                    new KtxEncodingOptions { Version = ktxVersion == 2 ? KtxVersion.Version2 : KtxVersion.Version1 });
                break;
            case TextureContainer.Pvr:
                PvrCodec.Write(new PvrTexture(output), path);
                break;
            default:
                throw new NotSupportedException($"Unsupported structured texture output container '{container}'.");
        }
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
            TextureContainer.Hdr => DecodeImage(HdrCodec.DecodeRgba8(path), selection),
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
        => new TextureExtractor().Decode(texture.Texture, selection);

    private static ArrayBitmap<Rgba8UNorm> DecodeTexture(KtxTexture texture, TextureSubresourceSelection selection)
        => new TextureExtractor().Decode(texture.Texture, selection);

    private static ArrayBitmap<Rgba8UNorm> DecodeTexture(PvrTexture texture, TextureSubresourceSelection selection)
        => new TextureExtractor().Decode(texture.Texture, selection);

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
            case TextureContainer.Hdr:
                EnsureNoMipmaps(mipmaps, container);
                HdrCodec.Encode(bitmap, path);
                break;
            case TextureContainer.Dds:
                if (mipmaps == MipmapMode.Generate)
                {
                    DdsCodec.EncodeMipChain(
                        BitmapMipChain.Generate(bitmap, TextureMipmapGenerationOptions.GetDefault(format)),
                        path,
                        new DdsEncodingOptions { TextureFormat = format });
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
                    KtxCodec.EncodeMipChain(
                        BitmapMipChain.Generate(bitmap, TextureMipmapGenerationOptions.GetDefault(format)),
                        path,
                        ktxOptions);
                }
                else
                {
                    KtxCodec.Encode(bitmap, path, ktxOptions);
                }

                break;
            case TextureContainer.Pvr:
                if (mipmaps == MipmapMode.Generate)
                {
                    PvrCodec.EncodeMipChain(
                        BitmapMipChain.Generate(bitmap, TextureMipmapGenerationOptions.GetDefault(format)),
                        path,
                        new PvrEncodingOptions { TextureFormat = format });
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
            ".hdr" => TextureContainer.Hdr,
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

    private static void PrintFormatsJson(
        string? query,
        bool compressedOnly,
        bool uncompressedOnly,
        IReadOnlyList<FormatEntry> formats)
    {
        var document = new FormatsDocument(
            query,
            new FormatFiltersDocument(compressedOnly, uncompressedOnly),
            formats.Count,
            formats
                .OrderBy(static item => item.FieldName, StringComparer.OrdinalIgnoreCase)
                .Select(static item => BuildFormatDocument(item))
                .ToArray());
        Console.WriteLine(JsonSerializer.Serialize(document, CreateJsonOptions(writeIndented: true)));
    }

    private static FormatDocument BuildFormatDocument(FormatEntry entry)
    {
        var format = entry.Format;
        return new FormatDocument(
            entry.FieldName,
            format.Name,
            format.Kind.ToString(),
            format.Components.ToString(),
            format.ValueKind.ToString(),
            format.IsCompressed,
            format.ChannelCount,
            format.RedBits,
            format.GreenBits,
            format.BlueBits,
            format.AlphaBits,
            format.BlockWidth,
            format.BlockHeight,
            format.BlockDepth,
            format.BitsPerBlock,
            format.BytesPerBlock,
            format.BitsPerTexel,
            format.HeaderByteCount,
            format.IsVariableSize,
            format.SizeMode.ToString());
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

    private static void PrintConvertResult(string outputPath, BitmapQualityResult? metrics, bool printJson)
    {
        if (printJson)
        {
            var document = new ConvertResultDocument(
                outputPath,
                metrics is null ? null : BuildQualityDocument(metrics));
            Console.WriteLine(JsonSerializer.Serialize(document, CreateJsonOptions(writeIndented: true)));
            return;
        }

        Console.WriteLine($"wrote {outputPath}");
        if (metrics is not null)
        {
            PrintQuality(metrics);
        }
    }

    private static void PrintQuality(BitmapQualityResult quality, bool printJson = false)
    {
        if (printJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(BuildQualityDocument(quality), CreateJsonOptions(writeIndented: true)));
            return;
        }

        PrintQualityText(quality);
    }

    private static QualityDocument BuildQualityDocument(BitmapQualityResult quality) =>
        new(
            quality.Width,
            quality.Height,
            quality.IncludesAlpha,
            quality.MeanSquaredError,
            quality.RootMeanSquaredError,
            GetFiniteValueOrNull(quality.PeakSignalToNoiseRatio),
            FormatPsnrValue(quality.PeakSignalToNoiseRatio),
            new QualityChannelsDocument(
                BuildQualityChannelDocument(quality.Red),
                BuildQualityChannelDocument(quality.Green),
                BuildQualityChannelDocument(quality.Blue),
                quality.Alpha is null ? null : BuildQualityChannelDocument(quality.Alpha)));

    private static QualityChannelDocument BuildQualityChannelDocument(BitmapChannelQuality quality) =>
        new(
            quality.MeanSquaredError,
            quality.RootMeanSquaredError,
            GetFiniteValueOrNull(quality.PeakSignalToNoiseRatio),
            FormatPsnrValue(quality.PeakSignalToNoiseRatio));

    private static double? GetFiniteValueOrNull(double value) =>
        double.IsFinite(value) ? value : null;

    private static string FormatPsnrValue(double value)
    {
        if (double.IsPositiveInfinity(value))
        {
            return "inf";
        }

        if (double.IsNegativeInfinity(value))
        {
            return "-inf";
        }

        return value.ToString("F3", CultureInfo.InvariantCulture) + " dB";
    }

    private static void PrintQualityText(BitmapQualityResult quality)
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
    Hdr,
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

internal sealed record ImageColorSpaces(ImageColorSpace Png, ImageColorSpace Jpeg, ImageColorSpace Gif)
{
    public static ImageColorSpaces Default { get; } = new(
        ImageColorSpace.Linear,
        ImageColorSpace.Linear,
        ImageColorSpace.Linear);
}

internal sealed record InfoDocument(string Container, int Width, int Height, long FileBytes)
{
    public string? DecodedFormat { get; init; }

    public string? Format { get; init; }

    public string? FormatName { get; init; }

    public string? Kind { get; init; }

    public string? ValueKind { get; init; }

    public int? MipLevels { get; init; }

    public int? ArrayLayers { get; init; }

    public int? Faces { get; init; }

    public long? PayloadBytes { get; init; }

    public int? BlockWidth { get; init; }

    public int? BlockHeight { get; init; }

    public int? BitsPerBlock { get; init; }

    public int? BitsPerTexel { get; init; }

    public DdsInfoDocument? Dds { get; init; }

    public KtxInfoDocument? Ktx { get; init; }

    public PvrInfoDocument? Pvr { get; init; }

    public IReadOnlyList<InfoSubresourceDocument>? Subresources { get; init; }
}

internal sealed record InfoSubresourceDocument(
    int Mip,
    int Layer,
    string Face,
    int FaceIndex,
    int Width,
    int Height,
    int PayloadBytes);

internal sealed record DdsInfoDocument(
    string Header,
    string? DxgiFormat,
    string? AlphaMode,
    string? LegacyPixelFormat);

internal sealed record KtxInfoDocument(
    int Version,
    string? VkFormat,
    string? GlType,
    string? GlFormat,
    string? GlInternalFormat,
    string? Supercompression,
    uint KeyValueBytes,
    ulong SupercompressionGlobalDataBytes);

internal sealed record PvrInfoDocument(
    int Version,
    string? PixelFormat,
    uint? ColourSpace,
    string? ColourSpaceDescription,
    uint? ChannelType,
    uint MetadataBytes,
    int? MetadataEntries,
    string? LegacyPixelType,
    uint? LegacyBitCount);

internal sealed record ConvertResultDocument(
    string Output,
    QualityDocument? Quality);

internal sealed record QualityDocument(
    int Width,
    int Height,
    bool IncludesAlpha,
    double MeanSquaredError,
    double RootMeanSquaredError,
    double? PeakSignalToNoiseRatio,
    string PeakSignalToNoiseRatioText,
    QualityChannelsDocument Channels);

internal sealed record QualityChannelsDocument(
    QualityChannelDocument Red,
    QualityChannelDocument Green,
    QualityChannelDocument Blue,
    QualityChannelDocument? Alpha);

internal sealed record QualityChannelDocument(
    double MeanSquaredError,
    double RootMeanSquaredError,
    double? PeakSignalToNoiseRatio,
    string PeakSignalToNoiseRatioText);

internal sealed record FormatsDocument(
    string? Query,
    FormatFiltersDocument Filters,
    int Count,
    IReadOnlyList<FormatDocument> Formats);

internal sealed record FormatFiltersDocument(bool Compressed, bool Uncompressed);

internal sealed record FormatDocument(
    string FieldName,
    string FormatName,
    string Kind,
    string Components,
    string ValueKind,
    bool IsCompressed,
    int ChannelCount,
    int RedBits,
    int GreenBits,
    int BlueBits,
    int AlphaBits,
    int BlockWidth,
    int BlockHeight,
    int BlockDepth,
    int BitsPerBlock,
    int BytesPerBlock,
    int BitsPerTexel,
    int HeaderByteCount,
    bool IsVariableSize,
    string SizeMode);

internal sealed record ExtractManifest(
    string Source,
    string SourceContainer,
    string SourceFormat,
    string ImageContainer,
    int Width,
    int Height,
    int MipLevels,
    int ArrayLayers,
    int Faces,
    IReadOnlyList<ExtractManifestImage> Images)
{
    public string? SourceFormatName { get; init; }
}

internal sealed record ExtractManifestImage(
    int Mip,
    int Layer,
    string Face,
    int FaceIndex,
    int Width,
    int Height,
    string File);

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
