using TabHistorian.Services;

namespace TabHistorian;

public class Worker(SnapshotService snapshotService, ILogger<Worker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Take an initial snapshot on startup
        logger.LogInformation("TabHistorian starting, taking initial snapshot...");
        RunSnapshot();

        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            RunSnapshot();
        }
    }

    private void RunSnapshot()
    {
        try
        {
            snapshotService.TakeSnapshot();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Snapshot failed");
        }
    }
}
