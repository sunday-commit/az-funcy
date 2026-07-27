using Funcy.Core.Model;

namespace Funcy.Demo;

/// <summary>
/// In-memory stand-in for the inventory a real run keeps in SQLite: the single source of truth for
/// every demo service. Actions taken in the demo (start, stop, swap, pin, disable) mutate this
/// estate, so the UI reacts the way it does against Azure without anything being persisted.
/// </summary>
/// <remarks>
/// Registered as a singleton. The demo services fan out across threads exactly like the Azure ones,
/// so mutation is guarded by a lock; the models themselves are handed out by reference, mirroring
/// how the state coordinator owns cached instances.
/// </remarks>
public sealed class DemoEstate
{
    private readonly Lock _sync = new();
    private readonly List<SubscriptionDetails> _subscriptions = DemoDataset.CreateSubscriptions();
    private readonly List<FunctionAppDetails> _apps = DemoDataset.CreateApps();
    private readonly HashSet<string> _syncedSubscriptions = [];
    private readonly Dictionary<string, (long Active, long DeadLettered)> _countsByFunctionKey = [];

    public DemoEstate()
    {
        // The dataset declares each queue's contents next to the binding that reads it, which keeps
        // it readable. Lift those numbers out into the count lookup and clear them off the models so
        // the UI can only learn them the way it does live: by asking the count service.
        foreach (var function in _apps.SelectMany(a => a.Functions).Where(f => f.IsServiceBusTrigger))
        {
            _countsByFunctionKey[function.Key] =
                (function.ActiveMessages ?? 0, function.DeadLetteredMessages ?? 0);
            function.ActiveMessages = null;
            function.DeadLetteredMessages = null;
            function.CountStatus = ServiceBusCountStatus.None;
        }
    }

    public string CurrentSubscriptionId => DemoDataset.ProductionSubscriptionId;

    public (long Active, long DeadLettered)? CountsFor(string functionKey)
    {
        lock (_sync)
        {
            return _countsByFunctionKey.TryGetValue(functionKey, out var counts) ? counts : null;
        }
    }

    /// <summary>Whether an inventory pass has run for this subscription in this session. Demo mode
    /// keeps no database, so this stands in for "the local cache already has rows" and lets the
    /// first render fill in progressively, the way a real first run does.</summary>
    public bool HasSyncedInventory(string subscriptionId)
    {
        lock (_sync)
        {
            return _syncedSubscriptions.Contains(subscriptionId);
        }
    }

    public void MarkInventorySynced(string subscriptionId)
    {
        lock (_sync)
        {
            _syncedSubscriptions.Add(subscriptionId);
        }
    }

    public List<SubscriptionDetails> Subscriptions()
    {
        lock (_sync)
        {
            return _subscriptions.ToList();
        }
    }

    public List<FunctionAppDetails> AppsFor(string subscriptionId)
    {
        lock (_sync)
        {
            var apps = _apps.Where(a => a.Subscription == subscriptionId).ToList();
            apps.Sort();
            return apps;
        }
    }

    public bool HasApps(string subscriptionId)
    {
        lock (_sync)
        {
            return _apps.Any(a => a.Subscription == subscriptionId);
        }
    }

    public FunctionAppDetails? TryGetApp(string armId)
    {
        lock (_sync)
        {
            return _apps.FirstOrDefault(a => a.Id == armId);
        }
    }

    public void SetState(string armId, FunctionState state)
    {
        Mutate(armId, app =>
        {
            app.State = state;
            app.LastUpdated = DateTime.UtcNow;
        });
    }

    public void SetPinned(string armId, bool isPinned) => Mutate(armId, app => app.IsPinned = isPinned);

    public void SetFunctionDisabled(string armId, string functionName, bool disabled) =>
        Mutate(armId, app =>
        {
            var function = app.Functions.FirstOrDefault(f => f.Name == functionName);
            if (function is not null)
            {
                function.IsDisabled = disabled;
            }
        });

    /// <summary>Applies a slot swap: production takes the slot's place and the slot is left stopped,
    /// which is the end state the real swap leaves behind.</summary>
    public void Swap(string armId, string slotName) =>
        Mutate(armId, app =>
        {
            var slot = app.Slots.FirstOrDefault(s => s.Name == slotName);
            if (slot is not null)
            {
                slot.State = FunctionState.Stopped;
            }

            app.State = FunctionState.Running;
            app.LastUpdated = DateTime.UtcNow;
        });

    private void Mutate(string armId, Action<FunctionAppDetails> mutation)
    {
        lock (_sync)
        {
            var app = _apps.FirstOrDefault(a => a.Id == armId);
            if (app is not null)
            {
                mutation(app);
            }
        }
    }
}
