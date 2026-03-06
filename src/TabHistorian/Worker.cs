using TabHistorian.Services;

namespace TabHistorian;

public class Worker(SnapshotService snapshotService, StorageService storage, TabTrackingService tabTracking, IHostApplicationLifetime lifetime, ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Yield to let the host finish starting before we run and stop
        await Task.Yield();

        try
        {
            storage.BackupDatabase();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Backup failed");
        }

        // Retroactively process existing snapshots that predate tab tracking
        try
        {
            tabTracking.ProcessExistingSnapshots();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Retroactive tab tracking failed");
        }

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

        logger.LogInformation("All tasks complete, shutting down");
        lifetime.StopApplication();
    }
}
