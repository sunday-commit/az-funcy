namespace Funcy.Console.Ui.Controllers;

// Implemented by a checklist controller whose selected row is toggled (checked/unchecked) on
// Enter rather than navigating into a sub-panel.
public interface IToggleSelectionController
{
    void ToggleSelected();
}
