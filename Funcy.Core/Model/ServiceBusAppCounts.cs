namespace Funcy.Core.Model;

// Aggregated Service Bus message counts across a function app's Service Bus-triggered functions.
// HasServiceBusFunctions is false when the app has no SB triggers at all; AllLoaded is true only
// when every SB-triggered function has successfully resolved counts (partial fetches and failures
// leave it false so callers can render an "not yet available" state).
public readonly record struct ServiceBusAppCounts(
    long ActiveMessages,
    long DeadLetteredMessages,
    bool HasServiceBusFunctions,
    bool AllLoaded);

// Pure aggregation of per-function Service Bus counts up to the owning function app.
public static class ServiceBusCountAggregator
{
    public static ServiceBusAppCounts Aggregate(FunctionAppDetails app)
    {
        long active = 0;
        long dlq = 0;
        var hasServiceBus = false;
        var allLoaded = true;

        foreach (var function in app.Functions)
        {
            if (!function.IsServiceBusTrigger)
            {
                continue;
            }

            hasServiceBus = true;

            if (function.CountStatus == ServiceBusCountStatus.Loaded)
            {
                active += function.ActiveMessages ?? 0;
                dlq += function.DeadLetteredMessages ?? 0;
            }
            else
            {
                // Loading, Failed or never fetched: the sum is incomplete.
                allLoaded = false;
            }
        }

        return new ServiceBusAppCounts(active, dlq, hasServiceBus, hasServiceBus && allLoaded);
    }
}
