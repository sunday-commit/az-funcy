using Funcy.Core.Model;
using Funcy.Data.Entities;

namespace Funcy.Infrastructure.Mappers;

public static class FunctionDetailsMapper
{
    public static FunctionDetails Map(this Function function)
    {
        return new FunctionDetails
        {
            FunctionAppName = function.FunctionApp?.Name ?? string.Empty,
            Name = function.Name,
            Trigger = function.Trigger,
            QueueName = function.QueueName,
            TopicName = function.TopicName,
            SubscriptionName = function.SubscriptionName,
            ConnectionSetting = function.ConnectionSetting,
            IsDisabled = function.IsDisabled
        };
    }
}