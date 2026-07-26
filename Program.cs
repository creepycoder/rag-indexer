using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using UEFA.Rag.Indexer.Models;
using UEFA.Rag.Indexer.Services;
using UEFA.Rag.Indexer.Services.Configuration;

Console.Title = "UEFA RAG Indexer";

IConfigurationRoot configuration;
try
{
    configuration = new ConfigurationBuilder()
        .SetBasePath(AppContext.BaseDirectory)
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
        .AddEnvironmentVariables()
        .Build();
}
catch (JsonException ex)
{
    AnsiConsole.MarkupLine($"[red]Invalid appsettings.json: {ex.Message}[/]");
    AnsiConsole.MarkupLine("[yellow]Falling back to environment variables and defaults.[/]");
    configuration = new ConfigurationBuilder()
        .AddEnvironmentVariables()
        .Build();
}

var embeddingOptions = configuration
    .GetSection(EmbeddingOptions.SectionName)
    .Get<EmbeddingOptions>() ?? new EmbeddingOptions();

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
            await RunAsync(cmdArgs[2], embeddingOptions);
            return;
        case "list":
            await ListAsync();
            return;
        case "info":
            await InfoAsync(cmdArgs.Length > 2 ? cmdArgs[2] : "uefa_code");
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
                "Info   - Collection details",
                "Clean  - Delete the Qdrant collection",
                "Exit"
            ])
            .HighlightStyle(new Style(foreground: Color.Cyan1, decoration: Decoration.Bold)));

    switch (choice)
    {
        case "Run    - Index a repository folder":
            await RunMenuAsync(embeddingOptions);
            break;
        case "List   - List all Qdrant collections":
            await ListMenuAsync();
            break;
        case "Info   - Collection details":
            await InfoMenuAsync();
            break;
        case "Clean  - Delete the Qdrant collection":
            await CleanMenuAsync();
            break;
        case "Exit":
            AnsiConsole.MarkupLine("[green]Goodbye![/]");
            return;
    }
}

static async Task RunAsync(string folder, EmbeddingOptions embeddingOptions)
{
    if (!Directory.Exists(folder))
    {
        AnsiConsole.MarkupLine($"[red]Folder not found:[/] {folder}");
        return;
    }

    var embeddingService = GetEmbeddingService(embeddingOptions);
    var indexer = new IndexingService(embeddingService);

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

static IEmbeddingService GetEmbeddingService(EmbeddingOptions options)
{
    try
    {
        return options.Provider.ToLowerInvariant() switch
        {
            "azure" => new AzureOpenAiEmbeddingService(options.AzureOpenAi),
            _ => new OllamaEmbeddingService(options.Ollama)
        };
    }
    catch (InvalidOperationException ex)
    {
        AnsiConsole.MarkupLine($"[red]ERROR: {ex.Message}[/]");
        AnsiConsole.MarkupLine("[yellow]Falling back to OllamaEmbeddingService.[/]");
        return new OllamaEmbeddingService(options.Ollama);
    }
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
        AnsiConsole.MarkupLine("[yellow]If Qdrant is not running, start it and try again.[/]");
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

    try
    {
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("red"))
            .StartAsync("Deleting collection...", async ctx =>
            {
                await qdrant.DeleteCollectionAsync();
            });
    }
    catch (Exception ex)
    {
        AnsiConsole.MarkupLine($"[red]Failed to delete collection: {ex.Message}[/]");
    }
}

static async Task RunMenuAsync(EmbeddingOptions embeddingOptions)
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

    await RunAsync(folder, embeddingOptions);

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

static async Task InfoMenuAsync()
{
    AnsiConsole.Write(new Rule("[yellow]Collection Info[/]").RuleStyle("grey"));
    AnsiConsole.WriteLine();

    var name = AnsiConsole.Ask("Collection name:", "uefa_code");
    await InfoAsync(name);

    PressAnyKeyToContinue();
}

static async Task InfoAsync(string collectionName)
{
    var qdrant = new QdrantService();

    var info = await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .SpinnerStyle(Style.Parse("green"))
        .StartAsync("Fetching collection info...", async ctx =>
        {
            return await qdrant.GetCollectionInfoAsync(collectionName);
        });

    if (info is null)
    {
        AnsiConsole.MarkupLine("[red]Could not retrieve collection info. Ensure Qdrant is running.[/]");
        return;
    }

    var table = new Table()
        .AddColumn("Property")
        .AddColumn("Value");

    table.AddRow("Name", collectionName);
    table.AddRow("Status", info.Status.ToString());
    table.AddRow("Points", info.PointsCount.ToString());
    table.AddRow("Segments", info.SegmentsCount.ToString());
    table.AddRow("Vectors (indexed)", info.IndexedVectorsCount.ToString());
    table.AddRow("Queue", info.UpdateQueue.ToString());

    AnsiConsole.Write(table);
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