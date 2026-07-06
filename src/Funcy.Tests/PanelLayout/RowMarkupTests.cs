using Funcy.Console.Ui.PanelLayout;
using Funcy.Tests.TestSupport;
using Spectre.Console;
using Xunit;

namespace Funcy.Tests.PanelLayout;

public class RowMarkupTests
{
    [Fact]
    public void Add_ReturnsSameInstance_ForChaining()
    {
        var row = new RowMarkup { Key = "k" };
        var returned = row.Add("Name", new RowCell(new Markup("a"), new Markup("b")));
        Assert.Same(row, returned);
    }

    [Fact]
    public void GetCell_ReturnsSelectedOrUnselected()
    {
        var row = new RowMarkup { Key = "k" }
            .Add("Name", new RowCell(new Markup("SEL"), new Markup("UNSEL")));
        Assert.Equal("SEL", MarkupText.Plain(row.GetCell("Name", true)));
        Assert.Equal("UNSEL", MarkupText.Plain(row.GetCell("Name", false)));
    }

    [Fact]
    public void GetCell_MissingColumn_ReturnsSingleSpace()
    {
        var row = new RowMarkup { Key = "k" };
        Assert.Equal(" ", MarkupText.Plain(row.GetCell("Nope", true))); // fallback cell is a single space
    }

    [Fact]
    public void Add_SameColumnTwice_Overwrites()
    {
        var row = new RowMarkup { Key = "k" }
            .Add("Name", new RowCell(new Markup("first"), new Markup("first")))
            .Add("Name", new RowCell(new Markup("second"), new Markup("second")));
        Assert.Single(row.Cells);
        Assert.Equal("second", MarkupText.Plain(row.GetCell("Name", true)));
    }

    [Fact]
    public void Cells_KeyComparison_IsOrdinalCaseSensitive()
    {
        var row = new RowMarkup { Key = "k" }
            .Add("Name", new RowCell(new Markup("lower"), new Markup("lower")))
            .Add("NAME", new RowCell(new Markup("upper"), new Markup("upper")));
        Assert.Equal(2, row.Cells.Count); // "Name" and "NAME" are distinct keys
    }

    [Fact]
    public void RowCell_Get_SelectsByFlag()
    {
        var cell = new RowCell(new Markup("s"), new Markup("u"));
        Assert.Equal("s", MarkupText.Plain(cell.Get(true)));
        Assert.Equal("u", MarkupText.Plain(cell.Get(false)));
    }
}
