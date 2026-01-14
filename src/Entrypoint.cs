using Spectre.Console;

namespace SwiftlyS2.Codegen;

public static class Entrypoint
{
    private static string? _projectRootPath;

    public static string ProjectRootPath
    {
        get
        {
            if (_projectRootPath == null)
            {
                var assemblyLocation = AppDomain.CurrentDomain.BaseDirectory;
                var current = new DirectoryInfo(assemblyLocation);
                while (current != null)
                {
                    if (current.GetFiles("*.csproj").Length > 0)
                    {
                        _projectRootPath = current.FullName;
                        break;
                    }
                    current = current.Parent;
                }

                _projectRootPath ??= assemblyLocation;
            }
            return _projectRootPath;
        }
    }

    public static async Task Main()
    {
        var gameChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>().Title("Select the [green]game[/] to generate Swiftly:Source2 files:")
                .AddChoices(["Counter-Strike: 2"])
        );

        AnsiConsole.MarkupLine($"Generating Swiftly:Source2 files for [green]{gameChoice}[/]...");
        AnsiConsole.WriteLine();

        switch (gameChoice)
        {
            case "Counter-Strike: 2":
                await CS2.GeneratorOptions.ShowGeneratorOptionsAsync();
                break;
            default:
                AnsiConsole.MarkupLine("[red]Error:[/] Unknown game selected.");
                return;
        }
    }

    public static string GenerateOutputPath(string path)
    {
        return "output/" + path;
    }
}