using System.Text;
using System.Text.Json;
using Funcy.Infrastructure.Azure.Models;
using Funcy.Infrastructure.Shell;

namespace Funcy.Infrastructure.Azure;

public class AzureResourceService : IAzureResourceService
{
    public async Task<string> GetCurrentSubscriptionId()
    {
        return await ShellCommandRunner.RunAsync("az", "account show --query id -o tsv");
    }
    
    public async Task<bool> HasAnyFunctionAppsAsync(string subscriptionId)
    {
        var query =
            $"Resources | where subscriptionId == '{subscriptionId}' " +
            "| where type =~ 'microsoft.web/sites' " +
            "| where kind has 'functionapp' " +
            "| limit 1 " +
            "| project id";

        try
        {
            var graphArgs = BuildGraphArgs(query, 1, null);
            var json = await ShellCommandRunner.RunAsync("az", graphArgs);
            var response = JsonSerializer.Deserialize<GraphQueryResponse>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return response?.Count > 0;
        }
        catch
        {
            return true; // Assume has apps if probe fails
        }
    }

    public async Task<List<FunctionAppGraphRow>> GetAllFunctionApps(string subscriptionId)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };
        
        var query =
            $"Resources | where subscriptionId == '{subscriptionId}' " +
            "| where type =~ 'microsoft.web/sites' " +
            "| where kind has 'functionapp' " +
            "| project id, name, resourceGroup, subscriptionId, tags, state=tostring(properties.state)";
        const int pageSize = 1000;

        var results = new List<FunctionAppGraphRow>();
        string? skipToken = null;

        while (true)
        {
            var graphQuery = BuildGraphArgs(query, pageSize, skipToken);
            var functionJson = await ShellCommandRunner.RunAsync("az", graphQuery);
            var response = JsonSerializer.Deserialize<GraphQueryResponse>(functionJson, options);
            if (response is null)
            {
                throw new InvalidOperationException("Failed to deserialize response");
            }
            
            results.AddRange(response.Data);
            skipToken = response.SkipToken;
            
            if (string.IsNullOrWhiteSpace(skipToken))
            {
                break;
            }
        }

        return results;
    }
    
    public async Task<string?> GetServiceBusNamespaceIdAsync(string namespaceName)
    {
        // Not filtered to the current subscription: the namespace may live in a different one.
        var query =
            "Resources | where type =~ 'microsoft.servicebus/namespaces' " +
            $"| where name =~ '{namespaceName}' " +
            "| project id, name";

        var graphArgs = BuildGraphArgs(query, 1, null);
        var json = await ShellCommandRunner.RunAsync("az", graphArgs);
        var response = JsonSerializer.Deserialize<NamespaceGraphResponse>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        return response?.Data.FirstOrDefault()?.Id;
    }

    private static string BuildGraphArgs(string query, int first, string? skipToken)
    {
        var sb = new StringBuilder(256);
        sb.Append("graph query");
        sb.Append(" --first ");
        sb.Append(first);
        sb.Append(" -q ");
        sb.Append(Quote(query));

        if (!string.IsNullOrWhiteSpace(skipToken))
        {
            sb.Append(" --skip-token ");
            sb.Append(Quote(skipToken));
        }
        sb.Append(" -o json");
        return sb.ToString();
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }
}

public interface IAzureResourceService
{
    Task<string> GetCurrentSubscriptionId();
    Task<List<FunctionAppGraphRow>> GetAllFunctionApps(string subscriptionId);
    Task<bool> HasAnyFunctionAppsAsync(string subscriptionId);
    Task<string?> GetServiceBusNamespaceIdAsync(string namespaceName);
}
