using Spectre.Console;
using SwiftlyS2.Codegen.CS2.Generators;

namespace SwiftlyS2.Codegen.CS2;

public static class GeneratorOptions
{
    public const string GameId = "CS2";

    /// <summary>
    /// Dictionary of available generators mapped by their display name
    /// </summary>
    private static readonly Dictionary<string, BaseGenerator> Generators = new()
    {
        { "Natives", new Natives() }
    };

    public static async Task ShowGeneratorOptionsAsync()
    {
        var selectedGenerators = AnsiConsole.Prompt(
            new MultiSelectionPrompt<string>()
                .Title("Select the [green]generators[/] to run:")
                .PageSize(10)
                .MoreChoicesText("[grey](Move up and down to reveal more generators)[/]")
                .InstructionsText("[grey](Press [blue]<space>[/] to toggle a generator, [green]<enter>[/] to accept)[/]")
                .AddChoices(Generators.Keys)
        );

        if (!selectedGenerators.Any())
        {
            AnsiConsole.MarkupLine("[yellow]No generators selected.[/]");
            return;
        }

        AnsiConsole.WriteLine();
        var startTime = DateTime.Now;

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
                    var result = await Generators[name].GenerateFilesAsync();
                    generatorStatus[name] = new GeneratorState
                    {
                        Status = result.Success ? ExecutionStatus.Completed : ExecutionStatus.Failed,
                        Result = result
                    };
                    ctx.UpdateTarget(CreateStatusTable(generatorStatus, startTime));
                    return (name, result);
                }).ToArray();

                var updateTask = Task.Run(async () =>
                {
                    while (generatorStatus.Values.Any(s => s.Status == ExecutionStatus.Running))
                    {
                        await Task.Delay(100);
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
            .AddColumn(new TableColumn("[bold]Status[/]").Centered());

        table.Title = new TableTitle($"[bold]Generating files... (Elapsed: {elapsed.TotalSeconds:F1}s)[/]");

        foreach (var (name, state) in generatorStatus)
        {
            var statusText = state.Status switch
            {
                ExecutionStatus.Running => "[yellow]⏳ Running...[/]",
                ExecutionStatus.Completed => "[green]✓ Completed[/]",
                ExecutionStatus.Failed => "[red]✗ Failed[/]",
                _ => "[grey]Pending[/]"
            };

            table.AddRow(name, statusText);
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
    }
}