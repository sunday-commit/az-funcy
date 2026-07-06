namespace Funcy.Core.Model;

public class FunctionDetails : IComparable<FunctionDetails>, IHasKey
{
    public required string FunctionAppName { get; set; }
    public required string Name { get; set; }
    public required string Trigger { get; set; }

    // Service Bus trigger target, captured from the function binding config.
    // May contain %SettingName% indirection tokens (stored raw).
    public string? QueueName { get; set; }
    public string? TopicName { get; set; }
    public string? SubscriptionName { get; set; }
    public string? ConnectionSetting { get; set; }

    // Live runtime counts for Service Bus triggers; null until resolved.
    public long? ActiveMessages { get; set; }
    public long? DeadLetteredMessages { get; set; }
    public ServiceBusCountStatus CountStatus { get; set; } = ServiceBusCountStatus.None;

    public bool IsServiceBusTrigger =>
        !string.IsNullOrEmpty(QueueName) ||
        (!string.IsNullOrEmpty(TopicName) && !string.IsNullOrEmpty(SubscriptionName));

    // Human-readable target: "topic/subscription" for topics, "queue" for queues.
    public string ListensTo
    {
        get
        {
            if (!string.IsNullOrEmpty(TopicName) && !string.IsNullOrEmpty(SubscriptionName))
            {
                return $"{TopicName}/{SubscriptionName}";
            }

            return QueueName ?? string.Empty;
        }
    }

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
