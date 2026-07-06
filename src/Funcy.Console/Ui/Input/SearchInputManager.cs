using System.Text;
using Spectre.Console;

namespace Funcy.Console.Ui.Input;

public class SearchInputManager
{
    private bool _searchMode;
    private readonly StringBuilder _searchText = new();
    private int _searchIndex;

    public string SearchText => _searchText.ToString();
    public Markup SearchMarkup => GetMarkup();

    public bool HandleInput(ConsoleKeyInfo keyInfo)
    {
        switch (keyInfo.Key)
        {
            case ConsoleKey.Backspace:
                if (_searchText.Length > 1)
                {
                    _searchText.Remove(_searchText.Length - 2, 1);
                    _searchIndex = _searchText.Length - 1;
                }
                break;
            case ConsoleKey.Enter:
                _searchMode = false;
                _searchText.Remove(_searchText.Length - 1, 1);
                break;
            case ConsoleKey.LeftArrow:
                _searchIndex = Math.Max(0, _searchIndex - 1);
                break;
            case ConsoleKey.RightArrow:
                _searchIndex = Math.Min(_searchText.Length - 1, _searchIndex + 1);
                break;
            case ConsoleKey.Delete:
                if (_searchIndex < _searchText.Length - 1)
                {
                    _searchText.Remove(_searchIndex, 1);
                }
                break;
            default:
                var keyToChar = Interpret(keyInfo);
                if (keyToChar != null)
                {
                    _searchText.Insert(_searchIndex, keyToChar);
                    _searchIndex++;
                }
                break;
        }

        return _searchMode;
    }

    private Markup GetMarkup()
    {
        var markupText = _searchText.ToString();

        if (string.IsNullOrEmpty(markupText))
        {
            return new Markup(markupText);
        }

        if (!_searchMode)
        {
            // Escape the raw user input so markup metacharacters (e.g. '[') render literally
            // instead of being parsed as Spectre markup.
            markupText = Markup.Escape(markupText) + " " + UiStyles.CreateDangerText("del");
        }
        else
        {
            markupText = Markup.Escape(markupText[.._searchIndex])
                         + "[underline]"
                         + Markup.Escape(markupText[_searchIndex].ToString())
                         + "[/]"
                         + Markup.Escape(markupText[(_searchIndex + 1)..]);
            markupText += " " + UiStyles.CreateDangerText("↩");
        }

        return new Markup(markupText);
    }

    private char? Interpret(ConsoleKeyInfo keyInfo)
    {
        return !char.IsControl(keyInfo.KeyChar) ? keyInfo.KeyChar : null;
    }

    public void InitializeSearchMode()
    {
        _searchMode = true;
        _searchText.Append(' ');
    }

    public void ClearSearchText()
    {
        _searchText.Clear();
        _searchIndex = 0;
    }
}
