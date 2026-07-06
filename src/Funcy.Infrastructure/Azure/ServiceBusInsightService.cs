using System.Collections.Concurrent;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.ServiceBus;
using Funcy.Core.Interfaces;
using Funcy.Core.Model;
using Microsoft.Extensions.Logging;

namespace Funcy.Infrastructure.Azure;

public class ServiceBusInsightService(
    ILogger<ServiceBusInsightService> logger,
    IAzureResourceService resourceService,
    ArmClient client) : IServiceBusInsightService
{
    // Cached namespace ARM id per (function app, connection setting). Null is cached too so a
    // namespace that cannot be resolved is not looked up again for the process lifetime.
    private readonly ConcurrentDictionary<string, string?> _namespaceIdCache = new();

    public async Task<IReadOnlyList<ServiceBusCountResult>> GetCountsAsync(
        string functionAppArmId,
        IReadOnlyList<FunctionDetails> serviceBusFunctions,
        CancellationToken cancellationToken)
    {
        var results = new List<ServiceBusCountResult>(serviceBusFunctions.Count);
        if (serviceBusFunctions.Count == 0)
        {
            return results;
        }

        // Only pay for the app settings fetch when something still needs to be resolved.
        var needSettings = serviceBusFunctions.Any(f =>
            !_namespaceIdCache.ContainsKey(CacheKey(functionAppArmId, f.ConnectionSetting))
            || HasPlaceholder(f.QueueName)
            || HasPlaceholder(f.TopicName)
            || HasPlaceholder(f.SubscriptionName));

        ServiceBusConnectionResolver? resolver = null;
        if (needSettings)
        {
            resolver = await TryBuildResolverAsync(functionAppArmId, cancellationToken);
        }

        foreach (var function in serviceBusFunctions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await GetCountsForFunctionAsync(functionAppArmId, function, resolver, cancellationToken));
        }

        return results;
    }

    private async Task<ServiceBusCountResult> GetCountsForFunctionAsync(
        string functionAppArmId,
        FunctionDetails function,
        ServiceBusConnectionResolver? resolver,
        CancellationToken cancellationToken)
    {
        try
        {
            var namespaceId = await ResolveNamespaceIdAsync(functionAppArmId, function.ConnectionSetting, resolver);
            if (namespaceId is null)
            {
                logger.LogWarning("Could not resolve Service Bus namespace for function {Function}", function.Key);
                return Failed(function);
            }

            var nsId = new ResourceIdentifier(namespaceId);
            var queueName = Resolve(resolver, function.QueueName);
            var topicName = Resolve(resolver, function.TopicName);
            var subscriptionName = Resolve(resolver, function.SubscriptionName);

            if (!string.IsNullOrEmpty(topicName) && !string.IsNullOrEmpty(subscriptionName))
            {
                var id = ServiceBusSubscriptionResource.CreateResourceIdentifier(
                    nsId.SubscriptionId, nsId.ResourceGroupName, nsId.Name, topicName, subscriptionName);
                var data = (await client.GetServiceBusSubscriptionResource(id).GetAsync(cancellationToken)).Value.Data;
                return new ServiceBusCountResult(function.Key,
                    data.CountDetails?.ActiveMessageCount ?? 0,
                    data.CountDetails?.DeadLetterMessageCount ?? 0,
                    true);
            }

            if (!string.IsNullOrEmpty(queueName))
            {
                var id = ServiceBusQueueResource.CreateResourceIdentifier(
                    nsId.SubscriptionId, nsId.ResourceGroupName, nsId.Name, queueName);
                var data = (await client.GetServiceBusQueueResource(id).GetAsync(cancellationToken)).Value.Data;
                return new ServiceBusCountResult(function.Key,
                    data.CountDetails?.ActiveMessageCount ?? 0,
                    data.CountDetails?.DeadLetterMessageCount ?? 0,
                    true);
            }

            return Failed(function);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to fetch Service Bus counts for function {Function}", function.Key);
            return Failed(function);
        }
    }

    private async Task<string?> ResolveNamespaceIdAsync(
        string functionAppArmId,
        string? connectionSetting,
        ServiceBusConnectionResolver? resolver)
    {
        var cacheKey = CacheKey(functionAppArmId, connectionSetting);
        if (_namespaceIdCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        string? namespaceId = null;
        var namespaceName = resolver?.ResolveNamespace(connectionSetting);
        if (!string.IsNullOrEmpty(namespaceName))
        {
            namespaceId = await resourceService.GetServiceBusNamespaceIdAsync(namespaceName);
        }

        _namespaceIdCache[cacheKey] = namespaceId;
        return namespaceId;
    }

    private async Task<ServiceBusConnectionResolver?> TryBuildResolverAsync(
        string functionAppArmId,
        CancellationToken cancellationToken)
    {
        try
        {
            var webSite = client.GetWebSiteResource(ResourceIdentifier.Parse(functionAppArmId));
            var settings = (await webSite.GetApplicationSettingsAsync(cancellationToken)).Value.Properties;
            return new ServiceBusConnectionResolver(
                new Dictionary<string, string>(settings, StringComparer.OrdinalIgnoreCase));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to fetch application settings for {FunctionApp}", functionAppArmId);
            return null;
        }
    }

    private static string? Resolve(ServiceBusConnectionResolver? resolver, string? value)
        => resolver is null ? value : resolver.ResolveValue(value);

    private static ServiceBusCountResult Failed(FunctionDetails function)
        => new(function.Key, null, null, false);

    private static bool HasPlaceholder(string? value) => value?.Contains('%') == true;

    private static string CacheKey(string functionAppArmId, string? connectionSetting)
        => $"{functionAppArmId}|{connectionSetting ?? string.Empty}";
}
