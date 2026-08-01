using Rag.Indexer.Services;

namespace Rag.Indexer.Worker;

/// <summary>
/// Runs the delta-indexing pipeline continuously for the configured repositories.
/// An initial index runs on startup; subsequent passes only reprocess changed files,
/// so keeping the worker running keeps the Qdrant index fresh.
/// </summary>
public sealed class IndexerWorker : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IConfiguration _config;
    private readonly ILogger<IndexerWorker> _logger;

    public IndexerWorker(
        IServiceProvider services,
        IConfiguration config,
        ILogger<IndexerWorker> logger)
    {
        _services = services;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var repositories = _config.GetSection("Indexing:Repositories").Get<string[]>() ?? [];
        var intervalSeconds = _config.GetValue<double>("Indexing:IntervalSeconds", 300);

        if (repositories.Length == 0)
        {
            _logger.LogWarning(
                "No repositories configured under Indexing:Repositories. " +
                "Set Indexing__Repositories__0 (etc.) or appsettings.json to enable continuous indexing.");
            return;
        }

        _logger.LogInformation(
            "Indexer worker started for {Count} repository(ies), re-checking every {Interval}s.",
            repositories.Length, intervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var repository in repositories)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                if (!Directory.Exists(repository))
                {
                    _logger.LogWarning("Repository folder not found, skipping: {Path}", repository);
                    continue;
                }

                try
                {
                    using var scope = _services.CreateScope();
                    var indexing = scope.ServiceProvider.GetRequiredService<IndexingService>();
                    await indexing.IndexAsync(repository);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Indexing failed for {Path}", repository);
                }
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
