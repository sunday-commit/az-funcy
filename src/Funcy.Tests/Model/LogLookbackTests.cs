using Funcy.Core.Model;
using Xunit;

namespace Funcy.Tests.Model;

public class LogLookbackTests
{
    [Theory]
    [InlineData(LogLookback.OneHour, LogLookback.SixHours)]
    [InlineData(LogLookback.SixHours, LogLookback.TwelveHours)]
    [InlineData(LogLookback.TwelveHours, LogLookback.TwentyFourHours)]
    [InlineData(LogLookback.TwentyFourHours, LogLookback.ThreeDays)]
    [InlineData(LogLookback.ThreeDays, LogLookback.SevenDays)]
    [InlineData(LogLookback.SevenDays, LogLookback.ThirtyDays)]
    [InlineData(LogLookback.ThirtyDays, LogLookback.OneHour)]
    public void Next_CyclesInOrder(LogLookback current, LogLookback expected)
    {
        Assert.Equal(expected, current.Next());
    }

    [Fact]
    public void Next_FullCycleReturnsToStart()
    {
        var lookback = LogLookback.OneHour;
        for (var i = 0; i < 7; i++)
        {
            lookback = lookback.Next();
        }

        Assert.Equal(LogLookback.OneHour, lookback);
    }

    [Theory]
    [InlineData(LogLookback.OneHour, 1)]
    [InlineData(LogLookback.SixHours, 6)]
    [InlineData(LogLookback.TwelveHours, 12)]
    [InlineData(LogLookback.TwentyFourHours, 24)]
    public void ToTimeSpan_Hours(LogLookback lookback, int hours)
    {
        Assert.Equal(TimeSpan.FromHours(hours), lookback.ToTimeSpan());
    }

    [Theory]
    [InlineData(LogLookback.ThreeDays, 3)]
    [InlineData(LogLookback.SevenDays, 7)]
    [InlineData(LogLookback.ThirtyDays, 30)]
    public void ToTimeSpan_Days(LogLookback lookback, int days)
    {
        Assert.Equal(TimeSpan.FromDays(days), lookback.ToTimeSpan());
    }
}
