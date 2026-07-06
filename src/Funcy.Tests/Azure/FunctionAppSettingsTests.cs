using Funcy.Infrastructure.Azure;
using Xunit;

namespace Funcy.Tests.Azure;

public class FunctionAppSettingsTests
{
    [Fact]
    public void ApplyDisabledSetting_WhenDisabling_SetsFlagTrue()
    {
        var settings = new Dictionary<string, string>();

        FunctionAppSettings.ApplyDisabledSetting(settings, "ProcessPayment", true);

        Assert.Equal("true", settings["AzureWebJobs.ProcessPayment.Disabled"]);
    }

    [Fact]
    public void ApplyDisabledSetting_WhenEnabling_SetsFlagFalse()
    {
        var settings = new Dictionary<string, string>
        {
            ["AzureWebJobs.ProcessPayment.Disabled"] = "true"
        };

        FunctionAppSettings.ApplyDisabledSetting(settings, "ProcessPayment", false);

        Assert.Equal("false", settings["AzureWebJobs.ProcessPayment.Disabled"]);
    }

    [Fact]
    public void ApplyDisabledSetting_PreservesOtherSettings()
    {
        var settings = new Dictionary<string, string>
        {
            ["FUNCTIONS_WORKER_RUNTIME"] = "dotnet",
            ["AzureWebJobs.OtherFunction.Disabled"] = "true",
            ["SomeConnectionString"] = "value"
        };

        FunctionAppSettings.ApplyDisabledSetting(settings, "ProcessPayment", true);

        Assert.Equal("dotnet", settings["FUNCTIONS_WORKER_RUNTIME"]);
        Assert.Equal("true", settings["AzureWebJobs.OtherFunction.Disabled"]);
        Assert.Equal("value", settings["SomeConnectionString"]);
        Assert.Equal("true", settings["AzureWebJobs.ProcessPayment.Disabled"]);
        Assert.Equal(4, settings.Count);
    }

    [Fact]
    public void ApplyDisabledSetting_OnlyTouchesTargetFunction()
    {
        var settings = new Dictionary<string, string>
        {
            ["AzureWebJobs.FunctionA.Disabled"] = "false",
            ["AzureWebJobs.FunctionB.Disabled"] = "false"
        };

        FunctionAppSettings.ApplyDisabledSetting(settings, "FunctionA", true);

        Assert.Equal("true", settings["AzureWebJobs.FunctionA.Disabled"]);
        Assert.Equal("false", settings["AzureWebJobs.FunctionB.Disabled"]);
    }

    [Fact]
    public void DisabledKey_UsesAzureWebJobsConvention()
    {
        Assert.Equal("AzureWebJobs.MyFunc.Disabled", FunctionAppSettings.DisabledKey("MyFunc"));
    }
}
