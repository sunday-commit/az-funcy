namespace Funcy.Data.Entities;

public class Function
{
    public long Id { get; set; }
    public required string AzureId { get; set; }
    public required string Name { get; init; }
    public required string Trigger { get; init; }

    // Service Bus trigger target details, captured from the function binding config.
    // All optional; may contain %SettingName% indirection tokens (stored raw).
    public string? QueueName { get; init; }
    public string? TopicName { get; init; }
    public string? SubscriptionName { get; init; }
    public string? ConnectionSetting { get; init; }

    public bool IsDisabled { get; set; }
    public long FunctionAppId { get; set; }
    public FunctionApp? FunctionApp { get; set; }
}