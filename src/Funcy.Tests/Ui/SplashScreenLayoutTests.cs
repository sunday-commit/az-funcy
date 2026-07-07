using System;
using Funcy.Console.Ui;
using Funcy.Infrastructure.Shell;
using Funcy.Tests.TestSupport;
using Spectre.Console;
using Spectre.Console.Rendering;
using Xunit;

namespace Funcy.Tests.Ui;

public class SplashScreenLayoutTests
{
    private const string Tagline = "Azure Function Apps, at a glance";

    // Renders to plain (styling-stripped) text at a fixed console width, preserving leading
    // whitespace so alignment can be asserted.
    private static string RenderAt(IRenderable renderable, int width)
    {
        var writer = new System.IO.StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(writer),
        });
        console.Profile.Width = width;
        console.Write(renderable);
        return writer.ToString();
    }

    [Fact]
    public void Initializing_ShowsTaglineAndStatus()
    {
        var text = MarkupText.Plain(SplashScreenLayout.Initializing(120, "*"));

        Assert.Contains(Tagline, text);
        Assert.Contains("Initializing...", text);
    }

    [Fact]
    public void Success_ShowsReadyMessageAndTagline()
    {
        var text = MarkupText.Plain(SplashScreenLayout.Success(120));

        Assert.Contains(Tagline, text);
        Assert.Contains("All required tools are installed", text);
        Assert.Contains("Press any key to continue", text);
    }

    [Fact]
    public void Errors_ListsMissingToolsAndInstructions()
    {
        var result = new ToolValidationResult(
            IsValid: false,
            MissingTools: new() { "az", "func" },
            InstallInstructions: new() { "brew install azure-cli", "npm i -g azure-functions-core-tools" });

        var text = MarkupText.Plain(SplashScreenLayout.Errors(120, result));

        Assert.Contains("Missing required tools", text);
        Assert.Contains("az", text);
        Assert.Contains("func", text);
        Assert.Contains("brew install azure-cli", text);
        Assert.Contains("Press any key to exit", text);
    }

    [Fact]
    public void Exception_ShowsResolvedTitleForNotLoggedIn()
    {
        var text = MarkupText.Plain(SplashScreenLayout.Exception(120, new InvalidOperationException("Please run 'az login'")));

        Assert.Contains("You are not logged in to Azure", text);
        Assert.Contains("az login", text);
        Assert.Contains("Press any key to exit", text);
    }

    [Fact]
    public void Exception_ShowsGenericTitleAndDetail()
    {
        var text = MarkupText.Plain(SplashScreenLayout.Exception(120, new InvalidOperationException("boom happened")));

        Assert.Contains("Initialization failed", text);
        Assert.Contains("boom happened", text);
    }

    [Fact]
    public void Tagline_IsHorizontallyCentered()
    {
        var lines = RenderAt(SplashScreenLayout.Initializing(120, "*"), 120).Split('\n');
        var taglineLine = Array.Find(lines, l => l.Contains(Tagline));

        Assert.NotNull(taglineLine);

        var leading = taglineLine!.Length - taglineLine.TrimStart().Length;
        var trailing = taglineLine.Length - taglineLine.TrimEnd().Length;

        // Centered content has whitespace on the left, roughly balanced with the right.
        Assert.True(leading > 0, $"expected leading whitespace, got {leading}");
        Assert.True(Math.Abs(leading - trailing) <= 2, $"expected balanced margins, leading={leading} trailing={trailing}");
    }

    [Theory]
    [InlineData(40)]
    [InlineData(80)]
    [InlineData(120)]
    [InlineData(160)]
    public void AllStates_RenderWithoutThrowing_AcrossWidths(int width)
    {
        var result = new ToolValidationResult(false, new() { "az" }, new() { "install az" });

        // Each of the four terminal states must render at every width without throwing.
        _ = RenderAt(SplashScreenLayout.Initializing(width, "*"), width);
        _ = RenderAt(SplashScreenLayout.Success(width), width);
        _ = RenderAt(SplashScreenLayout.Errors(width, result), width);
        _ = RenderAt(SplashScreenLayout.Exception(width, new InvalidOperationException("kaboom")), width);
    }

    [Fact]
    public void NarrowWidth_UsesCompactWordmark()
    {
        var text = MarkupText.Plain(SplashScreenLayout.Initializing(40, "*"));

        // Below the Figlet threshold the wordmark falls back to a literal text mark.
        Assert.Contains("FUNCY", text);
    }

    [Theory]
    [InlineData("1.4.2", null, "v1.4.2")]
    [InlineData("1.4.2+abc123", null, "v1.4.2")]
    [InlineData("v2.0.0", null, "v2.0.0")]
    [InlineData("  1.0.0  ", null, "v1.0.0")]
    [InlineData("0.0.0", null, null)]
    [InlineData(null, null, null)]
    [InlineData("", null, null)]
    public void NormalizeVersion_HandlesInformationalVersion(string? informational, Version? assemblyVersion, string? expected)
    {
        Assert.Equal(expected, SplashScreenLayout.NormalizeVersion(informational, assemblyVersion));
    }

    [Fact]
    public void NormalizeVersion_FallsBackToAssemblyVersion()
    {
        Assert.Equal("v3.2.1", SplashScreenLayout.NormalizeVersion(null, new Version(3, 2, 1, 0)));
    }

    [Fact]
    public void ResolveVersion_NullAssembly_ReturnsNull()
    {
        Assert.Null(SplashScreenLayout.ResolveVersion(null));
    }
}
