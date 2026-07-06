namespace Funcy.Core.Model;

public class FunctionAppDetails : IComparable<FunctionAppDetails>, IHasKey, IPinnable, IOperationVisibility
{
    public required string Name { get; init; }
    public string Key => Name;
    public required FunctionState State { get; set; }
    public FunctionStatus Status { get; set; } = new();
    public bool IsPinned { get; set; }

    // Any non-idle status (in progress, or the short-lived success/error/swapped
    // result) keeps the row visible past a non-matching filter until the status
    // TTL resets it to Idle, at which point the bypass clears itself.
    public bool HasActiveOperation => Status.Status != StatusType.Idle;
    public Dictionary<string, string> Tags { get; init; } = [];
    public List<FunctionAppSlotDetails> Slots { get; set; } = [];
    public List<FunctionDetails> Functions { get; set; } = [];
    public required string ResourceGroup { get; init; }
    public required string Subscription { get; init; }
    public string AnimatingFrame { get; set; } = "";
    public DateTime LastUpdated { get; set; }
    
    public required string Id { get; init; }

    public int CompareTo(FunctionAppDetails? other)
    {
        if (other is null)
        {
            return 1;
        }
        
        return StringComparer.Ordinal.Compare(Name, other.Name);
    }
}
