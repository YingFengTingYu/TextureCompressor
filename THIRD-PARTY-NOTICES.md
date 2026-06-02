# Third-Party Notices

TextureCompressor is licensed under the MIT License. This file summarizes
third-party NuGet packages used by the repository and records descriptive
trademark usage for supported texture and container formats.

This notice is informational and should not replace review of the referenced
license texts, package metadata, or upstream project terms.

## Core Library Scope

The core `TextureCompressor` library is implemented as managed code and does
not reference the external NuGet packages listed below. These dependencies are
used by optional encoder adapter projects, the CLI, source generator, tests, or
specific file-format packages such as KTX. If an application references only the
core library project/package, it does not take dependencies on those external
encoder, native runtime, CLI, or test packages.

## Direct NuGet Package References

| Package | Version | Declared license | Project URL | Referenced by | Usage |
| --- | --- | --- | --- | --- | --- |
| AstcEncoderCSharp | 5.4.3 | MIT | https://github.com/Ash39/Astc-Encoder-csharp | TextureCompressor.Codecs.AstcEnc | Optional ASTC encoder adapter. |
| BasisUniversal.NET | 1.0.0-preview.3 | Apache-2.0 | https://github.com/YingFengTingYu/BasisUniversal.NET | TextureCompressor.Codecs.BasisUniversal | Optional Basis Universal encoder/transcoder adapter. |
| BCnEncoder.Net | 2.3.0 | MIT OR Unlicense | https://github.com/Nominom/BCnEncoder.NET | TextureCompressor.Codecs.BCnEncoder | Optional BCn encoder adapter. |
| Hexa.NET.DirectXTex | 2.0.4 | MIT | https://github.com/HexaEngine/Hexa.NET.DirectXTex | TextureCompressor.Codecs.DirectXTex | Optional DirectXTex encoder adapter. |
| Microsoft.CodeAnalysis.CSharp | 5.3.0 | MIT | https://github.com/dotnet/roslyn | TextureCompressor.SourceGenerators | Source generator support. |
| PVRTexLib.NET | 1.0.3 | MIT | https://github.com/YingFengTingYu/PVRTexLib.NET | TextureCompressor.Codecs.PVRTexLib | Optional PVRTexLib encoder adapter and test reference. |
| System.CommandLine | 2.0.8 | MIT | https://github.com/dotnet/command-line-api | TextureCompressor.Cli | CLI command-line parsing. |
| ZstdSharp.Port | 0.8.8 | MIT | https://github.com/oleg-st/ZstdSharp | TextureCompressor.FileFormats.Ktx | KTX2 Zstandard supercompression support. |
| coverlet.collector | 6.0.4 | MIT | https://github.com/coverlet-coverage/coverlet | Test projects | Test coverage collection. |
| Microsoft.NET.Test.Sdk | 17.14.1 | MIT | https://github.com/microsoft/vstest | Test projects | Test SDK. |
| xunit | 2.9.3 | Apache-2.0 | https://github.com/xunit/xunit | Test projects | Test framework. |
| xunit.runner.visualstudio | 3.1.4 | Apache-2.0 | https://github.com/xunit/visualstudio.xunit | Test projects | Test runner. |

## Notable Transitive Packages

