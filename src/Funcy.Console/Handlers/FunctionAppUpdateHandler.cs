using System.Collections.Concurrent;
using System.Diagnostics;
using Funcy.Console.Handlers.Concurrency;
using Funcy.Console.Settings;
using Funcy.Console.Ui;
using Funcy.Console.Ui.State;
using Funcy.Core.Interfaces;
using Funcy.Core.Model;
using Funcy.Infrastructure.Azure;
using Microsoft.Extensions.Logging;

namespace Funcy.Console.Handlers;

public class FunctionAppUpdateHandler : IDetailsLoader
{
    private readonly ILogger<FunctionAppUpdateHandler> _logger;
    private readonly IAzureFunctionService _functionService;
    private readonly FunctionStateCoordinator _functionStateCoordinator;
    private readonly AnimationHandler _animationHandler;
    private readonly IUiStatusState _uiStatusState;
    private readonly IUiErrorLog _uiErrorLog;
    private readonly FunctionStatusManager _functionStatusManager;
    private readonly AppContext _appContext;
    private readonly IAzureSessionMonitor _sessionMonitor;
    private readonly IFuncySettingsService _settingsService;
    private readonly IServiceBusInsightService _serviceBusInsightService;

    // Cap on concurrent Service Bus count fetches during the app-list background pass, mirroring
    // the details fan-out throttle.
    private const int ServiceBusFetchConcurrency = 4;

    // The CancellationTokenSource and its Task are correlated and touched from three entry
    // points (SynchronizeFunctionAppDataAsync, LoadAllDetailsAsync, OnSubscriptionChanged) on
    // different threads. Holding them in one immutable object and swapping it with
    // Interlocked.Exchange lets the pair be replaced atomically, without a lock — the swap
    // hands the previous scope to exactly one caller, so it is never torn, orphaned, or
    // disposed twice.
    private sealed record SyncScope(CancellationTokenSource Cts, Task Task);

    private SyncScope? _scope;

    private readonly ConcurrentDictionary<string, DateTime> _lastSubscriptionSyncUtc = new();

    public FunctionAppUpdateHandler(ILogger<FunctionAppUpdateHandler> logger,
        IAzureFunctionService functionService,
        FunctionStateCoordinator functionStateCoordinator,
        AnimationHandler animationHandler,
        IUiStatusState uiStatusState,
        IUiErrorLog uiErrorLog,
        FunctionStatusManager functionStatusManager,
        AppContext appContext,
        IAzureSessionMonitor sessionMonitor,
        IFuncySettingsService settingsService,
        IServiceBusInsightService serviceBusInsightService)
    {
        _logger = logger;
        _functionService = functionService;
        _functionStateCoordinator = functionStateCoordinator;
        _animationHandler = animationHandler;
        _uiStatusState = uiStatusState;
        _uiErrorLog = uiErrorLog;
        _functionStatusManager = functionStatusManager;
        _appContext = appContext;
        _sessionMonitor = sessionMonitor;
        _settingsService = settingsService;
        _serviceBusInsightService = serviceBusInsightService;

        _appContext.OnSubscriptionChange += OnSubscriptionChanged;
    }

    private async void OnSubscriptionChanged(SubscriptionDetails obj)
    {
        await CancelCurrentSyncAsync();

        await InitializeAsync();

        if (ShouldRefreshSubscription(obj.Id))
        {
            await SynchronizeFunctionAppDataAsync();
        }
    }

    private async Task CancelCurrentSyncAsync()
    {
        var scope = Interlocked.Exchange(ref _scope, null);
        if (scope is null)
        {
            return;
        }

        try
        {
            await scope.Cts.CancelAsync();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed
        }

        try
        {
            await scope.Task;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error waiting for old task during subscription change");
        }

        scope.Cts.Dispose();
    }

    private bool ShouldRefreshSubscription(string subscriptionId)
    {
        var intervalMinutes = _settingsService.Current.SubscriptionRefreshIntervalMinutes;
        if (intervalMinutes == 0)
        {
            return true;
        }

        if (!_lastSubscriptionSyncUtc.TryGetValue(subscriptionId, out var lastSync))
        {
            return true;
        }

        return (DateTime.UtcNow - lastSync).TotalMinutes >= intervalMinutes;
    }

