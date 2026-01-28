using Spectre.Console;
using SwiftlyS2.Codegen.CS2.Generators;

namespace SwiftlyS2.Codegen.CS2;

public static class GeneratorOptions
{
    public const string GameId = "CS2";

    /// <summary>
    /// Dictionary of available generators mapped by their display name
    /// </summary>
    private static readonly Dictionary<string, Func<string?, string?, BaseGenerator>> GeneratorFactories = new()
    {
        { "Natives", (nativesPath, _) => new Natives(nativesPath) },
        { "Game Events", (_, gameEventsPath) => new GameEvents(gameEventsPath) }
    };

    public static async Task ShowGeneratorOptionsAsync(string? nativesPath, string? gameEventsPath)
    {
        var selectedGenerators = AnsiConsole.Prompt(
            new MultiSelectionPrompt<string>()
                .Title("Select the [green]generators[/] to run:")
                .PageSize(10)
                .MoreChoicesText("[grey](Move up and down to reveal more generators)[/]")
                .InstructionsText("[grey](Press [blue]<space>[/] to toggle a generator, [green]<enter>[/] to accept)[/]")
                .AddChoices(GeneratorFactories.Keys)
        );

        if (!selectedGenerators.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No generators selected.[/]");
            return;
        }

        // Prompt for paths if not provided
        if (selectedGenerators.Contains("Natives") && string.IsNullOrEmpty(nativesPath))
        {
            var defaultNativesPath = Path.Combine(Entrypoint.ProjectRootPath, "data", "natives");
            if (Directory.Exists(defaultNativesPath))
            {
                var useDefault = AnsiConsole.Confirm($"Use default natives path: [grey]{defaultNativesPath}[/]?", true);
                if (useDefault)
                {
                    nativesPath = defaultNativesPath;
                }
            }

            if (string.IsNullOrEmpty(nativesPath))
            {
                AnsiConsole.MarkupLine("[yellow]Please select the natives folder:[/]");
                nativesPath = Entrypoint.BrowseForDirectory("Browse for Natives Folder", Entrypoint.ProjectRootPath);
                AnsiConsole.MarkupLine($"[green]Selected natives path:[/] {nativesPath}");
            }
        }

        if (selectedGenerators.Contains("Game Events") && string.IsNullOrEmpty(gameEventsPath))
        {
            var defaultGameEventsPath = Path.Combine(Entrypoint.ProjectRootPath, "data", "gameevents");
            if (Directory.Exists(defaultGameEventsPath))
            {
                var useDefault = AnsiConsole.Confirm($"Use default game events path: [grey]{defaultGameEventsPath}[/]?", true);
                if (useDefault)
                {
                    gameEventsPath = defaultGameEventsPath;
                }
            }

            if (string.IsNullOrEmpty(gameEventsPath))
            {
                AnsiConsole.MarkupLine("[yellow]Please select the game events folder:[/]");
                gameEventsPath = Entrypoint.BrowseForDirectory("Browse for Game Events Folder", Entrypoint.ProjectRootPath);
                AnsiConsole.MarkupLine($"[green]Selected game events path:[/] {gameEventsPath}");
            }
        }

        AnsiConsole.WriteLine();
        var startTime = DateTime.Now;

        var generators = selectedGenerators.ToDictionary(
            name => name,
            name => GeneratorFactories[name](nativesPath, gameEventsPath)
        );

        var generatorStatus = new Dictionary<string, GeneratorState>();
        foreach (var name in selectedGenerators)
        {
            generatorStatus[name] = new GeneratorState { Status = ExecutionStatus.Running };
        }

        await AnsiConsole.Live(CreateStatusTable(generatorStatus, startTime))
            .StartAsync(async ctx =>
            {
                var generatorTasks = selectedGenerators.Select(async name =>
                {
                    var generator = generators[name];
                    var result = await generator.GenerateFilesAsync();
                    generatorStatus[name] = new GeneratorState
                    {
                        Status = result.Success ? ExecutionStatus.Completed : ExecutionStatus.Failed,
                        Result = result,
                        StatusHistory = generator.Progress.History.ToList(),
                        CurrentStatus = generator.Progress.CurrentStatus
                    };
                    ctx.UpdateTarget(CreateStatusTable(generatorStatus, startTime));
                    return (name, result);
                }).ToArray();

                var updateTask = Task.Run(async () =>
                {
                    while (generatorStatus.Values.Any(s => s.Status == ExecutionStatus.Running))
                    {
                        await Task.Delay(100);

                        // Update status history from generators
                        foreach (var name in selectedGenerators)
                        {
                            if (generatorStatus[name].Status == ExecutionStatus.Running)
                            {
                                var generator = generators[name];
                                generatorStatus[name].StatusHistory = generator.Progress.History.ToList();
                                generatorStatus[name].CurrentStatus = generator.Progress.CurrentStatus;
                            }
                        }

                        ctx.UpdateTarget(CreateStatusTable(generatorStatus, startTime));
                    }
                });

                var results = await Task.WhenAll(generatorTasks);
                await updateTask;

                ctx.UpdateTarget(CreateStatusTable(generatorStatus, startTime));
            });

        AnsiConsole.WriteLine();
        var failedGenerators = generatorStatus.Where(kvp => kvp.Value.Status == ExecutionStatus.Failed).ToList();

        if (failedGenerators.Any())
        {
            AnsiConsole.MarkupLine("[red]Errors occurred during generation:[/]");
            AnsiConsole.WriteLine();

            foreach (var (name, state) in failedGenerators)
            {
                AnsiConsole.MarkupLine($"[red]✗ {name}:[/]");
                if (state.Result?.ErrorMessage != null)
                {
                    AnsiConsole.MarkupLine($"  [grey]{state.Result.ErrorMessage.EscapeMarkup()}[/]");
                }
                if (state.Result?.Exception != null)
                {
                    AnsiConsole.WriteException(state.Result.Exception);
                }
                AnsiConsole.WriteLine();
            }
        }
        else
        {
            var elapsed = DateTime.Now - startTime;
            AnsiConsole.MarkupLine($"[green]✓ All generators completed successfully in {elapsed.TotalSeconds:F2}s![/]");
        }
    }

