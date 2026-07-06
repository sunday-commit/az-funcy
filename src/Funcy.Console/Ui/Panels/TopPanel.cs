using Funcy.Console.Ui.PanelLayout;
using Funcy.Console.Ui.Shortcuts;
using Spectre.Console;
using Funcy.Core.Model;


namespace Funcy.Console.Ui.Panels;

public class TopPanel
{
    // Index of the subscription-name column and the minimum it may shrink to (the historic width).
    private const int NameColumnIndex = 1;
    private const int MinNameWidth = 35;
    // Combined width of every fixed column (label + the shortcut columns); the name column takes
    // whatever the target table width leaves after these.
    private const int FixedColumnsWidth = 15 + 15 + 19 + 20 + 18 + 15;

    private string _subscriptionName;
    private readonly Table _dataTable = new();
    private readonly Table _statusTable = new();
    private readonly Dictionary<TableIndex, ShortcutMap> _renderedShortcuts = new();
    private readonly Func<int> _windowWidth;
    private int _nameWidth = MinNameWidth;
    public Panel Panel { get; }

    public TopPanel(AppContext appContext, Func<int>? windowWidth = null)
    {
        _windowWidth = windowWidth ?? (() => System.Console.WindowWidth);
        _subscriptionName = appContext.CurrentSubscription.Name;
        InitDataTable();
        InitStatusTable();

        var layoutTable = new Table();
        layoutTable.Border(TableBorder.None);
        layoutTable.ShowHeaders = false;
        layoutTable.AddColumn("");
        layoutTable.AddRow(_dataTable);
        layoutTable.AddRow(_statusTable);


        Panel = new Panel(layoutTable)
        {
            Width = AdaptiveLayout.PanelWidth(AdaptiveLayout.MinTableWidth)
        };
        Panel.BorderColor(Color.Orange1);

        // Size to the terminal before the first render so the subscription name is not squeezed.
        ApplyAdaptiveWidth();

        appContext.OnSubscriptionChange += OnSubscriptionChanged;
    }

    // Grows the subscription-name column into the width the terminal actually offers, keeping the
    // shortcut grid fixed. The panel total tracks the list panel so the two stay aligned; the
    // shortcut columns keep it from ever narrowing below its natural minimum. Render-thread only.
    public void HandleResize() => ApplyAdaptiveWidth();

    // Subscription-name column width for a given console width: the adaptive table width minus the
    // fixed shortcut/label columns, never below the historic minimum. Pure so it can be unit-tested
    // without a live AppContext.
    public static int ResolveNameWidth(int consoleWidth)
        => Math.Max(MinNameWidth, AdaptiveLayout.ResolveTableWidth(consoleWidth) - FixedColumnsWidth);

    private void ApplyAdaptiveWidth()
    {
        var consoleWidth = _windowWidth();
        var nameWidth = ResolveNameWidth(consoleWidth);
        if (nameWidth != _nameWidth)
        {
            _nameWidth = nameWidth;
            _dataTable.Columns[NameColumnIndex].Width(nameWidth);
        }

        // The name column may floor above the target, so the panel follows the wider of the two.
        var target = AdaptiveLayout.ResolveTableWidth(consoleWidth);
        Panel.Width = AdaptiveLayout.PanelWidth(Math.Max(target, FixedColumnsWidth + nameWidth));
    }

    private void OnSubscriptionChanged(SubscriptionDetails obj)
    {
        _subscriptionName = obj.Name;
        _dataTable.Rows.Update(0, 1, new Markup($"{_subscriptionName}"));
    }

    private void InitStatusTable()
    {
        _statusTable.Border(TableBorder.None);
        _statusTable.ShowHeaders = false;
        _statusTable.AddColumn("");
        _statusTable.AddColumn("");
        _statusTable.AddRow(UiStyles.CreateLabelMarkup("Status: "));
    }

