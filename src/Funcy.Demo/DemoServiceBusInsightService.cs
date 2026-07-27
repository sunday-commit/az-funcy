using Funcy.Core.Interfaces;
using Funcy.Core.Model;

namespace Funcy.Demo;

/// <summary>Resolves the demo estate's queue contents. One app is deliberately left unresolvable so
/// the failed-count state is visible too: it is a state users hit for real, usually on a namespace
/// they lack access to.</summary>
internal sealed class DemoServiceBusInsightService(DemoEstate estate) : IServiceBusInsightService
{
    private const string UnresolvableApp = "func-shipments-tracker-prd";

    public async Task<IReadOnlyList<ServiceBusCountResult>> GetCountsAsync(
        string functionAppArmId,
        IReadOnlyList<FunctionDetails> serviceBusFunctions,
        CancellationToken cancellationToken)
    {
        if (serviceBusFunctions.Count == 0)
        {
            return [];
        }

        await Task.Delay(DemoLatency.PerAppCounts, cancellationToken);

        var app = estate.TryGetApp(functionAppArmId);
        var namespaceId =
            $"/subscriptions/{DemoDataset.ProductionSubscriptionId}/resourceGroups/rg-integration-prd" +
            "/providers/Microsoft.ServiceBus/namespaces/sb-integration-prd";

        return serviceBusFunctions
            .Select(function =>
            {
                if (app?.Name == UnresolvableApp)
                {
                    // Kept short enough to fit the Issues panel's message column unabridged.
                    return new ServiceBusCountResult(function.Key, null, null, Success: false,
                        ErrorMessage: "Service Bus counts: no Reader access on sb-shipments-prd");
                }

                var counts = estate.CountsFor(function.Key);
                if (counts is null)
                {
                    return new ServiceBusCountResult(function.Key, null, null, Success: false);
                }

                return new ServiceBusCountResult(
                    function.Key,
                    counts.Value.Active,
                    counts.Value.DeadLettered,
                    Success: true,
                    QueueName: function.QueueName,
                    TopicName: function.TopicName,
                    SubscriptionName: function.SubscriptionName,
                    NamespaceId: namespaceId);
            })
            .ToList();
    }
}