    public async Task InitializeAsync()
    {
        _functionStateCoordinator.SetSubscription(_appContext.CurrentSubscription.Id);
        var functionsFromDatabase =
            await _functionService.GetFunctionsFromDatabase(_appContext.CurrentSubscription.Id);
        _functionStateCoordinator.InitCache(functionsFromDatabase);
    }

    public async Task SynchronizeFunctionAppDataAsync()
    {
        await StartSyncScope(SynchronizeFunctionAppDataInternalAsync);
    }

    /// <summary>
    /// Atomically replaces any in-flight sync scope with a fresh one running
    /// <paramref name="work"/> under a new <see cref="CancellationToken"/>. The new scope is
    /// published with <see cref="Interlocked.Exchange{T}(ref T, T)"/>, which hands the previous
    /// scope to exactly one caller so it can be retired without a torn pair or an orphaned
    /// <see cref="CancellationTokenSource"/>.
    /// </summary>
    private Task StartSyncScope(Func<CancellationToken, Task> work)
    {
        var cts = new CancellationTokenSource();
        var task = Task.Run(() => work(cts.Token), cts.Token);

        var previous = Interlocked.Exchange(ref _scope, new SyncScope(cts, task));
        RetireScope(previous);
        return task;
    }

    /// <summary>
    /// Cancels a retired scope without blocking. Its <see cref="CancellationTokenSource"/> is
    /// disposed only after the task drains, so the token is never disposed while still observed.
    /// </summary>
    private void RetireScope(SyncScope? scope)
    {
        if (scope is null)
        {
            return;
        }

        try
        {
            scope.Cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already disposed
        }

        _ = scope.Task.ContinueWith(t =>
        {
            if (t.Exception is not null)
            {
                _logger.LogDebug(t.Exception, "Retired sync scope ended with an exception");
            }

            scope.Cts.Dispose();
        }, TaskScheduler.Default);
    }

