using System.Text;
using Spectre.Console;

namespace Funcy.Console.Ui.Input;

public enum EditInputResult
{
    Continue,
    Commit,
    Cancel
}

// Inline text editor for a single setting value. Mirrors SearchInputManager's key handling but
// is pre-filled with the current value and reports commit/cancel explicitly. Rendered into the
// TopPanel input cell while MainContainer is in edit mode.
public sealed class SettingEditManager
{
    private readonly StringBuilder _text = new();
    private int _cursor;

    public string Key { get; private set; } = "";
    public string Text => _text.ToString();

    public void Begin(string key, string initialValue)
    {
        Key = key;
        _text.Clear();
        _text.Append(initialValue);
        _cursor = _text.Length;
    }

    public EditInputResult HandleInput(ConsoleKeyInfo keyInfo)
    {
        switch (keyInfo.Key)
        {
            case ConsoleKey.Enter:
                return EditInputResult.Commit;
            case ConsoleKey.Escape:
                return EditInputResult.Cancel;
            case ConsoleKey.Backspace:
                if (_cursor > 0)
                {
                    _text.Remove(_cursor - 1, 1);
                    _cursor--;
                }
                break;
            case ConsoleKey.Delete:
                if (_cursor < _text.Length)
                {
                    _text.Remove(_cursor, 1);
                }
                break;
            case ConsoleKey.LeftArrow:
                _cursor = Math.Max(0, _cursor - 1);
                break;
            case ConsoleKey.RightArrow:
                _cursor = Math.Min(_text.Length, _cursor + 1);
                break;
            case ConsoleKey.Home:
                _cursor = 0;
                break;
            case ConsoleKey.End:
                _cursor = _text.Length;
                break;
            default:
                if (!char.IsControl(keyInfo.KeyChar))
                {
                    _text.Insert(_cursor, keyInfo.KeyChar);
                    _cursor++;
                }
                break;
        }

        return EditInputResult.Continue;
    }

    public Markup GetMarkup(string? error)
    {
        var text = _text.ToString();
        string rendered;
        if (_cursor >= text.Length)
        {
            rendered = text.EscapeMarkup() + "[underline] [/]";
        }
        else
        {
            rendered = text[.._cursor].EscapeMarkup()
                       + "[underline]" + text[_cursor].ToString().EscapeMarkup() + "[/]"
                       + text[(_cursor + 1)..].EscapeMarkup();
        }

        // Prefix a clearly styled label naming the setting so the edit reads as an edit, not the
        // filter (which shares this TopPanel cell). Value + cursor render as before.
        rendered = $"[{UiStyles.Label}]Edit {Key.EscapeMarkup()}:[/] " + rendered;

        rendered += " " + UiStyles.CreateDangerText("↩");
        if (!string.IsNullOrEmpty(error))
        {
            rendered += $" [{UiStyles.Danger}]{error.EscapeMarkup()}[/]";
        }

        return new Markup(rendered);
    }
}
