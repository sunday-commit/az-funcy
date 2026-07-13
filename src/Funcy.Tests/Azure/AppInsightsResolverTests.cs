using Funcy.Infrastructure.Azure;
using Xunit;

namespace Funcy.Tests.Azure;

public class AppInsightsResolverTests
{
    [Fact]
    public async Task ResolveResourceIdAsync_AfterTransientFailure_Retries()
    {
        var lookup = new SequenceLookup(
            _ => throw new InvalidOperationException("Transient failure"),
            _ => Task.FromResult<string?>("/subscriptions/sub/resourceGroups/rg/providers/microsoft.insights/components/app"));
        var resolver = new AppInsightsResolver(lookup);

        var first = await resolver.ResolveResourceIdAsync("app-id", CancellationToken.None);
        var second = await resolver.ResolveResourceIdAsync("app-id", CancellationToken.None);

        Assert.Null(first);
        Assert.NotNull(second);
        Assert.Equal(2, lookup.CallCount);
    }

    [Fact]
    public async Task ResolveResourceIdAsync_AfterCancellation_Retries()
    {
        var lookup = new SequenceLookup(
            token => Task.FromCanceled<string?>(token),
            _ => Task.FromResult<string?>("resource-id"));
        var resolver = new AppInsightsResolver(lookup);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => resolver.ResolveResourceIdAsync("app-id", cancellation.Token));
        var result = await resolver.ResolveResourceIdAsync("app-id", CancellationToken.None);

        Assert.Equal("resource-id", result);
        Assert.Equal(2, lookup.CallCount);
    }

    [Fact]
    public async Task ResolveResourceIdAsync_AfterSuccessfulResolution_UsesCache()
    {
        var lookup = new SequenceLookup(_ => Task.FromResult<string?>("resource-id"));
        var resolver = new AppInsightsResolver(lookup);

        var first = await resolver.ResolveResourceIdAsync("app-id", CancellationToken.None);
        var second = await resolver.ResolveResourceIdAsync("app-id", CancellationToken.None);

        Assert.Equal("resource-id", first);
        Assert.Equal("resource-id", second);
        Assert.Equal(1, lookup.CallCount);
    }

    private sealed class SequenceLookup(params Func<CancellationToken, Task<string?>>[] results)
        : IAppInsightsResourceIdLookup
    {
        private int _index;

        public int CallCount { get; private set; }

        public Task<string?> ResolveAsync(string functionAppArmId, CancellationToken cancellationToken)
        {
            CallCount++;
            return results[_index++](cancellationToken);
        }
    }
}
