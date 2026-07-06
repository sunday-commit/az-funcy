using Funcy.Console.Ui;
using Funcy.Console.Ui.PanelLayout.Renderers;
using Funcy.Core.Model;
using Spectre.Console;
using Spectre.Console.Rendering;
using Xunit;

namespace Funcy.Tests.Panels;

public class FunctionAppLayoutRendererTests
{
    private readonly FunctionAppLayoutRenderer _sut = new([], _ => 10);

    [Fact]
    public void CreateBypassRowMarkup_AddsDimGlyphCueToName()
    {
        var app = MakeApp("payment-app");

        var normal = RenderPlain(_sut.CreateRowMarkup(app).GetCell("Name", isSelected: false));
        var bypass = RenderPlain(_sut.CreateBypassRowMarkup(app).GetCell("Name", isSelected: false));

        Assert.DoesNotContain(UiStyles.BypassGlyph, normal);
        Assert.Contains(UiStyles.BypassGlyph, bypass);
        Assert.Contains("payment-app", bypass);
    }

    [Fact]
    public void CreateBypassRowMarkup_KeepsNonNameCellsUnchanged()
    {
        var app = MakeApp("payment-app");

        var normalState = RenderPlain(_sut.CreateRowMarkup(app).GetCell("State", isSelected: false));
        var bypassState = RenderPlain(_sut.CreateBypassRowMarkup(app).GetCell("State", isSelected: false));

        Assert.Equal(normalState, bypassState);
    }

    private static FunctionAppDetails MakeApp(string name) => new()
    {
        Name = name,
        State = FunctionState.Running,
        ResourceGroup = "rg-test",
        Subscription = "sub-test",
        Id = "id-test"
    };

    // Renders a cell with styling stripped so only the literal text (incl. the cue glyph) remains.
    private static string RenderPlain(IRenderable renderable)
    {
        var writer = new StringWriter();
        var console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No,
            ColorSystem = ColorSystemSupport.NoColors,
            Out = new AnsiConsoleOutput(writer)
        });
        console.Profile.Width = 200; // headless console defaults to zero width and renders nothing
        console.Write(renderable);
        return writer.ToString();
    }
}
