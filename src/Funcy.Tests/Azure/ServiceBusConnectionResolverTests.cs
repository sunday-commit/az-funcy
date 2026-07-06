using Funcy.Infrastructure.Azure;
using Xunit;

namespace Funcy.Tests.Azure;

public class ServiceBusConnectionResolverTests
{
    private static ServiceBusConnectionResolver Resolver(params (string Key, string Value)[] settings)
        => new(settings.ToDictionary(s => s.Key, s => s.Value, StringComparer.OrdinalIgnoreCase));

    [Fact]
    public void ResolveNamespace_FullyQualifiedNamespaceVariant_ReturnsNamespace()
    {
        var resolver = Resolver(("MyConn__fullyQualifiedNamespace", "myns.servicebus.windows.net"));

        Assert.Equal("myns", resolver.ResolveNamespace("MyConn"));
    }

    [Fact]
    public void ResolveNamespace_ConnectionStringVariant_ReturnsNamespace()
    {
        var resolver = Resolver(("MyConn",
            "Endpoint=sb://myns2.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey=secret"));

        Assert.Equal("myns2", resolver.ResolveNamespace("MyConn"));
    }

    [Fact]
    public void ResolveNamespace_LegacyPrefixVariant_ReturnsNamespace()
    {
        var resolver = Resolver(("AzureWebJobsMyConn",
            "Endpoint=sb://legacyns.servicebus.windows.net/;SharedAccessKeyName=k;SharedAccessKey=secret"));

        Assert.Equal("legacyns", resolver.ResolveNamespace("MyConn"));
    }

    [Fact]
    public void ResolveNamespace_EmptyConnection_UsesDefaultSetting()
    {
        var resolver = Resolver(("AzureWebJobsServiceBus__fullyQualifiedNamespace", "defaultns.servicebus.windows.net"));

        Assert.Equal("defaultns", resolver.ResolveNamespace(""));
        Assert.Equal("defaultns", resolver.ResolveNamespace(null));
    }

    [Fact]
    public void ResolveNamespace_PercentIndirection_ResolvesViaSettings()
    {
        // %RealConn% -> setting "RealConn" holds the actual connection setting name.
        var resolver = Resolver(
            ("RealConn", "ActualConn"),
            ("ActualConn__fullyQualifiedNamespace", "indirectns.servicebus.windows.net"));

        Assert.Equal("indirectns", resolver.ResolveNamespace("%RealConn%"));
    }

    [Fact]
    public void ResolveNamespace_Unresolvable_ReturnsNull()
    {
        var resolver = Resolver(("SomethingElse", "value"));

        Assert.Null(resolver.ResolveNamespace("MyConn"));
    }

    [Fact]
    public void ResolveValue_PercentPlaceholder_ResolvesEntityName()
    {
        var resolver = Resolver(("QueueSetting", "real-queue"));

        Assert.Equal("real-queue", resolver.ResolveValue("%QueueSetting%"));
    }

    [Fact]
    public void ResolveValue_PlainValue_ReturnedUnchanged()
    {
        var resolver = Resolver(("QueueSetting", "real-queue"));

        Assert.Equal("plain-queue", resolver.ResolveValue("plain-queue"));
    }

    [Theory]
    [InlineData("myns.servicebus.windows.net", "myns")]
    [InlineData("Endpoint=sb://other.servicebus.windows.net/;SharedAccessKeyName=k", "other")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void ExtractNamespace_ParsesHostsAndConnectionStrings(string? value, string? expected)
    {
        Assert.Equal(expected, ServiceBusConnectionResolver.ExtractNamespace(value));
    }
}
