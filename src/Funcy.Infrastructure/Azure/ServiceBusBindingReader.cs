using System.Text.Json;
using Funcy.Infrastructure.Azure.Models;

namespace Funcy.Infrastructure.Azure;

// Values read from a serviceBusTrigger binding. Strings are kept raw and may contain
// %SettingName% indirection tokens which are resolved later against app settings.
public sealed record ServiceBusTriggerTarget(
    string? QueueName,
    string? TopicName,
    string? SubscriptionName,
    string? ConnectionSetting);

public static class ServiceBusBindingReader
{
    // Returns the Service Bus target for a function's trigger binding, or null when the
    // function is not triggered by Service Bus.
    public static ServiceBusTriggerTarget? TryRead(FunctionConfig config)
    {
        var trigger = config.Bindings.FirstOrDefault(b =>
            b.Type.EndsWith("Trigger", StringComparison.OrdinalIgnoreCase) &&
            (b.Direction is null || b.Direction.Equals("in", StringComparison.OrdinalIgnoreCase)));

        if (trigger is null || !trigger.Type.Equals("serviceBusTrigger", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return new ServiceBusTriggerTarget(
            ReadString(trigger.Extra, "queueName"),
            ReadString(trigger.Extra, "topicName"),
            ReadString(trigger.Extra, "subscriptionName"),
            ReadString(trigger.Extra, "connection"));
    }

    private static string? ReadString(IReadOnlyDictionary<string, JsonElement> extra, string key)
    {
        var match = extra.FirstOrDefault(kvp => kvp.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (match.Key is null || match.Value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var value = match.Value.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
