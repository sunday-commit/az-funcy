using Funcy.Core.Model;
using Funcy.Infrastructure.Azure;
using Funcy.Infrastructure.Shell;

namespace Funcy.Demo;

/// <summary>Serves the demo estate's subscriptions.</summary>
internal sealed class DemoSubscriptionService(DemoEstate estate) : ISubscriptionService
{
    public Task<List<SubscriptionDetails>> GetSubscriptions() => Task.FromResult(estate.Subscriptions());
}

/// <summary>Demo mode shells out to nothing, so there is nothing to validate.</summary>
internal sealed class DemoToolValidationService : IToolValidationService
{
    public Task<ToolValidationResult> ValidateRequiredToolsAsync() =>
        Task.FromResult(new ToolValidationResult(IsValid: true, MissingTools: [], InstallInstructions: []));
}

/// <summary>
/// A permanently healthy session. There is no az session behind demo mode, so the probe loop is a
/// no-op and re-login is inert: the expired-session banner can never appear over the recording.
/// </summary>
internal sealed class DemoSessionMonitor : IAzureSessionMonitor
{
    // The demo session is permanently healthy, so there is nothing to notify about; subscribing is
    // accepted and discarded rather than tracked.
    public event Action? Changed
    {
        add { }
        remove { }
    }

    public AzureSessionState State => AzureSessionState.Healthy;

    public Func<Task>? ReAuthenticatedCallback { get; set; }

    public Task RunProbeLoopAsync(CancellationToken token) => Task.CompletedTask;

    public Task ProbeOnceAsync(CancellationToken token) => Task.CompletedTask;

    public void ReportPossibleAuthFailure(Exception? ex)
    {
    }

    public void ReportPossibleAuthFailure(string? outputOrMessage)
    {
    }

    public void BeginReLogin()
    {
    }
}
