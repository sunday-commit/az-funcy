using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Funcy.Core.Interfaces;
using Funcy.Core.Model;
using Funcy.Data;
using Funcy.Data.Entities;
using Funcy.Infrastructure.Azure.Models;
using Funcy.Infrastructure.Mappers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Funcy.Infrastructure.Azure;

public class AzureFunctionService(
    ILogger<AzureFunctionService> logger,
    IAzureResourceService resourceService,
    ArmClient client,
    IDbContextFactory<FunctionAppDbContext> dbContextFactory) : IAzureFunctionService
{
    private static readonly SemaphoreSlim DbWriteLock = new(1, 1);
    
    public async Task<List<FunctionAppDetails>> GetFunctionsFromDatabase(string subscriptionId)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();
        var functionAppList = dbContext.FunctionApps.Include(x => x.Functions).Include(x => x.Slots).Include(x => x.Tags)
            .Where(f => f.Subscription == subscriptionId);
        var functionAppDetailsList = functionAppList.Select(x => x.Map()).ToList();
        functionAppDetailsList.Sort();
        return functionAppDetailsList;
    }

    public async IAsyncEnumerable<FunctionAppFetchResult> GetFunctionAppDetailsAsync(string subscriptionId,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        var channel = Channel.CreateUnbounded<FunctionAppFetchResult>();
        var allFunctionApps = await resourceService.GetAllFunctionApps(subscriptionId);
        
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);

        var cleanupTask = CleanUpFunctionApps(subscriptionId, allFunctionApps, cancellationToken);

        // Lock database access to prevent concurrent writes from multiple subscriptions
        // Use a timeout to allow cancelled operations to complete first
        var lockAcquired = await DbWriteLock.WaitAsync(TimeSpan.FromSeconds(5), cancellationToken);
        
        if (!lockAcquired)
        {
            logger.LogWarning("Could not acquire database write lock within timeout for subscription {SubscriptionId}", subscriptionId);
            throw new TimeoutException("Database write lock could not be acquired - another synchronization is still running");
        }
        
        try
        {
            var existingApps = await dbContext.FunctionApps
                .Include(f => f.Tags)
                .Where(f => f.Subscription == subscriptionId)
                .ToDictionaryAsync(f => f.AzureId, cancellationToken);

            foreach (var functionAppGraphRow in allFunctionApps)
            {
                existingApps.TryGetValue(functionAppGraphRow.Id, out var existing);
                var functionApp = AddOrUpdateFunctionApp(existing, functionAppGraphRow, dbContext);
                var details = functionApp.Map();
                await channel.Writer.WriteAsync(new FunctionAppFetchResult(
                        functionAppGraphRow.Name,
                        details,
                        FunctionAppUpdateKind.Inventory),
                    cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            DbWriteLock.Release();
        }
        
        await cleanupTask;
        channel.Writer.Complete();

        
        while (await channel.Reader.WaitToReadAsync(cancellationToken))
        {
            while (channel.Reader.TryRead(out var item))
            {
                yield return item;
            }
        }

        stopwatch.Stop();
        logger.LogInformation("Fetched all function app details in {ElapsedMilliseconds}ms",
            stopwatch.ElapsedMilliseconds);
    }

    private async Task CleanUpFunctionApps(string subscriptionId, List<FunctionAppGraphRow> allFunctionApps, CancellationToken cancellationToken)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var azureIds = allFunctionApps.Select(f => f.Id).ToHashSet();
        var toRemove = await dbContext.FunctionApps
            .Where(f => f.Subscription == subscriptionId && !azureIds.Contains(f.AzureId))
            .ToListAsync(cancellationToken);

        if (toRemove.Any())
        {
            dbContext.FunctionApps.RemoveRange(toRemove);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Removed {Count} function apps that no longer exist", toRemove.Count);
        }
    }

    public async IAsyncEnumerable<FunctionAppFetchResult> GetFunctionAppFunctionsAndSlotsAsync(
        List<FunctionAppDetails> functionAppDetails, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var stopwatch = new Stopwatch();
        stopwatch.Start();
        var channel = Channel.CreateUnbounded<FunctionAppFetchResult>();
        var throttler = new SemaphoreSlim(5);
        var tasks = new List<Task>();
        
        _ = Task.Run(async () =>
        {
            try
            {
                foreach (var functionAppDetail in functionAppDetails)
                {
                    await throttler.WaitAsync(cancellationToken);

                    var getFromAzureTask = Task.Run(async () =>
                    {
                        try
                        {
                            var updatedDetails = await GetFunctionAppDetails(functionAppDetail);
                            await channel.Writer.WriteAsync(new FunctionAppFetchResult(
                                    updatedDetails.Name,
                                    updatedDetails,
                                    FunctionAppUpdateKind.Details),
                                cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            logger.LogInformation("Operation cancelled");
                        }
                        catch (Exception e)
                        {
                            if (!cancellationToken.IsCancellationRequested)
                            {
                                await channel.Writer.WriteAsync(
                                    new FunctionAppFetchResult(
                                        functionAppDetail.Name,
                                        null,
                                        FunctionAppUpdateKind.Details,
                                        e.Message),
                                    CancellationToken.None);
                            }
                        }
                        finally
                        {
                            throttler.Release();
                        }
                    }, cancellationToken);
                    tasks.Add(getFromAzureTask);
                }
                
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException e)
            {
                logger.LogInformation("Operation cancelled");
            }
            finally
            {
                throttler.Dispose();
                channel.Writer.Complete();
            }
            
        }, cancellationToken);
        
        await foreach (var item in channel.Reader.ReadAllAsync(cancellationToken))
        {
            yield return item;
        }
    }

    public async Task<FunctionAppDetails> GetFunctionAppDetails(FunctionAppDetails functionAppDetails)
    {
        if (functionAppDetails.State == FunctionState.Stopped)
        {
            return functionAppDetails;
        }

        var sw = Stopwatch.StartNew();
        var dbContext = await dbContextFactory.CreateDbContextAsync();
        var existing = await dbContext.FunctionApps
            .Include(f => f.Functions)
            .Include(f => f.Slots)
            .Include(f => f.Tags)
            .FirstAsync(f => f.AzureId == functionAppDetails.Id);

        var webSiteResource = client.GetWebSiteResource(ResourceIdentifier.Parse(functionAppDetails.Id));

        var functionTask = Task.Run(() =>
            FetchFunctionListAsync(webSiteResource, functionAppDetails.Name, functionAppDetails.State.ToString()));
        var slotTask = Task.Run(() => FetchSlotListAsync(webSiteResource, functionAppDetails.Name));
        await Task.WhenAll(functionTask, slotTask);

        if (functionTask.Result is not null)
        {
            dbContext.Functions.RemoveRange(existing.Functions);
            existing.Functions = functionTask.Result;
        }

        if (slotTask.Result is not null)
        {
            dbContext.FunctionAppSlots.RemoveRange(existing.Slots);
            existing.Slots = slotTask.Result;
        }

        await dbContext.SaveChangesAsync();

        sw.Stop();
        logger.LogInformation("Fetched all function app details in {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);

        return existing.Map();
    }

    private List<Function>? FetchFunctionListAsync(WebSiteResource webSiteResource, string functionAppName,
        string functionAppState)
    {
        if (functionAppState.Equals("Stopped", StringComparison.OrdinalIgnoreCase))
            return null;

        List<Function>? functionList = [];
        try
        {
            var sw = Stopwatch.StartNew();
            var websiteFunctions = webSiteResource.GetSiteFunctions();
            foreach (var websiteFunction in websiteFunctions)
            {
                var config = websiteFunction.Data.Config.ToObjectFromJson<FunctionConfig>(new JsonSerializerOptions
                    { PropertyNameCaseInsensitive = true });
                var trigger = "";
                if (config is not null)
                {
                    var triggerBinding = config.Bindings.FirstOrDefault(b =>
                        b.Type.EndsWith("Trigger", StringComparison.OrdinalIgnoreCase) &&
                        (b.Direction == null || b.Direction.Equals("in", StringComparison.OrdinalIgnoreCase)));
                    trigger = triggerBinding?.Type ?? "";
                }

                functionList.Add(new Function
                {
                    AzureId = websiteFunction.Id.ToString(), Name = websiteFunction.Id.Name, Trigger = Capitalize(trigger),
                    IsDisabled = websiteFunction.Data.IsDisabled ?? false
                });
            }

            sw.Stop();
            logger.LogInformation("Fetched function list for {FunctionAppName} in {ElapsedMilliseconds}ms",
                functionAppName, sw.ElapsedMilliseconds);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error while fetching function list details {FunctionAppName}", functionAppName);
            functionList = null;
        }

        return functionList;
    }
    
    static string Capitalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        return char.ToUpperInvariant(value[0]) + value[1..];
    }

    private List<FunctionAppSlot>? FetchSlotListAsync(WebSiteResource webSite, string functionAppName)
    {
        List<FunctionAppSlot>? slotList = null;
        try
        {
            var sw = Stopwatch.StartNew();
            var slots = webSite.GetWebSiteSlots();
            slotList = Enumerable.Select(slots,
                slot => new FunctionAppSlot
                {
                    FullName = slot.Data.Name, Name = slot.Id.Name, AzureId = slot.Id.ToString(),
                    State = Enum.Parse<FunctionState>(slot.Data.State)
                }).ToList();
            sw.Stop();
            logger.LogInformation("Fetched slot list for {FunctionAppName} in {ElapsedMilliseconds}ms", functionAppName,
                sw.ElapsedMilliseconds);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error while fetching slot list details {FunctionAppName}", functionAppName);
        }

        return slotList;
    }

    private FunctionApp AddOrUpdateFunctionApp(FunctionApp? functionApp, FunctionAppGraphRow functionAppGraphRow,
        FunctionAppDbContext dbContext)
    {
        if (functionApp is null)
        {
            functionApp = new FunctionApp()
            {
                AzureId = functionAppGraphRow.Id,
                Name = functionAppGraphRow.Name,
                State = Enum.Parse<FunctionState>(functionAppGraphRow.State),
                Subscription = functionAppGraphRow.SubscriptionId,
                ResourceGroup = functionAppGraphRow.ResourceGroup,
                UpdatedAt = DateTime.UtcNow
            };
            dbContext.FunctionApps.Add(functionApp);
        }
        else
        {
            functionApp.State = Enum.Parse<FunctionState>(functionAppGraphRow.State);
            functionApp.UpdatedAt = DateTime.UtcNow;
            dbContext.FunctionAppTags.RemoveRange(functionApp.Tags);
        }

        functionApp.Tags = (functionAppGraphRow.Tags ?? [])
            .Select(kvp => new FunctionAppTag { Key = kvp.Key, Value = kvp.Value })
            .ToList();

        return functionApp;
    }
}
