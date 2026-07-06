namespace Funcy.Console.Ui.Controllers;

// Implemented by controllers whose selected row can be edited inline (the settings panel).
// MainContainer routes Enter to TryBeginEdit and commits through CommitEditAsync.
public interface IEditablePanel
{
    // True when the selected row is editable; yields its key and current raw value for pre-fill.
    bool TryBeginEdit(out string key, out string currentRawValue);

    // Applies the edit; returns a user-facing error message, or null on success.
    Task<string?> CommitEditAsync(string key, string rawValue);
}