| Package | Version | Declared license | Project URL | Pulled by |
| --- | --- | --- | --- | --- |
| BasisUniversal.Native | 1.0.0-preview.3 | Apache-2.0 | https://github.com/YingFengTingYu/BasisUniversal.NET | BasisUniversal.NET |
| BasisUniversal.NET.LowLevel | 1.0.0-preview.3 | Apache-2.0 | https://github.com/YingFengTingYu/BasisUniversal.NET | BasisUniversal.NET |
| CommunityToolkit.HighPerformance | 8.4.0 | MIT | https://github.com/CommunityToolkit/dotnet | BCnEncoder.Net |
| HexaGen.Runtime | 1.1.16 | MIT | https://github.com/JunaMeinhold/HexaGen | Hexa.NET.DirectXTex |
| HexaGen.Runtime.COM | 1.1.9 | MIT | https://github.com/JunaMeinhold/HexaGen | Hexa.NET.DirectXTex |
| Microsoft.CodeAnalysis.Analyzers | 5.3.0-2.25625.1 | MIT | https://github.com/dotnet/roslyn | Microsoft.CodeAnalysis.CSharp |
| Microsoft.CodeAnalysis.Common | 5.3.0 | MIT | https://github.com/dotnet/roslyn | Microsoft.CodeAnalysis.CSharp |
| Microsoft.CodeCoverage | 17.14.1 | MIT | https://github.com/microsoft/vstest | Microsoft.NET.Test.Sdk / coverlet.collector |
| Microsoft.TestPlatform.ObjectModel | 17.14.1 | MIT | https://github.com/microsoft/vstest | Microsoft.NET.Test.Sdk |
| Microsoft.TestPlatform.TestHost | 17.14.1 | MIT | https://github.com/microsoft/vstest | Microsoft.NET.Test.Sdk |
| NETStandard.Library | 2.0.3 | MIT | https://github.com/dotnet/standard | Microsoft.CodeAnalysis.CSharp |
| Newtonsoft.Json | 13.0.3 | MIT | https://www.newtonsoft.com/json | Microsoft.NET.Test.Sdk |
| System.Buffers | 4.6.0 | MIT | https://github.com/dotnet/maintenance-packages | Microsoft.CodeAnalysis.CSharp |
| System.Collections.Immutable | 9.0.0 | MIT | https://github.com/dotnet/runtime | Microsoft.CodeAnalysis.CSharp |
| System.Memory | 4.6.0 | MIT | https://github.com/dotnet/maintenance-packages | Microsoft.CodeAnalysis.CSharp |
| System.Numerics.Vectors | 4.6.0 | MIT | https://github.com/dotnet/maintenance-packages | Microsoft.CodeAnalysis.CSharp |
| System.Reflection.Metadata | 9.0.0 | MIT | https://github.com/dotnet/runtime | Microsoft.CodeAnalysis.CSharp |
| System.Runtime.CompilerServices.Unsafe | 6.1.0 | MIT | https://github.com/dotnet/maintenance-packages | Microsoft.CodeAnalysis.CSharp |
| System.Text.Encoding.CodePages | 8.0.0 | MIT | https://github.com/dotnet/runtime | Microsoft.CodeAnalysis.CSharp |
| System.Threading.Tasks.Extensions | 4.6.0 | MIT | https://github.com/dotnet/maintenance-packages | Microsoft.CodeAnalysis.CSharp |
| xunit.abstractions | 2.0.3 | Apache-2.0 | https://github.com/xunit/xunit | xunit |
| xunit.analyzers | 1.18.0 | Apache-2.0 | https://github.com/xunit/xunit.analyzers | xunit / xunit.runner.visualstudio |
| xunit.assert | 2.9.3 | Apache-2.0 | https://github.com/xunit/xunit | xunit |
| xunit.core | 2.9.3 | Apache-2.0 | https://github.com/xunit/xunit | xunit |
| xunit.extensibility.core | 2.9.3 | Apache-2.0 | https://github.com/xunit/xunit | xunit |
| xunit.extensibility.execution | 2.9.3 | Apache-2.0 | https://github.com/xunit/xunit | xunit |

## Native And Upstream Components

Some NuGet packages include or bind to native upstream libraries:

- AstcEncoderCSharp is a C# binding for Arm ASTC Encoder. The binding package
  declares MIT; the upstream Arm ASTC Encoder project is Apache-2.0.
- BasisUniversal.Native builds and distributes native binaries from
  BinomialLLC/basis_universal, which is licensed under Apache-2.0.
- Hexa.NET.DirectXTex wraps Microsoft DirectXTex, which is licensed under MIT.
- PVRTexLib.NET declares MIT for the .NET wrapper and includes PVRTexLib native
  runtime binaries. Review the PowerVR Tools / PVRTexLib terms from Imagination
  Technologies before redistributing packages or binaries that include those
  native components.

## Trademark And Certification Notice

Names such as KTX, Khronos, OpenGL, DirectX, DirectDraw Surface, DDS, PVR,
PVRTC, PowerVR, ASTC, Basis Universal, Basis, UASTC, ETC, EAC, S3TC, DXT, BCn,
BC1-BC7, RGTC, LATC, BPTC, and other product, API, codec, container, or format
names are used descriptively to identify interoperability targets and supported
data formats.

All trademarks are the property of their respective owners. TextureCompressor is
not affiliated with, sponsored by, endorsed by, certified by, or otherwise
approved by Khronos Group, Microsoft, Arm, Imagination Technologies, Binomial
LLC, or any other named rights holder. References to KTX, OpenGL, DirectX, PVR,
ASTC, Basis Universal, or related formats do not state or imply official
conformance, adopter status, certification, validation, or endorsement.
