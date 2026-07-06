using System.Text.Json;
using System.Text.Json.Serialization;

namespace Funcy.Infrastructure.Azure.Models;

public class FunctionConfig
{
    public string GeneratedBy { get; set; } = "";
    public string ConfigurationSource { get; set; } = "";
    public Binding[] Bindings { get; set; } = [];
    public string EntryPoint { get; set; } = "";
}

public class Binding
{
    public required string Type { get; set; }
    public string? Direction { get; set; }
    public string? Name { get; set; }

    // Catch-all for binding-specific properties (queueName, topicName, subscriptionName,
    // connection, ...) that are not modelled explicitly.
    [JsonExtensionData]
    public Dictionary<string, JsonElement> Extra { get; set; } = new();
}
