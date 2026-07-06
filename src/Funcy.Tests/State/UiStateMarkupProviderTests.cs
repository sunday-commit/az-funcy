using Funcy.Console.Handlers;
using Funcy.Console.Handlers.Models;
using Funcy.Console.Ui;
using Funcy.Console.Ui.State;
using Funcy.Tests.TestSupport;
using Xunit;

namespace Funcy.Tests.State;

public class UiStateMarkupProviderTests
{
    private sealed class FakeAnimations(string? frame) : IAnimationProvider
    {
        public List<AnimationContext> GetAnimations() => [];
        public AnimationContext? GetAnimation(string key) => frame is null ? null : new AnimationContext(key, frame);
    }

    private static string Render(UiStatusSnapshot snapshot, string? frame = null)
        => MarkupText.Plain(new UiStateMarkupProvider(new FakeAnimations(frame)).CreateMarkupFromUiStatusState(snapshot));

    [Fact]
    public void InventoryValidating_ShowsValidatingWithAnimationFrame()
    {
        var text = Render(new UiStatusSnapshot { IsInventoryValidating = true }, frame: "*");
        Assert.Equal("Validating all function apps *", text);
    }

    [Fact]
    public void InventoryValidating_TakesPriorityOverDetailsRefreshing()
    {
        var text = Render(new UiStatusSnapshot { IsInventoryValidating = true, IsDetailsRefreshing = true }, frame: "*");
        Assert.Equal("Validating all function apps *", text);
    }

    [Fact]
    public void DetailsRefreshing_ShowsProgressCounter()
    {
        var text = Render(new UiStatusSnapshot { IsDetailsRefreshing = true, DetailsInFlight = 2, TotalDetails = 5 });
        Assert.Equal("Refreshing function app details 2/5", text);
    }

    [Fact]
    public void Idle_WithTimestamp_ShowsLastUpdated()
    {
        var ticks = new DateTime(2026, 7, 6, 14, 35, 0, DateTimeKind.Utc).Ticks;
        var expected = $"Last Updated {new DateTime(ticks, DateTimeKind.Utc):HH:mm}";
        var text = Render(new UiStatusSnapshot { LastInventoryRefreshUtcTicks = ticks });
        Assert.Equal(expected, text);
    }

    [Fact]
    public void Idle_WithoutTimestamp_ShowsEmpty()
    {
        var text = Render(new UiStatusSnapshot { LastInventoryRefreshUtcTicks = 0 });
        Assert.Equal("", text);
    }
}
