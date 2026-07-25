using Spectre.Console;
using UEFA.Rag.Indexer.Models;
using UEFA.Rag.Indexer.Services;

Console.Title = "UEFA RAG Indexer";

// Ctrl+C → graceful exit
Console.CancelKeyPress += (sender, e) =>
{
    e.Cancel = true;
    Console.ResetColor();
    AnsiConsole.MarkupLine("\n[green]Goodbye![/]");
    Environment.Exit(0);
};

var cmdArgs = Environment.GetCommandLineArgs();

if (cmdArgs.Length > 1)
{
    switch (cmdArgs[1].ToLowerInvariant())
    {
        case "run" when cmdArgs.Length > 2:
            await RunAsync(cmdArgs[2]);
            return;
        case "list":
            await ListAsync();
            return;
        case "clean":
            await CleanAsync();
            return;
        case "delete-collection":
            await CleanAsync();
            return;
    }
}

while (true)
{
    // Check for Escape before showing the menu
    if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
    {
        AnsiConsole.MarkupLine("[green]Goodbye![/]");
        return;
    }

    AnsiConsole.Write(new Rule("[yellow]UEFA RAG Indexer[/]").RuleStyle("grey"));
    AnsiConsole.WriteLine();

    var choice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("What would you like to do?")
            .PageSize(10)
            .MoreChoicesText("[grey](Move up and down to reveal more options)[/]")
            .AddChoices([
                "Run    - Index a repository folder",
                "List   - List all Qdrant collections",
                "Clean  - Delete the Qdrant collection",
                "Logs   - View live log stream",
                "Exit"
            ])
            .HighlightStyle(new Style(foreground: Color.Cyan1, decoration: Decoration.Bold)));

    switch (choice)
    {
        case "Run    - Index a repository folder":
            await RunMenuAsync();
            break;
        case "List   - List all Qdrant collections":
            await ListMenuAsync();
            break;
        case "Clean  - Delete the Qdrant collection":
            await CleanMenuAsync();
            break;
        case "Logs   - View live log stream":
            await LogStreamMenuAsync();
            break;
        case "Exit":
            AnsiConsole.MarkupLine("[green]Goodbye![/]");
            return;
    }
}

static async Task RunAsync(string folder)
{
    if (!Directory.Exists(folder))
    {
        AnsiConsole.MarkupLine($"[red]Folder not found:[/] {folder}");
        return;
    }

    var indexer = new IndexingService();

    await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .SpinnerStyle(Style.Parse("green"))
        .StartAsync("Indexing repository...", async ctx =>
        {
            ctx.Status = "Indexing repository...";
            await indexer.IndexAsync(folder);
            ctx.Status = "Indexing completed!";
        });
}

static async Task ListAsync()
{
    var qdrant = new QdrantService();

    var collections = await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .SpinnerStyle(Style.Parse("green"))
        .StartAsync("Fetching collections...", async ctx =>
        {
            return await qdrant.ListCollectionsAsync();
        });

    AnsiConsole.Write(new Rule("[yellow]Qdrant Collections[/]").RuleStyle("grey"));
    AnsiConsole.WriteLine();

    if (collections.Count == 0)
    {
        AnsiConsole.MarkupLine("[grey](no collections found)[/]");
    }
    else
    {
        var table = new Table().AddColumn("Name");
        foreach (var name in collections)
        {
            table.AddRow(name);
        }
        AnsiConsole.Write(table);
    }

    AnsiConsole.WriteLine();
}

static async Task CleanAsync()
{
    var qdrant = new QdrantService();

    await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .SpinnerStyle(Style.Parse("red"))
        .StartAsync("Deleting collection...", async ctx =>
        {
            await qdrant.DeleteCollectionAsync();
        });
}

static async Task RunMenuAsync()
{
    AnsiConsole.Write(new Rule("[yellow]Run Indexer[/]").RuleStyle("grey"));
    AnsiConsole.WriteLine();

    var folder = AnsiConsole.Ask<string>("Enter the [green]folder path[/] to index:");

    if (string.IsNullOrWhiteSpace(folder))
    {
        AnsiConsole.MarkupLine("[red]No folder provided.[/]");
        PressAnyKeyToContinue();
        return;
    }

    await RunAsync(folder);

    AnsiConsole.MarkupLine("[green]Indexing completed.[/]");
    PressAnyKeyToContinue();
}

