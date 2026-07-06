using Funcy.Console.Ui.Input;
using Funcy.Tests.TestSupport;
using Xunit;

namespace Funcy.Tests.Input;

public class SettingEditManagerTests
{
    private static SettingEditManager Editing(string key, string value)
    {
        var manager = new SettingEditManager();
        manager.Begin(key, value);
        return manager;
    }

    [Fact]
    public void GetMarkup_PrefixesEditLabelWithSettingName()
    {
        var manager = Editing("TagColumns", "System");

        var text = MarkupText.Plain(manager.GetMarkup(null));

        // The label must make it obvious the user is editing a named setting, not filtering.
        Assert.StartsWith("Edit TagColumns:", text);
        Assert.Contains("System", text);
    }

    [Fact]
    public void GetMarkup_AppendsErrorSuffix()
    {
        var manager = Editing("SubscriptionRefreshIntervalMinutes", "-5");

        var text = MarkupText.Plain(manager.GetMarkup("must be >= 0"));

        Assert.StartsWith("Edit SubscriptionRefreshIntervalMinutes:", text);
        Assert.Contains("must be >= 0", text);
    }
}
