using UEFA.Rag.Indexer.Services;

Console.Title = "UEFA RAG Indexer";

while (true)
{
    Console.Clear();
    Console.WriteLine("=== UEFA RAG Indexer ===");
    Console.WriteLine();
    Console.WriteLine("1. Run    - Index a repository folder");
    Console.WriteLine("2. Clean  - Delete the Qdrant collection");
    Console.WriteLine("3. Exit");
    Console.WriteLine();
    Console.Write("Choose an option (1-3): ");

    var input = Console.ReadLine()?.Trim().ToLowerInvariant();

    switch (input)
    {
        case "1":
        case "run":
            await RunMenuAsync();
            break;
        case "2":
        case "clean":
            await CleanMenuAsync();
            break;
        case "3":
        case "exit":
            Console.WriteLine("Goodbye!");
            return;
        default:
            Console.WriteLine("Invalid option. Press any key to try again...");
            Console.ReadKey();
            break;
    }
}

static async Task RunMenuAsync()
{
    Console.Clear();
    Console.WriteLine("=== Run Indexer ===");
    Console.WriteLine();
    Console.Write("Enter the folder path to index: ");
    var folder = Console.ReadLine()?.Trim();

    if (string.IsNullOrWhiteSpace(folder))
    {
        Console.WriteLine("No folder provided. Press any key to return...");
        Console.ReadKey();
        return;
    }

    if (!Directory.Exists(folder))
    {
        Console.WriteLine($"Folder not found: {folder}");
        Console.WriteLine("Press any key to return...");
        Console.ReadKey();
        return;
    }

    Console.WriteLine();
    var indexer = new IndexingService();
    await indexer.IndexAsync(folder);

    Console.WriteLine();
    Console.WriteLine("Indexing completed. Press any key to return to menu...");
    Console.ReadKey();
}

static async Task CleanMenuAsync()
{
    Console.Clear();
    Console.WriteLine("=== Clean Collection ===");
    Console.WriteLine();
    Console.Write("Are you sure you want to delete the Qdrant collection? (y/N): ");
    var confirm = Console.ReadLine()?.Trim().ToLowerInvariant();

    if (confirm is not ("y" or "yes"))
    {
        Console.WriteLine("Clean cancelled. Press any key to return...");
        Console.ReadKey();
        return;
    }

    Console.WriteLine();
    var qdrant = new QdrantService();
    await qdrant.DeleteCollectionAsync();

    Console.WriteLine();
    Console.WriteLine("Press any key to return to menu...");
    Console.ReadKey();
}