using System.Reflection;
using Funcy.Infrastructure.Shell;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Funcy.Console.Ui;

/// <summary>
/// Pure, headless-renderable composition for the splash screen. Every state (initializing,
/// success, tool-validation failure, initialization exception) is built here so the visuals can
/// be asserted in tests without a real terminal. Nothing in this class touches the console or the
/// startup state machine - it only turns inputs into <see cref="IRenderable"/> cards.
/// </summary>
public static class SplashScreenLayout
{
    private const string Tagline = "Azure Function Apps, at a glance";
    private const string BrandName = "Funcy";

    // Below this width the Figlet wordmark no longer fits, so fall back to a compact text mark.
    private const int FigletMinWidth = 50;

    private static bool Unicode => AnsiConsole.Profile.Capabilities.Unicode;

    private static string Ok => Unicode ? "✓" : "OK";
    private static string Cross => Unicode ? "✗" : "x";
    private static string Arrow => Unicode ? "→" : "->";

    private static readonly string? Version = ResolveVersion(Assembly.GetEntryAssembly());

    /// <summary>Startup state: centered wordmark with the animated status line below.</summary>
    public static IRenderable Initializing(int consoleWidth, string frame)
        => Compose(consoleWidth, Color.Orange1, centeredBody: true,
            Center($"[orange1]{Spinner(frame)}[/] [grey85]Initializing...[/]"));

    /// <summary>All required tools present - ready to continue.</summary>
    public static IRenderable Success(int consoleWidth)
        => Compose(consoleWidth, Color.Orange1, centeredBody: true,
            Center($"[green]{Ok}[/] [grey85]All required tools are installed[/]"),
            Center("[grey54]Press any key to continue...[/]"));

    /// <summary>Tool validation failed - list the missing tools and how to install them.</summary>
    public static IRenderable Errors(int consoleWidth, ToolValidationResult? result)
    {
        var body = new List<IRenderable> { new Markup("[bold red]Missing required tools:[/]") };

        if (result?.MissingTools is { Count: > 0 })
        {
            foreach (var tool in result.MissingTools)
            {
                body.Add(new Markup($"  [red]{Cross}[/] {Markup.Escape(tool)}"));
            }
        }

        if (result?.InstallInstructions is { Count: > 0 })
        {
            body.Add(new Markup(""));
            body.Add(new Markup("[bold yellow]Installation instructions:[/]"));
            foreach (var instruction in result.InstallInstructions)
            {
                body.Add(new Markup($"  [grey]{Arrow}[/] {Markup.Escape(instruction)}"));
            }
        }

        body.Add(new Markup(""));
        body.Add(new Markup("[bold red]Install the missing tools and restart Funcy.[/]"));
        body.Add(new Markup("[grey54]Press any key to exit...[/]"));

        return Compose(consoleWidth, Color.Red, centeredBody: false, body.ToArray());
    }

    /// <summary>Initialization threw - show the resolved title, detail and suggested actions.</summary>
    public static IRenderable Exception(int consoleWidth, Exception exception)
    {
        var error = InitializationErrorResolver.Resolve(exception);
        var body = new List<IRenderable> { new Markup($"[bold red]{Markup.Escape(error.Title)}[/]") };

        if (error.Detail is not null)
        {
            body.Add(new Markup(""));
            body.Add(new Markup($"[grey85]{Markup.Escape(error.Detail)}[/]"));
        }

        if (error.Actions.Length > 0)
        {
            body.Add(new Markup(""));
            foreach (var action in error.Actions)
            {
                body.Add(new Markup($"  [grey]{Arrow}[/] [white]{Markup.Escape(action)}[/]"));
            }
        }

        body.Add(new Markup(""));
        body.Add(new Markup("[grey54]Press any key to exit...[/]"));

        return Compose(consoleWidth, Color.Red, centeredBody: false, body.ToArray());
    }

    // Shared card: breathing room, wordmark, tagline, version, a divider, then the state body.
    private static IRenderable Compose(int consoleWidth, Color border, bool centeredBody, params IRenderable[] body)
    {
        var figletMode = consoleWidth >= FigletMinWidth;
        var cardWidth = figletMode
            ? Math.Clamp(consoleWidth - 4, 48, 68)
            : Math.Clamp(consoleWidth - 2, 24, 46);

        var grid = new Grid { Expand = true };
        grid.AddColumn(new GridColumn { Padding = new Padding(0, 0, 0, 0) });

        grid.AddEmptyRow();
        grid.AddRow(Wordmark(figletMode));
        grid.AddEmptyRow();
        grid.AddRow(Center($"[grey70]{Tagline}[/]"));
        if (Version is not null)
        {
            grid.AddRow(Center($"[grey42]{Markup.Escape(Version)}[/]"));
        }

        grid.AddRow(new Rule { Style = new Style(Color.Grey35) });

        foreach (var line in body)
        {
            grid.AddRow(centeredBody ? line : new Padder(line, new Padding(1, 0, 0, 0)));
        }

        grid.AddEmptyRow();

        var panel = new Panel(grid)
        {
            Width = cardWidth,
            Border = Unicode ? BoxBorder.Rounded : BoxBorder.Square,
            Padding = new Padding(2, 0, 2, 0),
        };
        panel.BorderStyle(new Style(border));

        return new Align(panel, HorizontalAlignment.Center);
    }

    private static IRenderable Wordmark(bool figletMode)
    {
        if (figletMode)
        {
            return new FigletText(BrandName).Color(Color.Orange1).Centered();
        }

        var mark = Unicode ? $"« {BrandName.ToUpperInvariant()} »" : BrandName.ToUpperInvariant();
        return Center($"[bold orange1]{mark}[/]");
    }

    // The braille spinner frames are only safe on Unicode-capable terminals.
    private static string Spinner(string frame) => Unicode ? frame : "*";

    private static IRenderable Center(string markup) => new Markup(markup).Centered();

    /// <summary>
    /// Resolves the display version from the assembly's informational version (set by MinVer),
    /// stripping any build metadata. Falls back to the assembly version, or null when neither is
    /// meaningful, so the splash simply omits the version rather than crashing.
    /// </summary>
    public static string? ResolveVersion(Assembly? assembly)
    {
        if (assembly is null)
        {
            return null;
        }

        var informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return NormalizeVersion(informational, assembly.GetName().Version);
    }

    /// <summary>
    /// Pure version-string normalization: prefer the informational version (minus build metadata
    /// after '+'), fall back to the assembly version, and return null when neither is meaningful.
    /// Exposed for testing.
    /// </summary>
    public static string? NormalizeVersion(string? informationalVersion, Version? assemblyVersion)
    {
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var plusIndex = informationalVersion.IndexOf('+');
            var version = (plusIndex >= 0 ? informationalVersion[..plusIndex] : informationalVersion).Trim();
            if (version.Length > 0 && version != "0.0.0")
            {
                return version.StartsWith('v') ? version : $"v{version}";
            }
        }

        if (assemblyVersion is not null && assemblyVersion != new Version(0, 0, 0, 0))
        {
            return $"v{assemblyVersion.ToString(3)}";
        }

        return null;
    }
}
