using Funcy.Core.Model;
using Funcy.Infrastructure.Azure;
using Funcy.Infrastructure.Azure.Models;

namespace Funcy.Demo;

/// <summary>Answers the resource-graph lookups from the demo estate. Only the "does this
/// subscription have any function apps" probe is on a live demo path (it decides which
/// subscriptions are hidden as empty); the rest are implemented so no code path can fall through to
/// the az CLI.</summary>
internal sealed class DemoAzureResourceService(DemoEstate estate) : IAzureResourceService
{
    public Task<string> GetCurrentSubscriptionId(CancellationToken cancellationToken = default) =>
        Task.FromResult(estate.CurrentSubscriptionId);

    public Task<List<FunctionAppGraphRow>> GetAllFunctionApps(string subscriptionId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(estate.AppsFor(subscriptionId)
            .Select(app => new FunctionAppGraphRow(
                app.Id,
                app.Name,
                app.State == FunctionState.Stopped ? "Stopped" : "Running",
                app.Tags,
                app.ResourceGroup,
                app.Subscription))
            .ToList());

    public Task<bool> HasAnyFunctionAppsAsync(string subscriptionId, CancellationToken cancellationToken = default) =>
        Task.FromResult(estate.HasApps(subscriptionId));

    public Task<IReadOnlyList<(string Id, string Name)>> GetServiceBusNamespacesAsync(string subscriptionId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<(string, string)>>([(DemoNamespaceId, "sb-integration-prd")]);

    public Task<string?> GetServiceBusNamespaceIdAsync(string namespaceName,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<string?>(DemoNamespaceId);

    private const string DemoNamespaceId =
        "/subscriptions/" + DemoDataset.ProductionSubscriptionId +
        "/resourceGroups/rg-integration-prd/providers/Microsoft.ServiceBus/namespaces/sb-integration-prd";
}
