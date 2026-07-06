namespace Funcy.Console.Ui.Controllers;

// Bridges the controller's load state to the panel's empty-state message. The controller
// owns the text (loading / empty / error); the panel reads it through a closure.
public sealed class AppSettingsEmptyState
{
    public string? Message { get; set; }
}
