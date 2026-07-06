using System.Runtime.CompilerServices;
using Funcy.Core.Model;

namespace Funcy.Core.Interfaces;

public interface IAzureFunctionService
{
    Task<List<FunctionAppDetails>> GetFunctionsFromDatabase(string subscriptionId);
    IAsyncEnumerable<FunctionAppFetchResult> GetFunctionAppDetailsAsync(string subscriptionId, CancellationToken cancellationToken);
    Task<FunctionAppDetails> GetFunctionAppDetails(FunctionAppDetails functionAppDetails);

    IAsyncEnumerable<FunctionAppFetchResult> GetFunctionAppFunctionsAndSlotsAsync(
        List<FunctionAppDetails> functionAppDetails, CancellationToken cancellationToken);

    Task SetPinnedAsync(string azureId, bool isPinned);
}