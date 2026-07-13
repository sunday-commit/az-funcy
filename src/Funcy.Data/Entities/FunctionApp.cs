using Funcy.Core.Model;

namespace Funcy.Data.Entities;

public class FunctionApp
{
    public long Id { get; set; }
    public required string AzureId { get; set; }
    public required string Name { get; set; }
    public required string ResourceGroup { get; set; }
    public required string Subscription { get; set; }
    public FunctionState State { get; set; }
    public bool IsPinned { get; set; }
    public List<Function> Functions { get; set; } = [];
    public List<FunctionAppSlot> Slots { get; set; } = [];
    public List<FunctionAppTag> Tags { get; set; } = [];
    public DateTime UpdatedAt { get; set; }
}