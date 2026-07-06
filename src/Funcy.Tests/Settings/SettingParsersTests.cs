using Funcy.Console.Settings;
using Xunit;

namespace Funcy.Tests.Settings;

public class SettingParsersTests
{
    private static FuncySettings Apply(SettingParseResult result)
    {
        Assert.True(result.Success);
        Assert.NotNull(result.Apply);
        var settings = new FuncySettings();
        result.Apply!(settings);
        return settings;
    }

    [Fact]
    public void ParseTagColumns_CommaSeparated_TrimsAndSplits()
    {
        var settings = Apply(SettingParsers.ParseTagColumns(" System , Team "));
        Assert.Equal(["System", "Team"], settings.TagColumns);
    }

    [Fact]
    public void ParseTagColumns_Empty_ClearsList()
    {
        var settings = Apply(SettingParsers.ParseTagColumns("   "));
        Assert.Empty(settings.TagColumns);
    }

    [Fact]
    public void ParseTagColumns_BlankEntry_Fails()
    {
        var result = SettingParsers.ParseTagColumns("System,,Team");
        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void ParseRefreshInterval_Positive_Succeeds()
    {
        var settings = Apply(SettingParsers.ParseRefreshInterval("90"));
        Assert.Equal(90, settings.SubscriptionRefreshIntervalMinutes);
    }

    [Fact]
    public void ParseRefreshInterval_Zero_Succeeds()
    {
        var settings = Apply(SettingParsers.ParseRefreshInterval("0"));
        Assert.Equal(0, settings.SubscriptionRefreshIntervalMinutes);
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("")]
    public void ParseRefreshInterval_Invalid_Fails(string raw)
    {
        Assert.False(SettingParsers.ParseRefreshInterval(raw).Success);
    }

    [Fact]
    public void ParseDefaultWidth_InRange_Succeeds()
    {
        var settings = Apply(SettingParsers.ParseDefaultWidth("30"));
        Assert.Equal(30, settings.DefaultTagColumnWidth);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("101")]
    [InlineData("x")]
    public void ParseDefaultWidth_OutOfRange_Fails(string raw)
    {
        Assert.False(SettingParsers.ParseDefaultWidth(raw).Success);
    }

    [Fact]
    public void ParseTagColumnWidths_KeyValuePairs_Succeeds()
    {
        var settings = Apply(SettingParsers.ParseTagColumnWidths("System=30, Team=15"));
        Assert.Equal(30, settings.TagColumnWidths["System"]);
        Assert.Equal(15, settings.TagColumnWidths["Team"]);
    }

    [Fact]
    public void ParseTagColumnWidths_Empty_ClearsDictionary()
    {
        var settings = Apply(SettingParsers.ParseTagColumnWidths(""));
        Assert.Empty(settings.TagColumnWidths);
    }

    [Theory]
    [InlineData("System")]
    [InlineData("System=200")]
    [InlineData("=30")]
    [InlineData("System=abc")]
    public void ParseTagColumnWidths_Malformed_Fails(string raw)
    {
        Assert.False(SettingParsers.ParseTagColumnWidths(raw).Success);
    }

    [Fact]
    public void Descriptors_FormatOutput_RoundTripsThroughParse()
    {
        var settings = new FuncySettings
        {
            TagColumns = ["System", "Team"],
            SubscriptionRefreshIntervalMinutes = 45,
            DefaultTagColumnWidth = 25,
            TagColumnWidths = new Dictionary<string, int> { ["System"] = 40 }
        };

        foreach (var descriptor in SettingDescriptors.All)
        {
            var formatted = descriptor.Format(settings);
            var result = descriptor.Parse(formatted);
            Assert.True(result.Success, $"{descriptor.Name} did not round-trip: '{formatted}'");

            var roundTripped = new FuncySettings();
            result.Apply!(roundTripped);
            Assert.Equal(formatted, descriptor.Format(roundTripped));
        }
    }
}
