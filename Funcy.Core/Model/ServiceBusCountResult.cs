namespace Funcy.Core.Model;

// Runtime message counts resolved for a single Service Bus triggered function.
// Success is false when the counts could not be determined; in that case the counts are null.
public sealed record ServiceBusCountResult(
    string FunctionKey,
    long? ActiveMessages,
    long? DeadLetteredMessages,
    bool Success);
