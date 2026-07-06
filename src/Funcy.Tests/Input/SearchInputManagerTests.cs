using Funcy.Console.Ui.Input;
using Funcy.Tests.TestSupport;
using Xunit;

namespace Funcy.Tests.Input;

// Characterization: the search box keeps a trailing space as a cursor placeholder while in search
// mode; HandleInput returns the *search mode* flag (true = still typing, false = committed/idle).
public class SearchInputManagerTests
{
    private static ConsoleKeyInfo Char(char c) => new(c, (ConsoleKey)char.ToUpper(c), false, false, false);
    private static ConsoleKeyInfo Key(ConsoleKey key, char c = '\0') => new(c, key, false, false, false);

    private static SearchInputManager Typed(string text)
    {
        var mgr = new SearchInputManager();
        mgr.InitializeSearchMode();
        foreach (var c in text) mgr.HandleInput(Char(c));
        return mgr;
    }

    [Fact]
    public void FreshManager_HasEmptySearchText()
        => Assert.Equal("", new SearchInputManager().SearchText);

    [Fact]
    public void InitializeSearchMode_AppendsTrailingSpacePlaceholder()
    {
        var mgr = new SearchInputManager();
        mgr.InitializeSearchMode();
        Assert.Equal(" ", mgr.SearchText);
    }

    [Fact]
    public void Typing_InsertsBeforeTrailingSpace()
    {
        var mgr = Typed("ab");
        Assert.Equal("ab ", mgr.SearchText); // trailing placeholder remains
    }

    [Fact]
    public void Typing_ReturnsTrue_StaysInSearchMode()
    {
        var mgr = new SearchInputManager();
        mgr.InitializeSearchMode();
        Assert.True(mgr.HandleInput(Char('a')));
    }

    [Fact]
    public void Backspace_RemovesCharBeforePlaceholder()
    {
        var mgr = Typed("ab");
        mgr.HandleInput(Key(ConsoleKey.Backspace, '\b'));
        Assert.Equal("a ", mgr.SearchText);
    }

    [Fact]
    public void Backspace_NeverRemovesTheTrailingPlaceholder()
    {
        var mgr = Typed("a");           // "a "
        mgr.HandleInput(Key(ConsoleKey.Backspace, '\b')); // " "
        mgr.HandleInput(Key(ConsoleKey.Backspace, '\b')); // no-op, length == 1
        Assert.Equal(" ", mgr.SearchText);
    }

    [Fact]
    public void Enter_CommitsSearch_StripsPlaceholder_AndReturnsFalse()
    {
        var mgr = Typed("ab");
        var stillSearching = mgr.HandleInput(Key(ConsoleKey.Enter, '\r'));
        Assert.False(stillSearching);
        Assert.Equal("ab", mgr.SearchText); // placeholder removed on commit
    }

    [Fact]
    public void Escape_IsIgnored_ByTheManager()
    {
        // Characterization: there is no Escape case; the control char is not inserted and the
        // manager remains in search mode (returns true).
        var mgr = Typed("ab");
        var stillSearching = mgr.HandleInput(Key(ConsoleKey.Escape, '\u001b'));
        Assert.True(stillSearching);
        Assert.Equal("ab ", mgr.SearchText);
    }

    [Fact]
    public void Delete_RemovesCharAtCursor()
    {
        var mgr = Typed("abc"); // "abc ", cursor at index 3 (the placeholder)
        mgr.HandleInput(Key(ConsoleKey.LeftArrow));
        mgr.HandleInput(Key(ConsoleKey.LeftArrow));
        mgr.HandleInput(Key(ConsoleKey.LeftArrow)); // cursor at index 0
        mgr.HandleInput(Key(ConsoleKey.Delete));
        Assert.Equal("bc ", mgr.SearchText);
    }

    [Fact]
    public void Delete_AtPlaceholder_IsNoOp()
    {
        var mgr = Typed("abc"); // cursor at trailing placeholder (index 3, length 4)
        mgr.HandleInput(Key(ConsoleKey.Delete));
        Assert.Equal("abc ", mgr.SearchText);
    }

    [Fact]
    public void LeftArrow_ClampsAtZero()
    {
        var mgr = Typed("ab");
        for (var i = 0; i < 5; i++) mgr.HandleInput(Key(ConsoleKey.LeftArrow));
        // Cursor at 0: Delete removes first char.
        mgr.HandleInput(Key(ConsoleKey.Delete));
        Assert.Equal("b ", mgr.SearchText);
    }

    [Fact]
    public void ClearSearchText_EmptiesBuffer()
    {
        var mgr = Typed("abc");
        mgr.ClearSearchText();
        Assert.Equal("", mgr.SearchText);
    }

    // ---- SearchMarkup rendering ----

    [Fact]
    public void SearchMarkup_Empty_WhenNoText()
        => Assert.Equal("", MarkupText.Plain(new SearchInputManager().SearchMarkup));

    [Fact]
    public void SearchMarkup_InSearchMode_UnderlinesCursorAndShowsReturnHint()
    {
        var mgr = Typed("ab"); // "ab ", cursor on trailing placeholder
        Assert.Equal("ab  ↩", MarkupText.Plain(mgr.SearchMarkup));
    }

    [Fact]
    public void SearchMarkup_AfterCommit_ShowsDelHint()
    {
        var mgr = Typed("ab");
        mgr.HandleInput(Key(ConsoleKey.Enter, '\r'));
        Assert.Equal("ab del", MarkupText.Plain(mgr.SearchMarkup));
    }

    [Fact]
    public void SearchMarkup_WithUnescapedBracket_Throws()
    {
        // Characterization: user input is not escaped, so a '[' produces invalid Spectre markup
        // and building SearchMarkup throws.
        var mgr = Typed("a[");
        Assert.Throws<InvalidOperationException>(() => _ = mgr.SearchMarkup);
    }
}
