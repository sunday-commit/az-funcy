using Funcy.Console.Ui.ConsoleHelper;
using Xunit;

namespace Funcy.Tests.ConsoleHelper;

public class ConsoleKeyHelperTests
{
    [Theory]
    [InlineData(ConsoleKey.D0, 0)]
    [InlineData(ConsoleKey.D5, 5)]
    [InlineData(ConsoleKey.D9, 9)]
    public void TryGetDigit_TopRowDigits(ConsoleKey key, int expected)
        => Assert.Equal(expected, ConsoleKeyHelper.TryGetDigit(key));

    [Theory]
    [InlineData(ConsoleKey.NumPad0, 0)]
    [InlineData(ConsoleKey.NumPad7, 7)]
    [InlineData(ConsoleKey.NumPad9, 9)]
    public void TryGetDigit_NumpadDigits(ConsoleKey key, int expected)
        => Assert.Equal(expected, ConsoleKeyHelper.TryGetDigit(key));

    [Theory]
    [InlineData(ConsoleKey.A)]
    [InlineData(ConsoleKey.Enter)]
    [InlineData(ConsoleKey.F1)]
    [InlineData(ConsoleKey.Spacebar)]
    public void TryGetDigit_NonDigits_ReturnNull(ConsoleKey key)
        => Assert.Null(ConsoleKeyHelper.TryGetDigit(key));
}
