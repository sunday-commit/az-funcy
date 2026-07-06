using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager.AppService.Models;
using Funcy.Core.Interfaces;
using Funcy.Core.Model;
using Funcy.Data;
using Funcy.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Funcy.Infrastructure.Azure;

public class FunctionAppManagementService(ILogger<FunctionAppManagementService> logger, IDbContextFactory<FunctionAppDbContext> dbContextFactory) : IFunctionAppManagementService
{
    private readonly ArmClient _client = new(new DefaultAzureCredential());
    
    public async Task StartFunction(FunctionAppDetails functionAppDetails)
    {
        var webSiteResource = _client.GetWebSiteResource(ResourceIdentifier.Parse(functionAppDetails.Id));
        await webSiteResource.StartAsync();
        await UpdateFunctionApp(functionAppDetails, FunctionAction.Start.GetFunctionState());
        logger.LogInformation("Started Function App: {FunctionAppName}", functionAppDetails.Name);
    }

    public async Task StopFunction(FunctionAppDetails functionAppDetails)
    {
        var webSiteResource = _client.GetWebSiteResource(ResourceIdentifier.Parse(functionAppDetails.Id));
        await webSiteResource.StopAsync();
        await UpdateFunctionApp(functionAppDetails, FunctionAction.Stop.GetFunctionState());
        logger.LogInformation("Stopped Function App: {FunctionAppName}", functionAppDetails.Name);
    }

    public async Task SwapFunction(FunctionAppDetails functionAppDetails, FunctionAppSlotDetails functionAppSlot)
    {
        try
        {
            await Task.Yield();
            var webSiteResource = _client.GetWebSiteResource(ResourceIdentifier.Parse(functionAppDetails.Id));
            
            var stagingResource = _client.GetWebSiteSlotResource(ResourceIdentifier.Parse(functionAppSlot.Id));

            //StartSlotAsync doesn't always return and can sit and idle forever. Use non-async method
            stagingResource.StartSlot();
        
            await webSiteResource.SwapSlotWithProductionAsync(WaitUntil.Completed,
                new CsmSlotEntity(functionAppSlot.Name, true));
            
            await stagingResource.StopSlotAsync();
            await UpdateFunctionApp(functionAppDetails, FunctionAction.Start.GetFunctionState());
            logger.LogInformation("Swapped Function App: {FunctionAppName}", functionAppDetails.Name);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Error while swapping {FunctionAppName}",  functionAppDetails.Name);
            throw;
        }
    }
    
    public async Task SetFunctionDisabled(FunctionAppDetails functionAppDetails, string functionName, bool disabled)
    {
        var webSiteResource = _client.GetWebSiteResource(ResourceIdentifier.Parse(functionAppDetails.Id));

        // Fetch the full app-settings collection, mutate only the one Disabled flag, and PUT the
        // whole dictionary back. A partial update would wipe every other setting.
        var settings = await webSiteResource.GetApplicationSettingsAsync();
        FunctionAppSettings.ApplyDisabledSetting(settings.Value.Properties, functionName, disabled);
        await webSiteResource.UpdateApplicationSettingsAsync(settings.Value);

        await UpdateFunctionDisabledState(functionAppDetails, functionName, disabled);
        logger.LogInformation("{Action} function {FunctionName} on {FunctionAppName}",
            disabled ? "Disabled" : "Enabled", functionName, functionAppDetails.Name);
    }

    private async Task UpdateFunctionDisabledState(FunctionAppDetails functionAppDetails, string functionName, bool disabled)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        var existing = await dbContext.FunctionApps
            .Include(f => f.Functions)
            .FirstOrDefaultAsync(f =>
                f.Name == functionAppDetails.Name && f.ResourceGroup == functionAppDetails.ResourceGroup &&
                f.Subscription == functionAppDetails.Subscription);

        var function = existing?.Functions.FirstOrDefault(fn => fn.Name == functionName);
        if (function is not null)
        {
            function.IsDisabled = disabled;
            await dbContext.SaveChangesAsync();
        }
    }

    private async Task UpdateFunctionApp(FunctionAppDetails functionAppDetails, FunctionState state)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync();

        var existing = await dbContext.FunctionApps
            .Include(f => f.Functions)
            .FirstOrDefaultAsync(f =>
                f.Name == functionAppDetails.Name && f.ResourceGroup == functionAppDetails.ResourceGroup &&
                f.Subscription == functionAppDetails.Subscription);
        
        if (existing is not null)
        {
            existing.State = state;
            existing.UpdatedAt = DateTime.UtcNow;
            functionAppDetails.LastUpdated = existing.UpdatedAt;
            await dbContext.SaveChangesAsync();
        }
    }
}