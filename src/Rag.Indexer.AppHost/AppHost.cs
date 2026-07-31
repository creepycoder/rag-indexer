var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.Rag_Indexer_Api>("rag-api");

builder.Build().Run();