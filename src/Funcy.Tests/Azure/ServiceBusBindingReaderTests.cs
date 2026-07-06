using System.Text.Json;
using Funcy.Infrastructure.Azure;
using Funcy.Infrastructure.Azure.Models;
using Xunit;

namespace Funcy.Tests.Azure;

public class ServiceBusBindingReaderTests
{
    private static FunctionConfig Parse(string json) =>
        JsonSerializer.Deserialize<FunctionConfig>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    [Fact]
    public void TryRead_QueueTrigger_ReturnsQueueName()
    {
        var config = Parse("""
        {
          "bindings": [
            { "type": "serviceBusTrigger", "direction": "in", "name": "msg",
              "queueName": "orders-queue", "connection": "MyServiceBus" }
          ]
        }
        """);

        var target = ServiceBusBindingReader.TryRead(config);

        Assert.NotNull(target);
        Assert.Equal("orders-queue", target!.QueueName);
        Assert.Null(target.TopicName);
        Assert.Null(target.SubscriptionName);
        Assert.Equal("MyServiceBus", target.ConnectionSetting);
    }

    [Fact]
    public void TryRead_TopicSubscriptionTrigger_ReturnsTopicAndSubscription()
    {
        var config = Parse("""
        {
          "bindings": [
            { "type": "serviceBusTrigger", "direction": "in", "name": "msg",
              "topicName": "orders-topic", "subscriptionName": "orders-sub",
              "connection": "MyServiceBus" }
          ]
        }
        """);

        var target = ServiceBusBindingReader.TryRead(config);

        Assert.NotNull(target);
        Assert.Equal("orders-topic", target!.TopicName);
        Assert.Equal("orders-sub", target.SubscriptionName);
        Assert.Null(target.QueueName);
    }

    [Fact]
    public void TryRead_MissingConnection_ReturnsNullConnection()
    {
        var config = Parse("""
        {
          "bindings": [
            { "type": "serviceBusTrigger", "direction": "in", "queueName": "orders-queue" }
          ]
        }
        """);

        var target = ServiceBusBindingReader.TryRead(config);

        Assert.NotNull(target);
        Assert.Equal("orders-queue", target!.QueueName);
        Assert.Null(target.ConnectionSetting);
    }

    [Fact]
    public void TryRead_PercentIndirection_StoredRaw()
    {
        var config = Parse("""
        {
          "bindings": [
            { "type": "serviceBusTrigger", "direction": "in",
              "queueName": "%QueueSetting%", "connection": "%ConnSetting%" }
          ]
        }
        """);

        var target = ServiceBusBindingReader.TryRead(config);

        Assert.NotNull(target);
        Assert.Equal("%QueueSetting%", target!.QueueName);
        Assert.Equal("%ConnSetting%", target.ConnectionSetting);
    }

    [Fact]
    public void TryRead_NonServiceBusTrigger_ReturnsNull()
    {
        var config = Parse("""
        {
          "bindings": [
            { "type": "httpTrigger", "direction": "in", "authLevel": "function" }
          ]
        }
        """);

        Assert.Null(ServiceBusBindingReader.TryRead(config));
    }
}
