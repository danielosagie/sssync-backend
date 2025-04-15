using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using sssync_backend.core.Interfaces;
using sssync_backend.core.Interfaces.Repositories; // <-- Add repository interfaces

namespace sssync_backend.workers;

public class SyncBackgroundService : BackgroundService
{
    private readonly ILogger<SyncBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider; // Use IServiceProvider to scope dependencies per run
    private readonly TimeSpan _period = TimeSpan.FromMinutes(15); // How often to run the sync - make configurable?

    public SyncBackgroundService(ILogger<SyncBackgroundService> logger, IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SyncBackgroundService starting.");

        // Optional: Add a delay before the first run?
        // await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        // Run once immediately at startup, then periodically
        if (!stoppingToken.IsCancellationRequested)
        {
            await DoWorkAsync(stoppingToken);
        }

        using var timer = new PeriodicTimer(_period);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await DoWorkAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
             _logger.LogInformation("SyncBackgroundService stopping gracefully (timer cancelled).");
        }
        catch (Exception ex)
        {
             _logger.LogCritical(ex, "SyncBackgroundService stopped due to unhandled exception in timer loop.");
        }

        _logger.LogInformation("SyncBackgroundService stopped.");
    }

    private async Task DoWorkAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("SyncBackgroundService executing work cycle at {Time}", DateTimeOffset.UtcNow);

        try
        {
            // Create a scope to resolve scoped services like ISyncService and DbContexts/Repositories
            using var scope = _serviceProvider.CreateScope();
            var syncService = scope.ServiceProvider.GetRequiredService<ISyncService>();
            var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>(); // <-- Get User Repo

            // --- Fetch Users to Sync ---
            var userIdsToSync = await userRepository.GetUserIdsWithActiveSyncAsync(stoppingToken);
             _logger.LogWarning("Using placeholder list of users to sync. Implement database fetch.");
             // Example: Add a test user ID if needed during development
             // userIdsToSync.Add("test-user-id-123");


             _logger.LogInformation("Found {UserCount} users potentially needing sync.", userIdsToSync.Count());

            // Consider running syncs in parallel if safe and beneficial
            // var tasks = userIdsToSync.Select(userId => Task.Run(async () => { ... }, stoppingToken));
            // await Task.WhenAll(tasks);

            foreach (var userId in userIdsToSync)
            {
                if (stoppingToken.IsCancellationRequested) break;

                try
                {
                    _logger.LogInformation("Attempting sync for user {UserId}", userId);
                    // The SynchronizeUserAsync method now contains the detailed steps
                    await syncService.SynchronizeUserAsync(userId, stoppingToken);
                    _logger.LogInformation("Finished sync attempt for user {UserId}", userId);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Sync cancelled for user {UserId} during execution.", userId);
                    break; // Exit loop if cancellation requested
                }
                catch (Exception ex)
                {
                    // Log specific exceptions from SynchronizeUserAsync if they bubble up
                    _logger.LogError(ex, "Unhandled exception during sync execution for user {UserId}.", userId);
                    // Potentially add logic to handle repeated failures for a user (e.g., disable sync after N failures)
                }
            }
        }
        catch (OperationCanceledException)
        {
             _logger.LogInformation("SyncBackgroundService work cycle cancelled.");
        }
        catch (Exception ex)
        {
            // Catch issues resolving services or other unexpected errors in the cycle
            _logger.LogError(ex, "Unhandled exception in SyncBackgroundService DoWorkAsync scope.");
        }

         _logger.LogInformation("SyncBackgroundService finished work cycle at {Time}", DateTimeOffset.UtcNow);
    }
} 