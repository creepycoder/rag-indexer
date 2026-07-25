using UEFA.Rag.Indexer.Services;

Console.Title = "UEFA RAG Indexer";

static void ClearScreen()
{
    try
    {
        Console.Clear();
    }
    catch
    {
        // Some terminals (e.g. VS Code debug console) don't support Clear()
        Console.WriteLine();
    }
}

static void WaitForKey()
{
    try
    {
        Console.ReadKey();
    }
    catch (InvalidOperationException)
    {
        Console.ReadLine();
    }
}

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
    ClearScreen();
    Console.WriteLine("=== UEFA RAG Indexer ===");
    Console.WriteLine();
    Console.WriteLine("1. Run    - Index a repository folder");
    Console.WriteLine("2. List   - List all Qdrant collections");
    Console.WriteLine("3. Clean  - Delete the Qdrant collection");
    Console.WriteLine("4. Exit");
    Console.WriteLine();
    Console.Write("Choose an option (1-4): ");

    string input;
    try
    {
        var key = Console.ReadKey(intercept: true);
        Console.WriteLine();
        input = key.KeyChar.ToString().ToLowerInvariant();
    }
    catch (InvalidOperationException)
    {
        // Fallback for redirected input or environments without a console
        input = Console.ReadLine()?.Trim().ToLowerInvariant() ?? "";
    }

    switch (input)
    {
        case "1":
        case "run":
            await RunMenuAsync();
            break;
        case "2":
        case "list":
            await ListMenuAsync();
            break;
        case "3":
        case "clean":
            await CleanMenuAsync();
            break;
        case "4":
        case "exit":
            Console.WriteLine("Goodbye!");
            return;
        default:
            Console.WriteLine("Invalid option. Press any key to try again...");
            WaitForKey();
            break;
    }
}

static async Task RunAsync(string folder)
{
    if (!Directory.Exists(folder))
    {
        Console.WriteLine($"Folder not found: {folder}");
        return;
    }

    var indexer = new IndexingService();
    await indexer.IndexAsync(folder);
}

static async Task ListAsync()
{
    var qdrant = new QdrantService();
    await qdrant.ListCollectionsAsync();
}

static async Task CleanAsync()
{
    var qdrant = new QdrantService();
    await qdrant.DeleteCollectionAsync();
}

static async Task RunMenuAsync()
{
    ClearScreen();
    Console.WriteLine("=== Run Indexer ===");
    Console.WriteLine();
    Console.Write("Enter the folder path to index: ");
    var folder = Console.ReadLine()?.Trim();

    if (string.IsNullOrWhiteSpace(folder))
    {
        Console.WriteLine("No folder provided. Press any key to return...");
        WaitForKey();
        return;
    }

    await RunAsync(folder);

    Console.WriteLine();
    Console.WriteLine("Indexing completed. Press any key to return to menu...");
    WaitForKey();
}

static async Task ListMenuAsync()
{
    ClearScreen();
    Console.WriteLine("=== List Collections ===");
    Console.WriteLine();

    await ListAsync();

    Console.WriteLine();
    Console.WriteLine("Press any key to return to menu...");
    WaitForKey();
}

static async Task CleanMenuAsync()
{
    ClearScreen();
    Console.WriteLine("=== Clean Collection ===");
    Console.WriteLine();
    Console.Write("Are you sure you want to delete the Qdrant collection? (y/N): ");
    var confirm = Console.ReadLine()?.Trim().ToLowerInvariant();

    if (confirm is not ("y" or "yes"))
    {
        Console.WriteLine("Clean cancelled. Press any key to return...");
        WaitForKey();
        return;
    }

    await CleanAsync();

    Console.WriteLine();
    Console.WriteLine("Press any key to return to menu...");
    WaitForKey();
}
