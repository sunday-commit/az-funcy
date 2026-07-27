using Funcy.Demo;
using Xunit;

namespace Funcy.Tests.Demo;

public class DemoModeTests
{
    [Fact]
    public void IsRequested_WithExactFlag_IsTrue()
        => Assert.True(DemoMode.IsRequested(["--demo"]));

    [Fact]
    public void IsRequested_WithFlagAmongOtherArguments_IsTrue()
        => Assert.True(DemoMode.IsRequested(["--verbose", "--demo"]));

    [Fact]
    public void IsRequested_WithNoArguments_IsFalse()
        => Assert.False(DemoMode.IsRequested([]));

    // Demo mode shows fabricated data, so only the exact flag may enable it: no near-miss spelling,
    // no casing variant, and nothing that merely contains the word.
    [Theory]
    [InlineData("demo")]
    [InlineData("-demo")]
    [InlineData("--DEMO")]
    [InlineData("--Demo")]
    [InlineData("--demos")]
    [InlineData("--demo=false")]
    [InlineData("--no-demo")]
    public void IsRequested_WithAnythingButTheExactFlag_IsFalse(string argument)
        => Assert.False(DemoMode.IsRequested([argument]));
}
