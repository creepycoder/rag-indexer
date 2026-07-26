namespace UEFA.Rag.Indexer.Services;

public interface IEmbeddingService
{
    Task<float[]> CreateAsync(string text);
}