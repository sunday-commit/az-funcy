namespace Funcy.Core.Model;

public class FunctionDetails : IComparable<FunctionDetails>, IHasKey
{
    public required string FunctionAppName { get; set; }
    public required string Name { get; set; }
    public required string Trigger { get; set; }

    // Reflects the AzureWebJobs.<name>.Disabled app setting. Persisted.
    public bool IsDisabled { get; set; }

    // Transient UI flag: a disable/enable toggle is in flight. Not persisted.
    public bool IsToggling { get; set; }
    public int CompareTo(FunctionDetails? other)
    {
        if (other is null)
        {
            return 1;
        }
        
        var byFunctionApp = StringComparer.Ordinal.Compare(FunctionAppName, other.FunctionAppName);
        return byFunctionApp != 0 ? byFunctionApp : StringComparer.Ordinal.Compare(Name, other.Name);
    }

    public string Key => FunctionAppName + Name;
}