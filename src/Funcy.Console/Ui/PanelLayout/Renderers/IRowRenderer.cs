namespace Funcy.Console.Ui.PanelLayout.Renderers;

public interface ILayoutRenderer<T>
{
    RowMarkup CreateRowMarkup(T item);
    ColumnLayout<T> CreateColumnLayout();

    // Called on the render thread whenever the resolved (flexed) column widths change so that
    // renderers which truncate cell text in markup do so at the width actually on screen.
    // Keyed by column header. No-op for renderers that let Spectre handle overflow.
    void SetResolvedWidths(IReadOnlyDictionary<string, int> resolvedWidths) { }

    // Row markup for an item shown only because it bypasses the filter (an active
    // operation, no text match). Defaults to the normal markup; panels that support
    // the bypass override this to add a subtle cue. Never carries search highlighting.
    RowMarkup CreateBypassRowMarkup(T item) => CreateRowMarkup(item);
}