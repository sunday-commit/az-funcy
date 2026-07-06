namespace Funcy.Console.Ui.Pagination;

public class ListPanelPaginator
{
    public int SelectedIndex { get; private set; }
    public int VisibleStartIndex{ get; private set; }
    private int _amountOfRows;
    public int MaxVisibleRows { get; private set; }

    // Window height source. Defaults to the real console; injectable so the paginator/view can
    // be exercised in headless tests (System.Console.WindowHeight throws without a console).
    private readonly Func<int> _windowHeight;

    public ListPanelPaginator(Func<int>? windowHeight = null)
    {
        _windowHeight = windowHeight ?? (() => System.Console.WindowHeight);

        // Ensure a valid window height before the first render. Previously SetItems primed
        // this via UpdateTotalRows; the targeted SetAll/Upsert path no longer does, so a
        // one-shot snapshot (e.g. the subscriptions panel) would otherwise Take(0) and show nothing.
        UpdateMaxVisibleRows();
    }

    public void UpdateTotalRows(int amountOfRows)
    {
        _amountOfRows = amountOfRows;
        UpdateMaxVisibleRows();
        
        if (SelectedIndex + VisibleStartIndex >= _amountOfRows)
        {
            SelectedIndex = Math.Min(SelectedIndex, _amountOfRows - VisibleStartIndex - 1);
            if (SelectedIndex < 0)
            {
                SelectedIndex = 0;
                VisibleStartIndex = 0;
            }
        }
    }

    public bool MoveUp()
    {
        var isVisibleStartIndexChanged = false;
        
        SelectedIndex--;
                        
        if (SelectedIndex < 0 && VisibleStartIndex > 0)
        {
            isVisibleStartIndexChanged = true;
            VisibleStartIndex--;
            SelectedIndex = 0;
        }

        if (SelectedIndex < 0)
        {
            SelectedIndex = 0;
        }

        return isVisibleStartIndexChanged;
    }
    
    public bool PageUp()
    {
        var isVisibleStartIndexChanged = false;
        
        if (VisibleStartIndex > 0)
        {
            VisibleStartIndex = Math.Max(0, VisibleStartIndex - MaxVisibleRows);
            isVisibleStartIndexChanged = true;
        }
        else
        {
            SelectedIndex = 0;
        }

        return isVisibleStartIndexChanged;
    }

    public bool MoveDown()
    {
        var isVisibleStartIndexChanged = false;
        SelectedIndex++;

        if (SelectedIndex >= MaxVisibleRows && SelectedIndex + VisibleStartIndex < _amountOfRows)
        {
            isVisibleStartIndexChanged = true;
            VisibleStartIndex++;
            SelectedIndex = MaxVisibleRows - 1;
        }

        if (SelectedIndex >= MaxVisibleRows)
        {
            SelectedIndex = MaxVisibleRows - 1;
        }
        
        if (SelectedIndex >= _amountOfRows)
        {
            SelectedIndex = _amountOfRows - 1;
        }

        return isVisibleStartIndexChanged;
    }
    
    public bool PageDown()
    {
        var isVisibleStartIndexChanged = false;
        
        if (VisibleStartIndex + MaxVisibleRows >= _amountOfRows)
        {
            SelectedIndex = MaxVisibleRows - 1;
        }
        else
        {
            VisibleStartIndex = Math.Min(_amountOfRows - MaxVisibleRows, VisibleStartIndex + MaxVisibleRows);
            isVisibleStartIndexChanged = true;
        }

        return isVisibleStartIndexChanged;
    }
    
    public void UpdateMaxVisibleRows()
    {
        // Floor at 0: a tiny terminal (windowHeight < 8) would otherwise make MaxVisibleRows
        // negative, which silently empties the view (Take(-n)) and corrupts skip/clamp math.
        MaxVisibleRows = Math.Max(0, _windowHeight() - 8);
    }
}