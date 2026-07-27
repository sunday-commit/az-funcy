using System.Runtime.CompilerServices;
using Funcy.Core.Interfaces;
using Funcy.Core.Model;

namespace Funcy.Demo;

/// <summary>Serves the demo estate through the same streaming contract the Azure service uses, so
/// the inventory pass, the details pass and the per-app refresh all behave as they do live.</summary>
internal sealed class DemoFunctionService(DemoEstate estate) : IAzureFunctionService
{
    public Task<List<FunctionAppDetails>> GetFunctionsFromDatabase(string subscriptionId)
    {
        // Nothing is cached until the subscription's first inventory pass, mirroring an empty
        // SQLite cache on a first run.
        var apps = estate.HasSyncedInventory(subscriptionId)
            ? estate.AppsFor(subscriptionId)
            : [];
        return Task.FromResult(apps);
    }

    public async IAsyncEnumerable<FunctionAppFetchResult> GetFunctionAppDetailsAsync(string subscriptionId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.Delay(DemoLatency.Inventory, cancellationToken);

        foreach (var app in estate.AppsFor(subscriptionId))
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return new FunctionAppFetchResult(app.Name, app, FunctionAppUpdateKind.Inventory);
        }

        estate.MarkInventorySynced(subscriptionId);
    }

    public async IAsyncEnumerable<FunctionAppFetchResult> GetFunctionAppFunctionsAndSlotsAsync(
        List<FunctionAppDetails> functionAppDetails, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var app in functionAppDetails)
        {
            await Task.Delay(DemoLatency.PerAppDetails, cancellationToken);
            yield return new FunctionAppFetchResult(app.Name, app, FunctionAppUpdateKind.Details);
        }
    }

    public async Task<FunctionAppDetails> GetFunctionAppDetails(FunctionAppDetails functionAppDetails)
    {
        await Task.Delay(DemoLatency.PerAppDetails);
        return estate.TryGetApp(functionAppDetails.Id) ?? functionAppDetails;
    }

    public Task SetPinnedAsync(string azureId, bool isPinned)
    {
        estate.SetPinned(azureId, isPinned);
        return Task.CompletedTask;
    }

    public Task SaveServiceBusNamespacesAsync(string functionAppArmId,
        IReadOnlyList<(string FunctionName, string NamespaceId)> resolved) => Task.CompletedTask;
}
