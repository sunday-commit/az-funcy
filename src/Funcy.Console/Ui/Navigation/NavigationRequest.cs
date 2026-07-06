using Funcy.Console.Ui.Panels;

namespace Funcy.Console.Ui.Navigation;

// Key resolves the owning function app (via the coordinator cache); SecondaryKey carries an
// optional child identifier, e.g. the function name when opening its log panel.
public sealed record NavigationRequest(PanelTarget Target, string Key, string? SecondaryKey = null);