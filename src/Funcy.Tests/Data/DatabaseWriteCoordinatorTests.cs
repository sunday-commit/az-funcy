using Funcy.Infrastructure.Data;
using Xunit;

namespace Funcy.Tests.Data;

public class DatabaseWriteCoordinatorTests
{
    [Fact]
    public async Task ExecuteAsync_ConcurrentOperations_NeverOverlap()
    {
        var coordinator = new DatabaseWriteCoordinator();
        var gate = new object();
        var concurrentOperations = 0;
        var maximumConcurrency = 0;

        var tasks = Enumerable.Range(0, 20).Select(_ => coordinator.ExecuteAsync(async () =>
        {
            lock (gate)
            {
                concurrentOperations++;
                maximumConcurrency = Math.Max(maximumConcurrency, concurrentOperations);
            }

            await Task.Delay(5);

            lock (gate)
            {
                concurrentOperations--;
            }
        }));

        await Task.WhenAll(tasks);

        Assert.Equal(1, maximumConcurrency);
    }
}