    private static Table CreateStatusTable(Dictionary<string, GeneratorState> generatorStatus, DateTime startTime)
    {
        var elapsed = DateTime.Now - startTime;
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[bold]Generator[/]").LeftAligned())
            .AddColumn(new TableColumn("[bold]Status[/]").LeftAligned())
            .AddColumn(new TableColumn("[bold]Progress[/]").LeftAligned());

        table.Title = new TableTitle($"[bold]Generating files... (Elapsed: {elapsed.TotalSeconds:F1}s)[/]");

        bool first = true;
        foreach (var (name, state) in generatorStatus)
        {
            if (!first)
            {
                table.AddEmptyRow();
            }
            first = false;

            var statusIcon = state.Status switch
            {
                ExecutionStatus.Running => "[yellow]⏳[/]",
                ExecutionStatus.Completed => "[green]✓[/]",
                ExecutionStatus.Failed => "[red]✗[/]",
                _ => "[grey]○[/]"
            };

            var statusText = state.Status switch
            {
                ExecutionStatus.Running => "[yellow]Running[/]",
                ExecutionStatus.Completed => "[green]Completed[/]",
                ExecutionStatus.Failed => "[red]Failed[/]",
                _ => "[grey]Pending[/]"
            };

            // Get the last 3 status messages for display
            var recentHistory = state.StatusHistory.TakeLast(3).ToList();
            var progressText = recentHistory.Any()
                ? string.Join("\n", recentHistory.Select(h => $"[grey]{h.EscapeMarkup()}[/]"))
                : "[grey]No progress yet[/]";

            table.AddRow(
                $"{statusIcon} {name}",
                statusText,
                progressText
            );
        }

        return table;
    }

    private enum ExecutionStatus
    {
        Pending,
        Running,
        Completed,
        Failed
    }

    private class GeneratorState
    {
        public ExecutionStatus Status { get; set; }
        public GeneratorResult? Result { get; set; }
        public List<string> StatusHistory { get; set; } = new();
        public string CurrentStatus { get; set; } = "Initializing...";
    }
}