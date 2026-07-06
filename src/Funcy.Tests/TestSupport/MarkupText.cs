using Spectre.Console;
using Spectre.Console.Rendering;

namespace Funcy.Tests.TestSupport;

/// <summary>
/// Renders a Spectre <see cref="IRenderable"/> to plain (styling-stripped) text so tests can
/// assert on the visible content deterministically without a real console.
/// </summary>
public static class MarkupText
{
    public static string Plain(IRenderable renderable)
    {
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(writer)
        });
        console.Profile.Width = 500;
        console.Write(renderable);
        return writer.ToString().Trim('\n', '\r');
    }
}
