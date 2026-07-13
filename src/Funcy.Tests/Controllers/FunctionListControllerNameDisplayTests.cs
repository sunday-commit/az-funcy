using System.Threading.Channels;
using Funcy.Console.Handlers;
using Funcy.Console.Handlers.Concurrency;
using Funcy.Console.Handlers.Models;
using Funcy.Console.Ui;
using Funcy.Console.Ui.Controllers;
using Funcy.Console.Ui.Navigation;
using Funcy.Console.Ui.PanelLayout.Renderers;
using Funcy.Console.Ui.Pagination.Matchers;
using Funcy.Console.Ui.Panels;
using Funcy.Console.Ui.Shortcuts;
using Funcy.Console.Ui.State;
using Funcy.Core.Interfaces;
using Funcy.Core.Model;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Funcy.Tests.Controllers;

// End-to-end coverage for the "Listens to" column showing the RESOLVED %SettingName% Service Bus
// binding names. Wires a real ListPanelView<FunctionDetails> + real FunctionLayoutRenderer + real
// FunctionListController + real FunctionStateCoordinator with a fake insight service that resolves
// the raw %X% names, and asserts on the RENDERED cell text (styling stripped via MarkupText.Plain).
public class FunctionListControllerNameDisplayTests
{
    private const string AppName = "orders-app";
    private const string Sub = "sub-1";
    private const string ArmId = "/subscriptions/sub-1/resourceGroups/rg/providers/Microsoft.Web/sites/orders-app";
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    // Raw, %SettingName%-style names as they are stored in SQLite and mapped from the DB row.
    private const string RawTopic = "%OrdersTopic%";
    private const string RawSub = "%OrdersSubscription%";
    private const string Resolved = "orders/incoming"; // resolved TopicName/SubscriptionName -> "orders/incoming"

    [Fact]
    public async Task ResolvedNames_SurviveADetailsKindRepublish_WhileTheReFetchIsStillInFlight()
    {
        var renderer = new FunctionLayoutRenderer();
        var view = new ListPanelView<FunctionDetails>(
            new FunctionMatcher(),
            renderer,
            new NoShortcuts(),
            new NoAnimations(),
            onEnterNavigation: null,
            header: "Functions",
            onAction: null,
            onActionNavigation: null,
            emptyStateMessage: null,
            windowHeight: () => 30,
            windowWidth: () => 200);

        var coordinator = new FunctionStateCoordinator();
        coordinator.SetSubscription(Sub);
        coordinator.InitCache([AppWithRawServiceBusFunction()]);

        var insight = new GatedInsightService(ResolveNames);
        var pulse = new Pulse();

        var controller = new FunctionListController(
            view,
            AppName,
            coordinator.TryGet(AppName)!.Functions,
            coordinator,
            insight,
            NullLogger<FunctionListController>.Instance,
            new NoopUiStatusState(),
            invalidate: pulse.Set);

        try
        {
            // 1) The constructor's fetch resolves the names. Release it and wait for the controller
            //    to apply the result, then assert the rendered cell shows the resolved target.
            var firstCall = await insight.NextCallAsync();
            var applied = pulse.NextAsync();
            firstCall.Release();
            await applied.WaitAsync(Timeout);

            Assert.Contains(Resolved, ListensToText(view));
            Assert.DoesNotContain("%", ListensToText(view));

            // 2) The background details pass republishes a Details-kind update: freshly DB-mapped
            //    FunctionDetails carrying the RAW %X% names replace the cached instances.
            var republished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            coordinator.OnFunctionListUpdated += (key, _) =>
            {
                if (key == AppName)
                {
                    republished.TrySetResult();
                }
            };
            await coordinator.PublishUpdateAsync(AppWithRawServiceBusFunction(), FunctionAppUpdateKind.Details);
            await republished.Task.WaitAsync(Timeout);
            await coordinator.WaitForPendingUpdatesAsync();

            // The controller re-fetches after the republish, but that fetch is still in flight (the
            // gate below is never released). The rendered cell must STILL show the resolved name —
            // not revert to the raw %X% placeholders the republish carried.
            Assert.Contains(Resolved, ListensToText(view));
            Assert.DoesNotContain("%", ListensToText(view));
        }
        finally
        {
            insight.ReleaseAll();
            controller.Dispose();
        }
    }

