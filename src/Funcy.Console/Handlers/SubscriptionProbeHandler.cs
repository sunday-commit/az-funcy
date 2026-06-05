using Funcy.Core.Model;
using Funcy.Infrastructure.Azure;
using Microsoft.Extensions.Logging;

namespace Funcy.Console.Handlers;

public class SubscriptionProbeHandler(
    IAzureResourceService azureResourceService,
    AppContext appContext,
    ILogger<SubscriptionProbeHandler> logger)
{
    public async Task ProbeAllSubscriptionsAsync(CancellationToken token)
    {
        var subscriptions = appContext.GetUnprobedSubscriptions();
        logger.LogInformation("Probing {Count} subscriptions", subscriptions.Count);

        using var throttler = new SemaphoreSlim(8, 8);
        var tasks = subscriptions.Select(sub => ProbeSubscriptionAsync(sub, throttler, token));
        await Task.WhenAll(tasks);

        logger.LogInformation("Subscription probe complete");
    }

    private async Task ProbeSubscriptionAsync(SubscriptionDetails sub, SemaphoreSlim throttler, CancellationToken token)
    {
        await throttler.WaitAsync(token);
        try
        {
            var hasApps = await azureResourceService.HasAnyFunctionAppsAsync(sub.Id);
            await appContext.RecordProbeResultAsync(sub.Id, hasApps);
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to probe '{Name}' ({SubscriptionId})", sub.Name, sub.Id);
        }
        finally
        {
            throttler.Release();
        }
    }
}