    private void InitDataTable()
    {
        _dataTable.Border(TableBorder.None);
        _dataTable.ShowHeaders = false;
        
        _dataTable.AddColumn("", column => column.Width = 15);
        _dataTable.AddColumn("", column =>
        {
            column.Width = 35;
            column.LeftAligned();
        });
        _dataTable.AddColumn("", column =>
        {
            column.Width = 15;
            column.LeftAligned();
        });
        _dataTable.AddColumn("", column =>
        {
            // Wide enough for the longest shortcut label rendered here, "<D> Disable/Enable".
            column.Width = 19;
            column.LeftAligned();
        });
        _dataTable.AddColumn("", column =>
        {
            column.Width = 20;
            column.LeftAligned();
        });
        _dataTable.AddColumn("", column =>
        {
            column.Width = 18;
            column.LeftAligned();
        });
        _dataTable.AddColumn("", column =>
        {
            column.Width = 15;
            column.LeftAligned();
        });

        _renderedShortcuts.Add(new TableIndex(0, 2), new ShortcutMap(ListPanelShortcuts.Filter, true));
        _renderedShortcuts.Add(new TableIndex(0, 3), new ShortcutMap(ListPanelShortcuts.Swap, true));
        _renderedShortcuts.Add(new TableIndex(0, 4), new ShortcutMap(ListPanelShortcuts.Refresh, true));
        _renderedShortcuts.Add(new TableIndex(0, 5), new ShortcutMap(ListPanelShortcuts.RefreshAll, true));
        _renderedShortcuts.Add(new TableIndex(1, 2), new ShortcutMap(ListPanelShortcuts.Start, true));
        _renderedShortcuts.Add(new TableIndex(1, 3), new ShortcutMap(ListPanelShortcuts.Stop, true));
        _renderedShortcuts.Add(new TableIndex(1, 4), new ShortcutMap(ListPanelShortcuts.ChangeSubscription, true));
        _renderedShortcuts.Add(new TableIndex(1, 5), new ShortcutMap(ListPanelShortcuts.Options, true));
        _renderedShortcuts.Add(new TableIndex(0, 6), new ShortcutMap(ListPanelShortcuts.Pin, true));
        _renderedShortcuts.Add(new TableIndex(1, 6), new ShortcutMap(ListPanelShortcuts.View, true));

        _dataTable.AddRow(UiStyles.CreateLabelMarkup("Subscription:"), new Markup($"{_subscriptionName}"), new Markup(""), new Markup(""), new Markup(""), new Markup(""), new Markup(""));
        _dataTable.AddRow(UiStyles.CreateLabelMarkup("Filter:"), new Markup(""), new Markup(""), new Markup(""), new Markup(""), new Markup(""), new Markup(""));

        UpdateShortcuts();
    }

    private void UpdateShortcuts()
    {
        foreach (var shortcut in _renderedShortcuts)
        {
            _dataTable.Rows.Update(shortcut.Key.Row, shortcut.Key.Column,
                UiStyles.CreateShortcutMarkup(shortcut.Value.Shortcut.DisplayChar, shortcut.Value.Shortcut.Label,
                    shortcut.Value.IsEnabled));
        }
    }
    
    public void SetSearchText(Markup searchMarkup)
    {
        UpdateSearchCell(searchMarkup);
    }

    public void SetUiStatusText(Markup markup)
    {
        _statusTable.Rows.Update(0, 1, markup);
    }
    
    private void UpdateSearchCell(Markup searchText)
    {
        _dataTable.Rows.Update(1, 1, searchText);
    }

    public void UpdateShortcuts(Dictionary<TableIndex, ShortcutMap> next)
    {
        foreach (var shortcut in next)
        {
            if (!_renderedShortcuts.TryGetValue(shortcut.Key, out var oldMap) || oldMap != shortcut.Value)
            {
                var markup = UiStyles.CreateShortcutMarkup(shortcut.Value.Shortcut.DisplayChar,
                    shortcut.Value.Shortcut.Label, shortcut.Value.IsEnabled);
                _dataTable.Rows.Update(shortcut.Key.Row, shortcut.Key.Column, markup);
                
                _renderedShortcuts[shortcut.Key] = shortcut.Value;
            }
            
            _dataTable.Rows.Update(shortcut.Key.Row, shortcut.Key.Column,
                UiStyles.CreateShortcutMarkup(shortcut.Value.Shortcut.DisplayChar, shortcut.Value.Shortcut.Label,
                    shortcut.Value.IsEnabled));
        }
        
        foreach (var removedKey in _renderedShortcuts.Keys.Except(next.Keys))
        {
            _dataTable.Rows.Update(removedKey.Row, removedKey.Column, new Markup(""));
            _renderedShortcuts.Remove(removedKey);
        }
    }
}