static async Task ListMenuAsync()
{
    AnsiConsole.Write(new Rule("[yellow]List Collections[/]").RuleStyle("grey"));
    AnsiConsole.WriteLine();

    await ListAsync();

    PressAnyKeyToContinue();
}

static async Task CleanMenuAsync()
{
    AnsiConsole.Write(new Rule("[yellow]Clean Collection[/]").RuleStyle("grey"));
    AnsiConsole.WriteLine();

    var confirmed = AnsiConsole.Confirm("Are you sure you want to delete the Qdrant collection?");

    if (!confirmed)
    {
        AnsiConsole.MarkupLine("[yellow]Clean cancelled.[/]");
        PressAnyKeyToContinue();
        return;
    }

    await CleanAsync();

    AnsiConsole.MarkupLine("[green]Collection deleted.[/]");
    PressAnyKeyToContinue();
}

static async Task LogStreamMenuAsync()
{
    AnsiConsole.Write(new Rule("[yellow]Live Log Stream[/]").RuleStyle("grey"));
    AnsiConsole.WriteLine();

    var log = LogStream.Instance;

    // Optional: filter by minimum log level
    var minLevel = AnsiConsole.Prompt(
        new SelectionPrompt<LogLevel>()
            .Title("Filter by minimum log level?")
            .PageSize(5)
            .AddChoices(LogLevel.Debug, LogLevel.Info, LogLevel.Warning, LogLevel.Error)
            .HighlightStyle(new Style(foreground: Color.Cyan1, decoration: Decoration.Bold)));

    AnsiConsole.MarkupLine("[grey]Press [yellow]Q[/] or [yellow]Esc[/] to stop the log stream and return to menu.[/]");
    AnsiConsole.WriteLine();

    // Show existing buffered entries first
    var snapshot = log.GetFiltered(minLevel: minLevel);
    foreach (var entry in snapshot)
    {
        WriteLogEntry(entry, minLevel);
    }

    // Live subscription
    var cts = new CancellationTokenSource();
    var onEntry = (LogEntry entry) =>
    {
        if (entry.Level >= minLevel)
        {
            WriteLogEntry(entry, minLevel);
        }
    };

    log.OnEntry += onEntry;

    try
    {
        // Wait until user presses Q or Esc
        while (!cts.Token.IsCancellationRequested)
        {
            if (Console.KeyAvailable)
            {
                var key = Console.ReadKey(true);
                if (key.Key is ConsoleKey.Q or ConsoleKey.Escape)
                {
                    break;
                }
            }

            await Task.Delay(200, cts.Token);
        }
    }
    catch (OperationCanceledException)
    {
        // Expected when token is cancelled
    }
    finally
    {
        log.OnEntry -= onEntry;
    }

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine("[green]Log stream stopped.[/]");
    PressAnyKeyToContinue();
}

static void WriteLogEntry(LogEntry entry, LogLevel minLevel)
{
    if (entry.Level < minLevel)
        return;

    var color = entry.Level switch
    {
        LogLevel.Debug => "grey",
        LogLevel.Info => "white",
        LogLevel.Warning => "yellow",
        LogLevel.Error => "red",
        _ => "white"
    };

    var lines = entry.Formatted.Split('\n');
    foreach (var line in lines)
    {
        AnsiConsole.MarkupLine($"[{color}]{line.EscapeMarkup()}[/]");
    }
}

static void PressAnyKeyToContinue()
{
    AnsiConsole.MarkupLine("\n[grey]Press any key to return to menu, Esc to quit...[/]");
    try
    {
        var key = Console.ReadKey(true);
        if (key.Key == ConsoleKey.Escape)
        {
            AnsiConsole.MarkupLine("[green]Goodbye![/]");
            Environment.Exit(0);
        }
    }
    catch (InvalidOperationException)
    {
        Console.ReadLine();
    }
}