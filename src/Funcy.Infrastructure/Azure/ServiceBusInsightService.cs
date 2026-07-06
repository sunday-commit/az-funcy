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
        // Resolve %SettingName% binding names first so they feed both the ARM lookup and the
        // display, regardless of whether the count fetch below succeeds.
        var (queueName, topicName, subscriptionName) = ResolveBindingNames(resolver, function);
        LogUnresolved(function.QueueName, queueName);
        LogUnresolved(function.TopicName, topicName);
        LogUnresolved(function.SubscriptionName, subscriptionName);

        try
        {
            var namespaceId = await ResolveNamespaceIdAsync(functionAppArmId, function.ConnectionSetting, resolver);
            if (namespaceId is null)
            {
                logger.LogWarning("Could not resolve Service Bus namespace for function {Function}", function.Key);
                return Failed(function, queueName, topicName, subscriptionName);
            }

            var nsId = new ResourceIdentifier(namespaceId);

            if (!string.IsNullOrEmpty(topicName) && !string.IsNullOrEmpty(subscriptionName))
            {
                var id = ServiceBusSubscriptionResource.CreateResourceIdentifier(
                    nsId.SubscriptionId, nsId.ResourceGroupName, nsId.Name, topicName, subscriptionName);
                var data = (await client.GetServiceBusSubscriptionResource(id).GetAsync(cancellationToken)).Value.Data;
                return new ServiceBusCountResult(function.Key,
                    data.CountDetails?.ActiveMessageCount ?? 0,
                    data.CountDetails?.DeadLetterMessageCount ?? 0,
                    true,
                    queueName, topicName, subscriptionName);
            }

            if (!string.IsNullOrEmpty(queueName))
            {
                var id = ServiceBusQueueResource.CreateResourceIdentifier(
                    nsId.SubscriptionId, nsId.ResourceGroupName, nsId.Name, queueName);
                var data = (await client.GetServiceBusQueueResource(id).GetAsync(cancellationToken)).Value.Data;
                return new ServiceBusCountResult(function.Key,
                    data.CountDetails?.ActiveMessageCount ?? 0,
                    data.CountDetails?.DeadLetterMessageCount ?? 0,
                    true,
                    queueName, topicName, subscriptionName);
            }

            return Failed(function, queueName, topicName, subscriptionName);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to fetch Service Bus counts for function {Function}", function.Key);
            return Failed(function, queueName, topicName, subscriptionName);
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

    // Resolves a function's %SettingName% binding names against the app settings the resolver wraps.
    // Names with no matching setting (or when no resolver is available) are returned raw.
    public static (string? QueueName, string? TopicName, string? SubscriptionName) ResolveBindingNames(
        ServiceBusConnectionResolver? resolver, FunctionDetails function)
        => (Resolve(resolver, function.QueueName),
            Resolve(resolver, function.TopicName),
            Resolve(resolver, function.SubscriptionName));

    private static string? Resolve(ServiceBusConnectionResolver? resolver, string? value)
        => resolver is null ? value : resolver.ResolveValue(value);

    private void LogUnresolved(string? raw, string? resolved)
    {
        if (IsPlaceholder(raw) && HasPlaceholder(resolved))
        {
            logger.LogDebug(
                "Service Bus binding setting {Setting} not found in application settings; leaving name unresolved",
                raw![1..^1]);
        }
    }

    private static ServiceBusCountResult Failed(
        FunctionDetails function, string? queueName, string? topicName, string? subscriptionName)
        => new(function.Key, null, null, false, queueName, topicName, subscriptionName);

    private static bool HasPlaceholder(string? value) => value?.Contains('%') == true;

    private static bool IsPlaceholder(string? value)
        => value is { Length: >= 2 } && value[0] == '%' && value[^1] == '%';

    private static string CacheKey(string functionAppArmId, string? connectionSetting)
        => $"{functionAppArmId}|{connectionSetting ?? string.Empty}";
}
