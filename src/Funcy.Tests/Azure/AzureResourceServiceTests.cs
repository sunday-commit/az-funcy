using Funcy.Infrastructure.Azure;
using Funcy.Infrastructure.Shell;
using Xunit;

namespace Funcy.Tests.Azure;

public class AzureResourceServiceTests
{
    [Fact]
    public async Task GetServiceBusNamespacesAsync_WhenResponseIsPaged_ReturnsEveryPage()
    {
        var commandRunner = new SequenceCommandRunner(
            """
            {
              "count": 1,
              "data": [{ "id": "/namespaces/one", "name": "one" }],
              "skip_token": "next-page"
            }
            """,
            """
            {
              "count": 1,
              "data": [{ "id": "/namespaces/two", "name": "two" }]
            }
            """);
        var service = new AzureResourceService(commandRunner);

        var namespaces = await service.GetServiceBusNamespacesAsync("subscription-id", CancellationToken.None);

        Assert.Equal(["one", "two"], namespaces.Select(n => n.Name));
        Assert.Equal(2, commandRunner.Arguments.Count);
        Assert.DoesNotContain("--skip-token", commandRunner.Arguments[0]);
        Assert.Contains("--skip-token \"next-page\"", commandRunner.Arguments[1]);
    }

    private sealed class SequenceCommandRunner(params string[] responses) : IShellCommandRunner
    {
        private int _responseIndex;

        public List<string> Arguments { get; } = [];

        public Task<string> RunAsync(string command, string arguments, CancellationToken cancellationToken = default)
        {
            Arguments.Add(arguments);
            return Task.FromResult(responses[_responseIndex++]);
        }
    }
}
