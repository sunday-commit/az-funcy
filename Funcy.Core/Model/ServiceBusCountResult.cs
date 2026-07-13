namespace Funcy.Core.Model;

// Runtime message counts resolved for a single Service Bus triggered function.
// Success is false when the counts could not be determined; in that case the counts are null.
// The queue/topic/subscription names carry the %SettingName% indirection resolved against the
// function app's application settings (raw when a setting is missing); they are per-runtime display
// values and are never persisted over the raw names in SQLite.
public sealed record ServiceBusCountResult(
    string FunctionKey,
    long? ActiveMessages,
    long? DeadLetteredMessages,
    bool Success,
    string? QueueName = null,
    string? TopicName = null,
    string? SubscriptionName = null,
    // ARM id of the namespace the counts were resolved against, so the caller can persist it and
    // skip the resolution next time. Null when the namespace could not be resolved.
    string? NamespaceId = null,
    string? ErrorMessage = null);
