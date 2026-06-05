using Funcy.Console.Ui;
using Funcy.Console.Ui.Shortcuts;
using Funcy.Core.Model;
using Xunit;

namespace Funcy.Tests.Shortcuts;

public class FunctionAppShortcutProviderTests
{
    private readonly FunctionAppShortcutProvider _sut = new(new UiStatusState());

    private static FunctionAppDetails MakeApp(int slotCount, StatusType status = StatusType.Idle)
    {
        var app = new FunctionAppDetails
        {
            Name = "appA",
            State = FunctionState.Running,
            ResourceGroup = "rg",
            Subscription = "sub-1",
            Id = "id"
        };
        app.Status.Status = status;
        app.Slots = Enumerable.Range(0, slotCount)
            .Select(i => new FunctionAppSlotDetails
            {
                Id = $"id{i}",
                FullName = $"appA/s{i}",
                Name = $"s{i}",
                State = FunctionState.Running
            })
            .ToList();
        return app;
    }

    [Fact]
    public void Swap_Disabled_WhenNoSlots()
        => Assert.False(_sut.IsActionValid(MakeApp(0), FunctionAction.Swap));

    [Fact]
    public void Swap_Enabled_WithSingleSlot()
        => Assert.True(_sut.IsActionValid(MakeApp(1), FunctionAction.Swap));

    [Fact]
    public void Swap_Enabled_WithMultipleSlots()
        => Assert.True(_sut.IsActionValid(MakeApp(3), FunctionAction.Swap));

    [Fact]
    public void Swap_Disabled_WhenOperationInProgress()
        => Assert.False(_sut.IsActionValid(MakeApp(1, StatusType.InProgress), FunctionAction.Swap));
}
