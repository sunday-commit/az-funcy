namespace Funcy.Console.Ui.Controllers;

// Implemented by controllers that commit an inline text edit (the settings panel's Text-kind
// rows). MainContainer begins the edit via the panel's own activation and commits here.
public interface IEditablePanel
{
    // Applies the edit; returns a user-facing error message, or null on success.
    Task<string?> CommitEditAsync(string key, string rawValue);
}
