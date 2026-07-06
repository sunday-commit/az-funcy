using Funcy.Core.Model;
using Funcy.Data.Entities;
using Funcy.Infrastructure.Mappers;
using Xunit;

namespace Funcy.Tests.Mappers;

public class FunctionAppDetailsMapperTests
{
    private static FunctionApp MakeEntity(bool isPinned) =>
        new()
        {
            AzureId = "/subscriptions/sub-1/sites/appA",
            Name = "appA",
            ResourceGroup = "rg",
            Subscription = "sub-1",
            State = FunctionState.Running,
            IsPinned = isPinned,
            UpdatedAt = DateTime.UtcNow
        };

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Map_CarriesIsPinnedFromEntity(bool isPinned)
    {
        var details = MakeEntity(isPinned).Map();

        Assert.Equal(isPinned, details.IsPinned);
    }
}
