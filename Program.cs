using UEFA.Rag.Indexer.Services;

var folder = args.Length > 0
    ? args[0]
    : @"C:\UEFA";

var indexer = new IndexingService();

await indexer.IndexAsync(folder);