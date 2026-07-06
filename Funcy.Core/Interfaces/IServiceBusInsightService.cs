using Funcy.Core.Model;

namespace Funcy.Core.Interfaces;

public interface IServiceBusInsightService
{
    // Resolves active and dead-letter message counts for the given Service Bus triggered functions
    // of a function app. Namespace resolution is cached per (function app, connection setting);
    // the counts themselves are always fetched fresh. Failures are reported per function via the
    // result's Success flag and never throw.
    Task<IReadOnlyList<ServiceBusCountResult>> GetCountsAsync(
        string functionAppArmId,
        IReadOnlyList<FunctionDetails> serviceBusFunctions,
        CancellationToken cancellationToken);
}
