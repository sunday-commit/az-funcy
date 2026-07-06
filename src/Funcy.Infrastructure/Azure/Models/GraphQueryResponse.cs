using System.Text.Json.Serialization;

namespace Funcy.Infrastructure.Azure.Models;

public record GraphQueryResponse(
    int Count,
    List<FunctionAppGraphRow> Data,
    [property: JsonPropertyName("skip_token")]
    string SkipToken,
    [property: JsonPropertyName("total_records")]
    int TotalCount);

public record FunctionAppGraphRow(string Id, string Name, string State, Dictionary<string, string> Tags, string ResourceGroup, string SubscriptionId);

public record NamespaceGraphResponse(int Count, List<NamespaceGraphRow> Data);

public record NamespaceGraphRow(string Id, string Name);
