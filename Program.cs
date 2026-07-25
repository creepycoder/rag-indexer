using Spectre.Console;
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

    await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .SpinnerStyle(Style.Parse("green"))
        .StartAsync("Fetching collections...", async ctx =>
        {
            await qdrant.ListCollectionsAsync();
        });
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
