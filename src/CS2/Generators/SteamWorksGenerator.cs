using SwiftlyS2.Codegen.CS2.Generators.SteamWorks;
using SwiftlyS2.Codegen.CS2.SteamWorks.Parser;

namespace SwiftlyS2.Codegen.CS2.Generators;

/// <summary>
/// Generator for Steamworks API bindings
/// </summary>
public class SteamworksGenerator : BaseGenerator
{
    /// <inheritdoc />
    public override string Name => "Steamworks";

    /// <inheritdoc />
    public override string OutputPath => Path.Combine(Entrypoint.ProjectRootPath, "output", "src", "SwiftlyS2.Generated", "Steamworks");

    private readonly string? _headersPath;

    /// <inheritdoc />
    public override string? DataPath => _headersPath;

    public SteamworksGenerator(string? headersPath)
    {
        _headersPath = headersPath;
    }

    /// <inheritdoc />
    public override async Task<GeneratorResult> GenerateFilesAsync()
    {
        try
        {
            if (string.IsNullOrEmpty(_headersPath))
                return new GeneratorResult { Success = false, ErrorMessage = "Steamworks headers path not set." };

            Progress.Report("Parsing Steamworks headers...");
            ParserSettings.FakeGameserverInterfaces = true;
            var parser = SteamworksParser.Parse(_headersPath);

            if (Directory.Exists(OutputPath))
                Directory.Delete(OutputPath, true);
            Directory.CreateDirectory(OutputPath);

            Progress.Report("Generating constants...");
            await ConstantsGenerator.GenerateAsync(parser, OutputPath);

            Progress.Report("Generating enums...");
            await EnumsGenerator.GenerateAsync(parser, OutputPath);

            var templatesPath = Path.Combine(Entrypoint.ProjectRootPath, "data", "templates");

            Progress.Report("Generating interfaces...");
            await InterfacesGenerator.GenerateAsync(parser, OutputPath, templatesPath);

            Progress.Report("Generating structs...");
            await StructsGenerator.GenerateAsync(parser, OutputPath);

            Progress.Report("Generating typedefs...");
            await TypedefsGenerator.GenerateAsync(parser, OutputPath, templatesPath);

            Progress.Report("Done.");
            return new GeneratorResult { Success = true };
        }
        catch (Exception ex)
        {
            return new GeneratorResult { Success = false, ErrorMessage = ex.Message, Exception = ex };
        }
    }
}
