using Qdrant.Client;

namespace Rag.Indexer.Services;

/// <summary>
/// Resolves Qdrant connection settings from the environment.
///
/// Supports both standalone runs (<c>QDRANT_HOST</c> / <c>QDRANT_PORT</c> / <c>QDRANT_REST_PORT</c>)
/// and the variables Aspire injects via <c>WithReference</c> on a Qdrant resource
/// (<c>QDRANT_GRPCHOST</c> / <c>QDRANT_GRPCPORT</c> / <c>QDRANT_HTTPHOST</c> / <c>QDRANT_HTTPPORT</c> / <c>QDRANT_APIKEY</c>).
/// </summary>
public static class QdrantConnection
{
    public static string GrpcHost =>
        Environment.GetEnvironmentVariable("QDRANT_GRPCHOST")
        ?? Environment.GetEnvironmentVariable("QDRANT_HOST")
        ?? "localhost";

    public static int GrpcPort =>
        int.TryParse(
            Environment.GetEnvironmentVariable("QDRANT_GRPCPORT")
            ?? Environment.GetEnvironmentVariable("QDRANT_PORT"),
            out var grpcPort)
            ? grpcPort
            : 6334;

    public static string HttpHost =>
        Environment.GetEnvironmentVariable("QDRANT_HTTPHOST")
        ?? Environment.GetEnvironmentVariable("QDRANT_HOST")
        ?? "localhost";

    public static int HttpPort =>
        int.TryParse(
            Environment.GetEnvironmentVariable("QDRANT_HTTPPORT")
            ?? Environment.GetEnvironmentVariable("QDRANT_REST_PORT"),
            out var httpPort)
            ? httpPort
            : 6333;

    public static string ApiKey =>
        Environment.GetEnvironmentVariable("QDRANT_APIKEY") ?? "";

    public static QdrantClient CreateClient() =>
        new(host: GrpcHost, port: GrpcPort, https: false, apiKey: ApiKey);
}