    private async Task SynchronizeFunctionAppDataInternalAsync(CancellationToken token)
    {
        try
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            _animationHandler.AddAppDetails("TopPanel");
            _uiStatusState.BeginInventoryValidation();
            var functionAppDetailsToUpdate =
                _functionService.GetFunctionAppDetailsAsync(_appContext.CurrentSubscription.Id, token);

            await UpdateFunctionAppList(functionAppDetailsToUpdate, token);

            await _functionStateCoordinator.WaitForPendingUpdatesAsync();

            _uiStatusState.EndInventoryValidation();
            _animationHandler.RemoveAppDetails("TopPanel");

            _uiStatusState.BeginDetailsRefresh();
            await LoadAllDetailsInBackground(token);

            _lastSubscriptionSyncUtc[_appContext.CurrentSubscription.Id] = DateTime.UtcNow;
        }
        catch (OperationCanceledException)
        {
            // Expected when subscription changes
        }
        catch (TimeoutException ex)
        {
            _logger.LogWarning(ex, "Timeout waiting for database lock - previous sync did not complete in time");
            _uiErrorLog.Report("Sync", ex.Message);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during synchronization");
            _uiErrorLog.Report("Sync", ex.Message);
            _sessionMonitor.ReportPossibleAuthFailure(ex);
            throw;
        }
        finally
        {
            // Always release the spinner and status flags, even on cancellation, so a sync
            // that is retired mid-flight does not leave the TopPanel stuck on "Validating…".
            // All three calls are idempotent.
            _animationHandler.RemoveAppDetails("TopPanel");
            _uiStatusState.EndInventoryValidation();
            _uiStatusState.EndDetailsRefresh();
        }
    }

    private async Task LoadAllDetailsInBackground(CancellationToken token)
    {
        var allApps = _functionStateCoordinator.GetCachedFunctionAppDetails();
        _uiStatusState.SetTotalDetails(allApps.Count);
        var updatedFunctionApps = _functionService.GetFunctionAppFunctionsAndSlotsAsync(allApps, token);
        await UpdateFunctionAppList(updatedFunctionApps, token);

        // Details are now reflected in the UI. End the details-refresh status here (before the
        // Service Bus count pass) so the TopPanel shows "Last Updated" instead of holding on
        // "Refreshing function app details 0/N" for the whole, much slower, best-effort count pass.
        // The counts then trickle in as background StateOnly updates per app.
        _uiStatusState.EndDetailsRefresh();

        // Counts depend on the functions fetched above, so this always runs after the details pass.
        // It is a no-op (zero extra work, zero Azure calls) unless the setting is enabled. Shares
        // the caller's cancellation token (the SyncScope) so it is torn down with the sync it follows.
        await FetchServiceBusCountsForAllAppsAsync(token);
    }

    // Fans out Service Bus count fetches across the apps that actually have SB triggers, throttled
    // like the details pass. Each app publishes its own StateOnly update as its sums arrive, so the
    // list rows fill in progressively.
    private async Task FetchServiceBusCountsForAllAppsAsync(CancellationToken token)
    {
        if (!_settingsService.Current.ShowServiceBusInAppList)
        {
            return;
        }

        // Detail updates published above are applied asynchronously; wait for them to drain into the
        // cache so every app's freshly fetched functions are visible before we pick apps to fetch.
        await _functionStateCoordinator.WaitForPendingUpdatesAsync();

        var apps = ServiceBusCountFetchPlanner.AppsToFetch(
            _settingsService.Current.ShowServiceBusInAppList,
            _functionStateCoordinator.GetCachedFunctionAppDetails());
        if (apps.Count == 0)
        {
            return;
        }

        using var throttler = new SemaphoreSlim(ServiceBusFetchConcurrency);
        var tasks = apps.Select(async app =>
        {
            await throttler.WaitAsync(token);
            try
            {
                await FetchServiceBusCountsForAppAsync(app, token);
            }
            finally
            {
                throttler.Release();
            }
        });

        await Task.WhenAll(tasks);
    }

    // Resolves counts for one app's SB-triggered functions, mutates the cached model in place and
    // republishes it so the row re-renders (model mutation + invalidate only, per the threading
    // rule). Never throws for per-app failures; cancellation propagates.
    private async Task FetchServiceBusCountsForAppAsync(FunctionAppDetails app, CancellationToken token)
    {
        if (string.IsNullOrEmpty(app.Id))
        {
            return;
        }

        var serviceBusFunctions = app.Functions.Where(f => f.IsServiceBusTrigger).ToList();
        if (serviceBusFunctions.Count == 0)
        {
            return;
        }

        try
        {
            var results = await _serviceBusInsightService.GetCountsAsync(app.Id, serviceBusFunctions, token);
            if (token.IsCancellationRequested)
            {
                return;
            }

            var byKey = serviceBusFunctions.ToDictionary(f => f.Key);
            var newlyResolvedNamespaces = new List<(string FunctionName, string NamespaceId)>();
            var permissionError = results.Select(r => r.ErrorMessage).FirstOrDefault(m => m is not null);
            if (permissionError is not null)
            {
                _uiErrorLog.Report(app.Name, permissionError);
            }
            foreach (var result in results)
            {
                if (!byKey.TryGetValue(result.FunctionKey, out var function))
                {
                    continue;
                }

                function.ActiveMessages = result.ActiveMessages;
                function.DeadLetteredMessages = result.DeadLetteredMessages;
                function.CountStatus = result.Success
                    ? ServiceBusCountStatus.Loaded
                    : ServiceBusCountStatus.Failed;

                // Persist both first-time resolutions and configuration-driven namespace changes.
                if (!string.IsNullOrEmpty(result.NamespaceId)
                    && !string.Equals(function.ServiceBusNamespaceId, result.NamespaceId,
                        StringComparison.OrdinalIgnoreCase))
                {
                    function.ServiceBusNamespaceId = result.NamespaceId;
                    newlyResolvedNamespaces.Add((function.Name, result.NamespaceId));
                }
            }

            if (newlyResolvedNamespaces.Count > 0)
            {
                await _functionService.SaveServiceBusNamespacesAsync(app.Id, newlyResolvedNamespaces);
            }

            await _functionStateCoordinator.PublishUpdateAsync(app);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer sync or subscription switch.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Service Bus counts for app {App}", app.Key);
        }
    }

    public void LoadDetails(string currentKey)
    {
        var functionAppDetails = _functionStateCoordinator.TryGet(currentKey);
        if (functionAppDetails is null) return;

        _ = Task.Run(async () =>
        {
            await _functionStatusManager.BeginOperation(functionAppDetails, FunctionAction.Refresh);
            var updatedDetails = await _functionService.GetFunctionAppDetails(functionAppDetails);
            await _functionStatusManager.CompleteOperation(updatedDetails, FunctionAction.Refresh, true);

            // R on the app list also refreshes the selected app's aggregated counts (no-op unless
            // the setting is enabled). CompleteOperation publishes a StateOnly update, which
            // MergeUpdate resolves by preserving the cached function instances rather than the
            // freshly-mapped ones. Align updatedDetails with those preserved instances so the
            // counts we resolve and republish land on the rows the list actually renders.
            if (_settingsService.Current.ShowServiceBusInAppList)
            {
                updatedDetails.Functions = functionAppDetails.Functions;
                await FetchServiceBusCountsForAppAsync(updatedDetails, CancellationToken.None);
            }
        });
    }

    public async Task LoadAllDetailsAsync()
    {
        if (!CanRefreshAll()) return;

        await StartSyncScope(async token =>
        {
            _uiStatusState.BeginDetailsRefresh();
            try
            {
                await LoadAllDetailsInBackground(token);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during refresh all");
                _uiErrorLog.Report("Sync", ex.Message);
                _sessionMonitor.ReportPossibleAuthFailure(ex);
            }
            finally
            {
                _uiStatusState.EndDetailsRefresh();
            }
        });
    }

    public bool CanRefreshAll()
    {
        var snapshot = _uiStatusState.GetSnapshot();
        return !snapshot.IsInventoryValidating && !snapshot.IsDetailsRefreshing;
    }

    public async Task TogglePinAsync(string key)
    {
        var details = _functionStateCoordinator.TryGet(key);
        if (details is null)
        {
            return;
        }

        var newValue = !details.IsPinned;
        try
        {
            await _functionService.SetPinnedAsync(details.Id, newValue);

            // Mutate the cached details and republish so the row re-sorts (pinned first) and the
            // glyph is rebuilt immediately, mirroring how status updates flow through the pipeline.
            details.IsPinned = newValue;
            details.LastUpdated = DateTime.UtcNow;
            await _functionStateCoordinator.PublishUpdateAsync(details);
        }
        catch (Exception ex)
        {
            // Called fire-and-forget from input handling; a failed pin must not surface as an
            // unobserved task exception.
            _logger.LogError(ex, "Failed to toggle pin for {Key}", key);
        }
    }

    private async Task UpdateFunctionAppList(IAsyncEnumerable<FunctionAppFetchResult> functionAppDetailsToUpdate,
        CancellationToken syncCtsToken)
    {
        List<string> existingFunctionAppNames = [];
        var sw = Stopwatch.StartNew();
        try
        {
            await foreach (var newApp in functionAppDetailsToUpdate.WithCancellation(syncCtsToken))
            {
                existingFunctionAppNames.Add(newApp.Name);
                if (newApp.IsSuccess)
                {
                    await _functionStateCoordinator.PublishUpdateAsync(newApp.Details!, newApp.UpdateKind);
                    _uiStatusState.IncrementDetailsInFlight();
                }
                else
                {
                    // Per-app fetch failure streamed from the Azure service — surface it persistently
                    // and classify so an expired session is caught even when the sync does not throw.
                    _uiErrorLog.Report(newApp.Name, newApp.ErrorMessage ?? "Failed to fetch function app details");
                    _sessionMonitor.ReportPossibleAuthFailure(newApp.ErrorMessage);
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("UpdateFunctionAppList was cancelled");
        }
        finally
        {
            _uiStatusState.ResetDetailsInFlight();
            sw.Stop();
            _logger.LogInformation("Updated Function App List in {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);

            if (existingFunctionAppNames.Count > 0)
            {
                await _functionStateCoordinator.RemoveFunctions(existingFunctionAppNames);
            }
        }
    }
}
