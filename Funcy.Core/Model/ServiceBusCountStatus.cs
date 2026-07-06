namespace Funcy.Core.Model;

public enum ServiceBusCountStatus
{
    // Not a Service Bus trigger, so counts are not applicable.
    None,
    // Counts are being fetched.
    Loading,
    // Counts were fetched successfully.
    Loaded,
    // Counts could not be resolved (e.g. namespace lookup or ARM call failed).
    Failed
}