    [Fact]
    public async Task ServiceBusPermissionFailure_IsReportedWithoutHidingFunction()
    {
        var view = new ListPanelView<FunctionDetails>(new FunctionMatcher(), new FunctionLayoutRenderer(),
            new NoShortcuts(), new NoAnimations(), null, "Functions", null, null, null, () => 30, () => 200);
        var coordinator = new FunctionStateCoordinator();
        coordinator.SetSubscription(Sub);
        coordinator.InitCache([AppWithRawServiceBusFunction()]);
        var insight = new GatedInsightService(f => new ServiceBusCountResult(f.Key, null, null, false,
            ErrorMessage: "Service Bus counts access denied. Required: Reader on the Service Bus namespace."));
        var errors = new UiErrorLog();
        var controller = new FunctionListController(view, AppName, coordinator.TryGet(AppName)!.Functions,
            coordinator, insight, NullLogger<FunctionListController>.Instance, new NoopUiStatusState(),
            uiErrorLog: errors);

        try
        {
            var call = await insight.NextCallAsync();
            call.Release();
            await WaitUntilAsync(() => errors.Count == 1);

            Assert.Contains("Reader on the Service Bus namespace", Assert.Single(errors.GetSnapshot()).Message);
            Assert.Equal("ProcessOrders", Assert.Single(coordinator.TryGet(AppName)!.Functions).Name);
        }
        finally
        {
            controller.Dispose();
        }
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(Timeout);
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private static ServiceBusCountResult ResolveNames(FunctionDetails f) =>
        new(f.Key, 3, 1, true, QueueName: null, TopicName: "orders", SubscriptionName: "incoming");

    private static string ListensToText(ListPanelView<FunctionDetails> view)
    {
        view.RenderIfNeeded();
        return MarkupTextOf(view);
    }

    private static string MarkupTextOf(ListPanelView<FunctionDetails> view)
        => Funcy.Tests.TestSupport.MarkupText.Plain(view.Panel);

    private static FunctionAppDetails AppWithRawServiceBusFunction() => new()
    {
        Name = AppName,
        State = FunctionState.Running,
        ResourceGroup = "rg",
        Subscription = Sub,
        Id = ArmId,
        Functions =
        [
            new FunctionDetails
            {
                FunctionAppName = AppName,
                Name = "ProcessOrders",
                Trigger = "ServiceBusTrigger",
                TopicName = RawTopic,
                SubscriptionName = RawSub,
                ConnectionSetting = "ServiceBusConnection"
            }
        ]
    };

    // A fake insight service whose GetCountsAsync parks each call until the test releases it,
    // giving the test deterministic control over exactly when resolved names become available.
    private sealed class GatedInsightService(Func<FunctionDetails, ServiceBusCountResult> resolve)
        : IServiceBusInsightService
    {
        private readonly Channel<Call> _calls = Channel.CreateUnbounded<Call>();

        public sealed class Call
        {
            public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
            public void Release() => Gate.TrySetResult();
        }

        public async Task<IReadOnlyList<ServiceBusCountResult>> GetCountsAsync(
            string functionAppArmId,
            IReadOnlyList<FunctionDetails> serviceBusFunctions,
            CancellationToken cancellationToken)
        {
            var call = new Call();
            await _calls.Writer.WriteAsync(call, cancellationToken);
            await call.Gate.Task.WaitAsync(cancellationToken);
            return serviceBusFunctions.Select(resolve).ToList();
        }

        public async Task<Call> NextCallAsync()
            => await _calls.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        public void ReleaseAll()
        {
            while (_calls.Reader.TryRead(out var call))
            {
                call.Release();
            }
        }
    }

    // A refreshable one-shot signal: capture NextAsync() before an action, await it after, and it
    // completes on the next Set() (fired by the controller's invalidate callback).
    private sealed class Pulse
    {
        private readonly Lock _lock = new();
        private TaskCompletionSource _tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Set()
        {
            TaskCompletionSource previous;
            lock (_lock)
            {
                previous = _tcs;
                _tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            previous.TrySetResult();
        }

        public Task NextAsync()
        {
            lock (_lock)
            {
                return _tcs.Task;
            }
        }
    }

    private sealed class NoAnimations : IAnimationProvider
    {
        public List<AnimationContext> GetAnimations() => [];
        public AnimationContext? GetAnimation(string key) => null;
    }

    private sealed class NoShortcuts : IShortcutProvider<FunctionDetails>
    {
        public Dictionary<TableIndex, ShortcutMap> Describe(FunctionDetails? item) => new();
        public bool IsActionValid(FunctionDetails? item, FunctionAction action) => false;
    }

    private sealed class NoopUiStatusState : IUiStatusState
    {
        public event Action? Changed { add { } remove { } }
        public UiStatusSnapshot GetSnapshot() => new();
        public void BeginInventoryValidation() { }
        public void EndInventoryValidation() { }
        public void BeginDetailsRefresh() { }
        public void EndDetailsRefresh() { }
        public void SetTotalDetails(int count) { }
        public void IncrementDetailsInFlight() { }
        public void ResetDetailsInFlight() { }
        public void SetLastError(string? message) { }
    }
}
