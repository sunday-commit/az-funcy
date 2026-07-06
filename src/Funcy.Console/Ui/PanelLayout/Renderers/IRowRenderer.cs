namespace Funcy.Console.Ui.PanelLayout.Renderers;

public interface ILayoutRenderer<T>
{
    RowMarkup CreateRowMarkup(T item);
    ColumnLayout<T> CreateColumnLayout();

    // Row markup for an item shown only because it bypasses the filter (an active
    // operation, no text match). Defaults to the normal markup; panels that support
    // the bypass override this to add a subtle cue. Never carries search highlighting.
    RowMarkup CreateBypassRowMarkup(T item) => CreateRowMarkup(item);
}