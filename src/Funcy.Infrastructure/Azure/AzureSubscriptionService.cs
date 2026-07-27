using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Resources.Models;
using Funcy.Core.Model;

namespace Funcy.Infrastructure.Azure;

// Lists the subscriptions the signed-in identity can see, flagging the one az considers current.
public interface ISubscriptionService
{
    Task<List<SubscriptionDetails>> GetSubscriptions();
}

public class AzureSubscriptionService(IAzureResourceService azureResourceService, ArmClient client)
    : ISubscriptionService
{
    public async Task<List<SubscriptionDetails>> GetSubscriptions()
    {
        var currentSubscriptionId = await azureResourceService.GetCurrentSubscriptionId();
        var subscriptionDetailsList = new List<SubscriptionDetails>();
        var subscriptions = client.GetSubscriptions();

        await foreach (var subscription in subscriptions.GetAllAsync()
                           .Where(x => x.Data.State is not null && x.Data.State == SubscriptionState.Enabled))
        {
            var subscriptionId = subscription.Data.SubscriptionId;

            subscriptionDetailsList.Add(new SubscriptionDetails
            {
                Name = subscription.Data.DisplayName,
                Id = subscriptionId,
                Current = string.Equals(currentSubscriptionId, subscriptionId, StringComparison.OrdinalIgnoreCase),
            });
        }

        return subscriptionDetailsList;
    }
}