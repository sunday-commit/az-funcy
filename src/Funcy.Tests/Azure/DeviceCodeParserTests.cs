using Funcy.Infrastructure.Azure;
using Xunit;

namespace Funcy.Tests.Azure;

public class DeviceCodeParserTests
{
    [Fact]
    public void TryParse_StandardWording_ExtractsUrlAndCode()
    {
        const string line =
            "To sign in, use a web browser to open the page https://microsoft.com/devicelogin " +
            "and enter the code ABCD1234 to authenticate.";

        Assert.True(DeviceCodeParser.TryParse(line, out var url, out var code));
        Assert.Equal("https://microsoft.com/devicelogin", url);
        Assert.Equal("ABCD1234", code);
    }

    [Fact]
    public void TryParse_HyphenatedCode_ExtractsCode()
    {
        const string line =
            "open the page https://aka.ms/devicelogin and enter the code A1B2-C3D4 to authenticate.";

        Assert.True(DeviceCodeParser.TryParse(line, out var url, out var code));
        Assert.Equal("https://aka.ms/devicelogin", url);
        Assert.Equal("A1B2-C3D4", code);
    }

    [Fact]
    public void TryParse_AlternateWordingBetweenUrlAndCode_StillMatches()
    {
        const string line =
            "Open https://microsoft.com/devicelogin in your browser, then enter the code XYZ98765 now.";

        Assert.True(DeviceCodeParser.TryParse(line, out var url, out var code));
        Assert.Equal("https://microsoft.com/devicelogin", url);
        Assert.Equal("XYZ98765", code);
    }

    [Fact]
    public void TryParse_UnrelatedLine_ReturnsFalse()
    {
        Assert.False(DeviceCodeParser.TryParse("Requesting a token...", out _, out _));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void TryParse_NullOrEmpty_ReturnsFalse(string? line)
    {
        Assert.False(DeviceCodeParser.TryParse(line, out _, out _));
    }
}
