using Funcy.Core.Model;

namespace Funcy.Console.Handlers;

// Decides which function apps the app-list Service Bus count fetch should target. Pure and static
// so the gating rules (disabled setting => nothing; enabled => only apps with SB triggers) are
// unit-testable without the full update handler.
public static class ServiceBusCountFetchPlanner
{
    public static IReadOnlyList<FunctionAppDetails> AppsToFetch(
        bool showServiceBusInAppList,
        IEnumerable<FunctionAppDetails> apps)
    {
        if (!showServiceBusInAppList)
        {
            return [];
        }

        return apps
            .Where(app => app.Functions.Any(f => f.IsServiceBusTrigger))
            .ToList();
    }
}
