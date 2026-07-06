using Funcy.Data.Entities;
using Funcy.Infrastructure.Mappers;
using Xunit;

namespace Funcy.Tests.Mappers;

public class FunctionDetailsMapperTests
{
    private static Function MakeFunction(bool isDisabled) => new()
    {
        AzureId = "azure-id",
        Name = "ProcessPayment",
        Trigger = "HttpTrigger",
        IsDisabled = isDisabled,
        FunctionApp = new FunctionApp
        {
            AzureId = "app-azure-id",
            Name = "appA",
            ResourceGroup = "rg",
            Subscription = "sub-1"
        }
    };

    [Fact]
    public void Map_CarriesDisabledState_WhenDisabled()
    {
        var result = MakeFunction(isDisabled: true).Map();

        Assert.True(result.IsDisabled);
        Assert.Equal("ProcessPayment", result.Name);
        Assert.Equal("HttpTrigger", result.Trigger);
        Assert.Equal("appA", result.FunctionAppName);
    }

    [Fact]
    public void Map_CarriesEnabledState_WhenNotDisabled()
    {
        var result = MakeFunction(isDisabled: false).Map();

        Assert.False(result.IsDisabled);
    }
}
