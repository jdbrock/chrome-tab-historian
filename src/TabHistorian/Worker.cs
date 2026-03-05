using TabHistorian.Services;

namespace TabHistorian;

public class Worker(SnapshotService snapshotService, StorageService storage, IHostApplicationLifetime lifetime, ILogger<Worker> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("TabHistorian taking snapshot...");
            snapshotService.TakeSnapshot();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Snapshot failed");
        }

        try
        {
            storage.PruneSnapshots();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Pruning failed");
        }

        try
        {
            storage.BackupDatabase();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Backup failed");
        }

        lifetime.StopApplication();
        return Task.CompletedTask;
    }
}
