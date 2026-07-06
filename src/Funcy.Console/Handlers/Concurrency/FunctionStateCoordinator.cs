using System.Collections.Concurrent;
using System.Threading.Channels;
using Funcy.Console.Handlers.Models;
using Funcy.Core.Model;

namespace Funcy.Console.Handlers.Concurrency;

public class FunctionStateCoordinator
{
    private readonly Channel<FunctionAppUpdate> _updateChannel = Channel.CreateUnbounded<FunctionAppUpdate>();
    private readonly Channel<FunctionAppDetails> _removeChannel = Channel.CreateUnbounded<FunctionAppDetails>();

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, CachedFunctionAppModel>> _cache = new();

    private readonly SemaphoreSlim _uiUpdateLock = new(1, 1);
    private string? _currentSubscriptionId;
    public event Action<List<FunctionAppDetails>>? OnCacheInit;
    public event Action<FunctionAppDetails>? OnFunctionAppUpdated;
    public event Action<FunctionAppDetails>? OnFunctionAppRemoved;
    public event Action<string, List<FunctionDetails>>? OnFunctionListUpdated;

    public FunctionStateCoordinator()
    {
        _ = ProcessUpdatesAsync();
        _ = ProcessRemovalsAsync();
    }
    
    public void SetSubscription(string subscriptionId)
    {
        _currentSubscriptionId = subscriptionId;
        _cache.GetOrAdd(subscriptionId, _ => new ConcurrentDictionary<string, CachedFunctionAppModel>());
    }
    
    private ConcurrentDictionary<string, CachedFunctionAppModel> GetCurrentCache()
    {
        return _cache.GetOrAdd(_currentSubscriptionId!, _ => new ConcurrentDictionary<string, CachedFunctionAppModel>());
    }
    
    public void InitCache(List<FunctionAppDetails> functionsFromDatabase)
    {
        var subCache = GetCurrentCache();
        subCache.Clear();
        foreach (var functionApp in functionsFromDatabase)
        {
            subCache.TryAdd(functionApp.Name, new CachedFunctionAppModel(functionApp, functionApp.LastUpdated));
        }
        OnCacheInit?.Invoke(functionsFromDatabase);
    }

    public List<FunctionAppDetails> GetCachedFunctionAppDetails()
    {
        return GetCurrentCache().Values.Select(f => f.FunctionAppDetails).ToList();
    }

    public FunctionAppDetails? TryGet(string key)
    {
        GetCurrentCache().TryGetValue(key, out var app);
        return app?.FunctionAppDetails;
    }

    public bool IsSubscriptionKnownEmpty(string subscriptionId)
    {
        return _cache.TryGetValue(subscriptionId, out var subCache) && subCache.IsEmpty;
    }

    public void MarkSubscriptionAsEmpty(string subscriptionId)
    {
        _cache.TryAdd(subscriptionId, new ConcurrentDictionary<string, CachedFunctionAppModel>());
    }

    private int _pendingUpdates;
    private TaskCompletionSource? _allUpdatesProcessed;

    public async Task PublishUpdateAsync(FunctionAppDetails details, FunctionAppUpdateKind updateKind = FunctionAppUpdateKind.StateOnly)
    {
        Interlocked.Increment(ref _pendingUpdates);
        await _updateChannel.Writer.WriteAsync(new FunctionAppUpdate(details, updateKind));
    }

    public async Task WaitForPendingUpdatesAsync()
    {
        if (_pendingUpdates == 0) return;
        
        _allUpdatesProcessed = new TaskCompletionSource();
        await _allUpdatesProcessed.Task;
    }
    
    private async Task PublishRemoveAsync(FunctionAppDetails removedApp)
    {
        await _removeChannel.Writer.WriteAsync(removedApp);
    }
    
    public async Task RemoveFunctions(List<string> existingFunctionAppNames)
    {
        var subCache = GetCurrentCache();
        var removedFunctions = subCache.Keys.Except(existingFunctionAppNames).Select(functionApp => subCache[functionApp]).ToList();
        foreach (var removedFunction in removedFunctions)
        {
            await PublishRemoveAsync(removedFunction.FunctionAppDetails);
        }
    }

    private async Task ProcessUpdatesAsync()
    {
        await foreach (var update in _updateChannel.Reader.ReadAllAsync())
        {
            if (update.Details.Subscription != _currentSubscriptionId)
            {
                Interlocked.Decrement(ref _pendingUpdates);
                if (_pendingUpdates == 0)
                {
                    _allUpdatesProcessed?.TrySetResult();
                }
                continue;
            }
            
            var subCache = GetCurrentCache();
            var mergedDetails = MergeUpdate(subCache, update);
            subCache[mergedDetails.Name] = new CachedFunctionAppModel(mergedDetails, mergedDetails.LastUpdated);
            await _uiUpdateLock.WaitAsync();
            try
            {
                OnFunctionAppUpdated?.Invoke(mergedDetails);
                if (update.UpdateKind != FunctionAppUpdateKind.Inventory)
                {
                    OnFunctionListUpdated?.Invoke(mergedDetails.Key, mergedDetails.Functions);
                }
            }
            finally
            {
                _uiUpdateLock.Release();
            }
            
            if (Interlocked.Decrement(ref _pendingUpdates) == 0)
            {
                _allUpdatesProcessed?.TrySetResult();
            }
        }
    }

    private static FunctionAppDetails MergeUpdate(
        ConcurrentDictionary<string, CachedFunctionAppModel> subCache,
        FunctionAppUpdate update)
    {
        // Only Details updates carry a fresh Functions/Slots payload. Inventory and StateOnly
        // updates carry no detail payload, so the cached Functions/Slots must be preserved
        // instead of being overwritten with the update's empty lists.
        if (update.UpdateKind == FunctionAppUpdateKind.Details)
        {
            return update.Details;
        }

        if (!subCache.TryGetValue(update.Details.Name, out var existing))
        {
            return update.Details;
        }
        
        update.Details.Functions = existing.FunctionAppDetails.Functions;
        update.Details.Slots = existing.FunctionAppDetails.Slots;

        return update.Details;
    }
    
    private async Task ProcessRemovalsAsync()
    {
        await foreach (var removedApp in _removeChannel.Reader.ReadAllAsync())
        {
            // _cache is keyed by subscription id; the app lives in the inner
            // per-subscription cache keyed by app name. Evict using the app's
            // own subscription so a mid-flight subscription switch can't make us
            // touch the wrong sub-cache.
            if (_cache.TryGetValue(removedApp.Subscription, out var subCache))
            {
                subCache.TryRemove(removedApp.Name, out _);
            }

            await _uiUpdateLock.WaitAsync();
            try
            {
                OnFunctionAppRemoved?.Invoke(removedApp);
            }
            finally
            {
                _uiUpdateLock.Release();
            }
        }
    }
}
