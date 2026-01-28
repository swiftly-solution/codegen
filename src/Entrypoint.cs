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

    public static async Task Main(string[] args)
    {
        // Parse command-line arguments
        string? nativesPath = null;
        string? gameEventsPath = null;

        for (int i = 0; i < args.Length; i++)
        {
            if ((args[i] == "--natives-path" || args[i] == "-n") && i + 1 < args.Length)
            {
                nativesPath = args[i + 1];
                i++;
            }
            else if ((args[i] == "--gameevents-path" || args[i] == "-g") && i + 1 < args.Length)
            {
                gameEventsPath = args[i + 1];
                i++;
            }
            else if (args[i] == "--help" || args[i] == "-h")
            {
                ShowHelp();
                return;
            }
        }

        var gameChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>().Title("Select the [green]game[/] to generate Swiftly:Source2 files:")
                .AddChoices(["Counter-Strike: 2"])
        );

        AnsiConsole.MarkupLine($"Generating Swiftly:Source2 files for [green]{gameChoice}[/]...");
        AnsiConsole.WriteLine();

        switch (gameChoice)
        {
            case "Counter-Strike: 2":
                await CS2.GeneratorOptions.ShowGeneratorOptionsAsync(nativesPath, gameEventsPath);
                break;
            default:
                AnsiConsole.MarkupLine("[red]Error:[/] Unknown game selected.");
                return;
        }
    }

    private static void ShowHelp()
    {
        AnsiConsole.MarkupLine("[bold]SwiftlyS2 Code Generator[/]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Usage:[/]");
        AnsiConsole.MarkupLine("  SwiftlyS2.Codegen [options]");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Options:[/]");
        AnsiConsole.MarkupLine("  -n, --natives-path <path>      Path to the natives folder");
        AnsiConsole.MarkupLine("  -g, --gameevents-path <path>   Path to the game events folder");
        AnsiConsole.MarkupLine("  -h, --help                     Show this help message");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Examples:[/]");
        AnsiConsole.MarkupLine("  SwiftlyS2.Codegen --natives-path C:\\path\\to\\natives");
        AnsiConsole.MarkupLine("  SwiftlyS2.Codegen -n C:\\path\\to\\natives -g C:\\path\\to\\gameevents");
    }

    public static string BrowseForDirectory(string title, string? defaultPath = null)
    {
        defaultPath ??= ProjectRootPath;
        var currentPath = defaultPath;

        while (true)
        {
            var directories = Directory.GetDirectories(currentPath)
                .Select(d => new DirectoryInfo(d).Name)
                .OrderBy(d => d)
                .ToList();

            var choices = new List<string>();
            if (Directory.GetParent(currentPath) != null)
            {
                choices.Add(".. (Parent Directory)");
            }
            choices.Add("✓ Select this directory");
            choices.AddRange(directories);

            var selection = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"{title}\n[grey]Current: {currentPath}[/]")
                    .PageSize(15)
                    .MoreChoicesText("[grey](Move up and down to see more folders)[/]")
                    .AddChoices(choices)
            );

            if (selection == "✓ Select this directory")
            {
                return currentPath;
            }
            else if (selection == ".. (Parent Directory)")
            {
                currentPath = Directory.GetParent(currentPath)!.FullName;
            }
            else
            {
                currentPath = Path.Combine(currentPath, selection);
            }
        }
    }

    public static string GenerateOutputPath(string path)
    {
        return "output/" + path;
    }
}