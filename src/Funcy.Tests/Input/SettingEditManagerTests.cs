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

    [Fact]
    public void GetMarkup_WithSuggestions_ShowsAvailableHint()
    {
        var manager = Editing("TagColumns", "System");
        manager.SetSuggestions(["CostCenter", "Env", "System", "Team"]);

        var text = MarkupText.Plain(manager.GetMarkup(null));

        Assert.Contains("(available: CostCenter, Env, System, Team)", text);
    }

    [Fact]
    public void Begin_ResetsSuggestions()
    {
        var manager = Editing("TagColumns", "System");
        manager.SetSuggestions(["Team"]);
        manager.Begin("TagColumns", "System");

        Assert.DoesNotContain("available", MarkupText.Plain(manager.GetMarkup(null)));
    }

    [Fact]
    public void Tab_CompletesNextUnusedSuggestion()
    {
        var manager = Editing("TagColumns", "System");
        manager.SetSuggestions(["CostCenter", "Env", "System", "Team"]);

        manager.HandleInput(new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false));

        // "System" is already present (case-insensitive); the first unused key is appended.
        Assert.Equal("System, CostCenter", manager.Text);
    }

    [Fact]
    public void Tab_IntoEmptyValue_InsertsFirstSuggestion()
    {
        var manager = Editing("TagColumns", "");
        manager.SetSuggestions(["CostCenter", "Env"]);

        manager.HandleInput(new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false));

        Assert.Equal("CostCenter", manager.Text);
    }

    [Fact]
    public void Tab_WithoutSuggestions_DoesNothing()
    {
        var manager = Editing("TagColumns", "System");

        manager.HandleInput(new ConsoleKeyInfo('\t', ConsoleKey.Tab, false, false, false));

        Assert.Equal("System", manager.Text);
    }
}
