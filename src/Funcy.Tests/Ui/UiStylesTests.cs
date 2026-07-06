using Funcy.Console.Ui;
using Funcy.Core.Model;
using Funcy.Tests.TestSupport;
using Spectre.Console;
using Xunit;

namespace Funcy.Tests.Ui;

public class UiStylesTests
{
    private static string Arrow(bool descending)
    {
        var unicode = AnsiConsole.Profile.Capabilities.Unicode;
        return descending ? (unicode ? "↓" : "v") : (unicode ? "↑" : "^");
    }

    // ---- CreateHeaderText (returns a raw markup string) ----

    [Fact]
    public void CreateHeaderText_NoIndex_JustBoldHeader()
        => Assert.Equal("[bold]Name[/]", UiStyles.CreateHeaderText("Name", null, false));

    [Fact]
    public void CreateHeaderText_WithIndex_Inactive_HasNoArrow_ButTrailingSpace()
        => Assert.Equal("[bold]Name[/][yellow](1) [/]", UiStyles.CreateHeaderText("Name", 1, false, false));

    [Fact]
    public void CreateHeaderText_ActiveAscending_ShowsUpArrow()
        => Assert.Equal($"[bold]Name[/][yellow](2) {Arrow(false)}[/]", UiStyles.CreateHeaderText("Name", 2, false, true));

    [Fact]
    public void CreateHeaderText_ActiveDescending_ShowsDownArrow()
        => Assert.Equal($"[bold]Name[/][yellow](2) {Arrow(true)}[/]", UiStyles.CreateHeaderText("Name", 2, true, true));

    // ---- CreateDangerText (raw markup string) ----

    [Fact]
    public void CreateDangerText_WrapsInBoldRed()
        => Assert.Equal("[bold red]del[/]", UiStyles.CreateDangerText("del"));

    // ---- Markup-producing helpers (assert visible text) ----

    [Fact]
    public void CreateLabelMarkup_RendersText()
        => Assert.Equal("Region", MarkupText.Plain(UiStyles.CreateLabelMarkup("Region")));

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CreateShortcutMarkup_RendersAngleBracketsAndDescription(bool enabled)
        => Assert.Equal("<F> Filter", MarkupText.Plain(UiStyles.CreateShortcutMarkup("F", "Filter", enabled)));

    [Fact]
    public void CreateSelectedCell_ConcatenatesTextAndStatus()
        => Assert.Equal("appA (current)", MarkupText.Plain(UiStyles.CreateSelectedCell("appA", " (current)")));

    [Fact]
    public void CreateStateCell_Running_RendersRunning()
        => Assert.Equal("Running", MarkupText.Plain(UiStyles.CreateStateCell(FunctionState.Running)));

    [Fact]
    public void CreateStateCell_Stopped_RendersStopped()
        => Assert.Equal("Stopped", MarkupText.Plain(UiStyles.CreateStateCell(FunctionState.Stopped)));

    [Fact]
    public void CreateStateCell_Unknown_Throws()
        => Assert.Throws<ArgumentOutOfRangeException>(() => UiStyles.CreateStateCell(FunctionState.Unknown));

    [Fact]
    public void CreateStatusCell_Idle_RendersEmpty()
        => Assert.Equal("", MarkupText.Plain(UiStyles.CreateStatusCell(new FunctionStatus { Status = StatusType.Idle })));

    [Fact]
    public void CreateStatusCell_Swapped_RendersSwapped()
        => Assert.Equal("Swapped", MarkupText.Plain(UiStyles.CreateStatusCell(new FunctionStatus { Status = StatusType.Swapped })));

    // ---- CreateFunctionsEmptyStateText ----

    private static FunctionAppDetails App(FunctionState state, StatusType status = StatusType.Idle, FunctionAction? action = null)
    {
        var app = new FunctionAppDetails { Name = "appA", State = state, ResourceGroup = "rg", Subscription = "sub", Id = "id" };
        app.Status.Status = status;
        app.Status.Action = action;
        return app;
    }

    private static UiStatusSnapshot Status(bool inventory = false, bool details = false) =>
        new() { IsInventoryValidating = inventory, IsDetailsRefreshing = details };

    [Fact]
    public void EmptyState_Starting_TakesPriority()
    {
        var text = UiStyles.CreateFunctionsEmptyStateText(App(FunctionState.Stopped, StatusType.InProgress, FunctionAction.Start), Status());
        Assert.Equal("[gray]Function app is starting. Functions will load when startup completes.[/]", text);
    }

    [Fact]
    public void EmptyState_Loading_WhenInventoryValidating()
    {
        var text = UiStyles.CreateFunctionsEmptyStateText(App(FunctionState.Running), Status(inventory: true));
        Assert.Equal("[gray]Functions are loading.[/]", text);
    }

    [Fact]
    public void EmptyState_Loading_WhenDetailsRefreshing()
    {
        var text = UiStyles.CreateFunctionsEmptyStateText(App(FunctionState.Running), Status(details: true));
        Assert.Equal("[gray]Functions are loading.[/]", text);
    }

    [Fact]
    public void EmptyState_Stopped_PromptsToStart()
    {
        var text = UiStyles.CreateFunctionsEmptyStateText(App(FunctionState.Stopped), Status());
        Assert.Equal("[gray]Function app is stopped. Start it to load functions.[/]", text);
    }

    [Fact]
    public void EmptyState_RunningAndIdle_ReturnsNull()
    {
        Assert.Null(UiStyles.CreateFunctionsEmptyStateText(App(FunctionState.Running), Status()));
    }
}
