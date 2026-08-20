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
        bool gameEvents = false;
        string? protobufsPath = null;
        bool datamaps = false;
        bool schemas = false;
        string? steamworksPath = null;
        string? game = null;

        for (int i = 0; i < args.Length; i++)
        {
            if ((args[i] == "--game" || args[i] == "-game") && i + 1 < args.Length)
            {
                game = args[i + 1];
                i++;
            }
            else if ((args[i] == "--natives-path" || args[i] == "-n") && i + 1 < args.Length)
            {
                nativesPath = args[i + 1];
                i++;
            }
            else if (args[i] == "--gameevents" || args[i] == "-g")
            {
                gameEvents = true;
            }
            else if ((args[i] == "--protobufs-path" || args[i] == "-p") && i + 1 < args.Length)
            {
                protobufsPath = args[i + 1];
                i++;
            }
            else if (args[i] == "--datamaps" || args[i] == "-d")
            {
                datamaps = true;
            }
            else if (args[i] == "--schemas" || args[i] == "-schema")
            {
                schemas = true;
            }
            else if ((args[i] == "--steamworks-path" || args[i] == "-s") && i + 1 < args.Length)
            {
                steamworksPath = args[i + 1];
                i++;
            }
            else if (args[i] == "--help" || args[i] == "-h")
            {
                ShowHelp();
                return;
            }
        }

        if (game == null)
        {
            game = AnsiConsole.Prompt(
                new SelectionPrompt<string>().Title("Select the [green]game[/] to generate Swiftly:Source2 files:")
                    .AddChoices(["Counter-Strike: 2"])
            );
        }

        AnsiConsole.MarkupLine($"Generating Swiftly:Source2 files for [green]{game}[/]...");
        AnsiConsole.WriteLine();

        switch (game)
        {
            case "Counter-Strike: 2":
            case "cs2":
                await CS2.GeneratorOptions.ShowGeneratorOptionsAsync(nativesPath, gameEvents, protobufsPath, datamaps, schemas, steamworksPath);
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
        AnsiConsole.MarkupLine("  -g, --gameevents               Generate game events (downloaded from CS2-Dumps)");
        AnsiConsole.MarkupLine("  -p, --protobufs-path <path>    Path to the protobufs folder");
        AnsiConsole.MarkupLine("  -d, --datamaps                 Generate datamaps (downloaded from CS2-Dumps)");
        AnsiConsole.MarkupLine("  -schema, --schemas             Generate schemas (downloaded from CS2-Dumps)");
        AnsiConsole.MarkupLine("  -s, --steamworks-path <path>   Path to the steam api folder");
        AnsiConsole.MarkupLine("  -h, --help                     Show this help message");
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Examples:[/]");
        AnsiConsole.MarkupLine("  SwiftlyS2.Codegen --natives-path C:\\path\\to\\natives");
        AnsiConsole.MarkupLine("  SwiftlyS2.Codegen -n C:\\path\\to\\natives -g -s C:\\path\\to\\steam\\folder");
    }

    public static string BrowseForDirectory(string title, string? defaultPath = null)
    {
        defaultPath ??= ProjectRootPath;
        var currentPath = defaultPath;

        // Ensure the starting path exists
        while (!Directory.Exists(currentPath))
        {
            var parent = Directory.GetParent(currentPath);
            if (parent == null)
            {
                currentPath = ProjectRootPath;
                break;
            }
            currentPath = parent.FullName;
        }

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

    public static string BrowseForFile(string title, string? defaultPath = null, string? fileExtension = null)
    {
        defaultPath ??= ProjectRootPath;
        var currentPath = defaultPath;

        // Ensure the starting path exists
        while (!Directory.Exists(currentPath))
        {
            var parent = Directory.GetParent(currentPath);
            if (parent == null)
            {
                currentPath = ProjectRootPath;
                break;
            }
            currentPath = parent.FullName;
        }

        while (true)
        {
            var directories = Directory.GetDirectories(currentPath)
                .Select(d => new DirectoryInfo(d).Name)
                .OrderBy(d => d)
                .ToList();

            var files = Directory.GetFiles(currentPath)
                .Select(f => new FileInfo(f).Name)
                .Where(f => fileExtension == null || f.EndsWith(fileExtension, StringComparison.OrdinalIgnoreCase))
                .OrderBy(f => f)
                .ToList();

            var choices = new List<string>();
            if (Directory.GetParent(currentPath) != null)
            {
                choices.Add(".. (Parent Directory)");
            }

            // Add files first
            foreach (var file in files)
            {
                choices.Add($"📄 {file}");
            }

            // Then directories
            foreach (var dir in directories)
            {
                choices.Add($"📁 {dir}");
            }

            if (choices.Count == 0 || (choices.Count == 1 && choices[0] == ".. (Parent Directory)"))
            {
                choices.Add("[grey](No items in this directory)[/]");
            }

            var selection = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title($"{title}\n[grey]Current: {currentPath}[/]")
                    .PageSize(15)
                    .MoreChoicesText("[grey](Move up and down to see more items)[/]")
                    .AddChoices(choices)
            );

            if (selection == ".. (Parent Directory)")
            {
                currentPath = Directory.GetParent(currentPath)!.FullName;
            }
            else if (selection == "[grey](No items in this directory)[/]")
            {
                continue;
            }
            else if (selection.StartsWith("📄 "))
            {
                var fileName = selection.Substring(3); // Emoji is 2 chars + space = 3
                return Path.Combine(currentPath, fileName);
            }
            else if (selection.StartsWith("📁 "))
            {
                var dirName = selection.Substring(3); // Emoji is 2 chars + space = 3
                currentPath = Path.Combine(currentPath, dirName);
            }
        }
    }

    public static string GenerateOutputPath(string path)
    {
        return "output/" + path;
    }
}