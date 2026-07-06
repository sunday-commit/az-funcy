using System.Collections.Concurrent;
using System.Diagnostics;
using Funcy.Console.Handlers.Concurrency;
using Funcy.Console.Settings;
using Funcy.Console.Ui;
using Funcy.Core.Interfaces;
using Funcy.Core.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Funcy.Console.Handlers;

public class FunctionAppUpdateHandler : IDetailsLoader
{
    private readonly ILogger<FunctionAppUpdateHandler> _logger;
    private readonly IAzureFunctionService _functionService;
    private readonly FunctionStateCoordinator _functionStateCoordinator;
    private readonly AnimationHandler _animationHandler;
    private readonly IUiStatusState _uiStatusState;
    private readonly FunctionStatusManager _functionStatusManager;
    private readonly AppContext _appContext;
    private readonly FuncySettings _settings;

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
        FunctionStatusManager functionStatusManager,
        AppContext appContext,
        IOptions<FuncySettings> settings)
    {
        _logger = logger;
        _functionService = functionService;
        _functionStateCoordinator = functionStateCoordinator;
        _animationHandler = animationHandler;
        _uiStatusState = uiStatusState;
        _functionStatusManager = functionStatusManager;
        _appContext = appContext;
        _settings = settings.Value;

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
        var intervalMinutes = _settings.SubscriptionRefreshIntervalMinutes;
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
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during synchronization");
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
