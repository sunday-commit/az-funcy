using Funcy.Core.Model;
using Funcy.Data;
using Funcy.Data.Entities;
using Funcy.Infrastructure.Azure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Funcy.Console;

public class AppContext(
    AzureSubscriptionService azureSubscriptionService,
    IDbContextFactory<FunctionAppDbContext> dbFactory,
    ILogger<AppContext> logger)
{
    private SubscriptionDetails? _currentSubscription;
    public SubscriptionDetails CurrentSubscription =>
        _currentSubscription ?? throw new InvalidOperationException("AppContext not initialized");
    private Dictionary<string, SubscriptionDetails> CachedSubscriptions { get; set; } = [];
    public event Action<SubscriptionDetails>? OnSubscriptionChange;

    public bool HideEmptySubscriptions { get; private set; } = true;
    public void ToggleHideEmptySubscriptions() => HideEmptySubscriptions = !HideEmptySubscriptions;

    private Dictionary<string, bool> _hiddenSubscriptions = [];

    public bool IsSubscriptionHidden(string subscriptionId) =>
        _hiddenSubscriptions.TryGetValue(subscriptionId, out var hidden) && hidden;

    public IReadOnlyList<SubscriptionDetails> GetUnprobedSubscriptions() =>
        CachedSubscriptions.Values
            .Where(s => !_hiddenSubscriptions.ContainsKey(s.Id))
            .ToList();

    public async Task InitializeAppContext()
    {
        var subscriptions = await azureSubscriptionService.GetSubscriptions();
        SetCachedSubscriptions(subscriptions);

        await using var db = await dbFactory.CreateDbContextAsync();
        _hiddenSubscriptions = await db.SubscriptionSettings
            .ToDictionaryAsync(s => s.SubscriptionId, s => s.IsHidden);

        _currentSubscription ??=
            subscriptions.FirstOrDefault(s => s.Current)
            ?? throw new InvalidOperationException("No current subscription resolved");
    }

    public async Task RecordProbeResultAsync(string subscriptionId, bool hasApps)
    {
        var isHidden = !hasApps;
        _hiddenSubscriptions[subscriptionId] = isHidden;

        var name = CachedSubscriptions.Values
            .FirstOrDefault(s => s.Id == subscriptionId)?.Name ?? subscriptionId;
        logger.LogInformation("Probed '{Name}': {Result}", name, hasApps ? "has function apps" : "no function apps");
        await using var db = await dbFactory.CreateDbContextAsync();
        if (!await db.SubscriptionSettings.AnyAsync(s => s.SubscriptionId == subscriptionId))
        {
            db.SubscriptionSettings.Add(new SubscriptionSetting
            {
                SubscriptionId = subscriptionId,
                Name = name,
                IsHidden = isHidden
            });
            await db.SaveChangesAsync();
        }
    }

    public void ToggleSubscriptionVisibility(string subscriptionId)
    {
        var current = _hiddenSubscriptions.TryGetValue(subscriptionId, out var hidden) && hidden;
        _hiddenSubscriptions[subscriptionId] = !current;
        _ = PersistVisibilityAsync(subscriptionId, !current);
    }

    private async Task PersistVisibilityAsync(string subscriptionId, bool isHidden)
    {
        var name = CachedSubscriptions.Values
            .FirstOrDefault(s => s.Id == subscriptionId)?.Name ?? subscriptionId;

        await using var db = await dbFactory.CreateDbContextAsync();
        var existing = await db.SubscriptionSettings.FindAsync(subscriptionId);
        if (existing != null)
        {
            existing.IsHidden = isHidden;
        }
        else
        {
            db.SubscriptionSettings.Add(new SubscriptionSetting
            {
                SubscriptionId = subscriptionId,
                Name = name,
                IsHidden = isHidden
            });
        }
        await db.SaveChangesAsync();
    }

    private void SetCachedSubscriptions(List<SubscriptionDetails> subscriptions)
    {
        var newCache = new Dictionary<string, SubscriptionDetails>();
        foreach (var subscription in subscriptions)
        {
            subscription.Current = _currentSubscription is not null ? subscription.Key == CurrentSubscription.Key : subscription.Current;
            newCache.TryAdd(subscription.Key, subscription);
        }

        CachedSubscriptions = newCache;
    }

    public IReadOnlyList<SubscriptionDetails> GetSnapshot() => CachedSubscriptions.Values.ToList();

    public void ChangeSubscription(string subscriptionKey)
    {
        if (!CachedSubscriptions.TryGetValue(subscriptionKey, out var subscription))
        {
            throw new KeyNotFoundException($"Subscription '{subscriptionKey}' not found");
        }

        if (CurrentSubscription.Key == subscriptionKey) return;

        _currentSubscription = subscription;
        CachedSubscriptions.Values.ToList().ForEach(s => s.Current = s.Key == subscriptionKey);

        OnSubscriptionChange?.Invoke(_currentSubscription);
    }
}
