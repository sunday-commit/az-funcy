using Funcy.Console.Handlers;
using Funcy.Infrastructure.Shell;
using Spectre.Console;

namespace Funcy.Console.Ui;

public class SplashScreen
{
    private readonly ToolValidationService _toolValidationService;
    private readonly AnimationHandler _animationHandler;
    private readonly Table _contentTable = new();
    public Panel Panel { get; private set; } = null!;
    private const string SplashAnimationKey = "SplashScreen";

    public SplashScreen(ToolValidationService toolValidationService, AnimationHandler animationHandler)
    {
        _toolValidationService = toolValidationService;
        _animationHandler = animationHandler;
        InitializePanel();
    }

    private void InitializePanel()
    {
        _contentTable.Border(TableBorder.None);
        _contentTable.ShowHeaders = false;
        _contentTable.AddColumn("", column => column.Width(133));
        
        var figlet = new FigletText("Funcy")
            .Color(Color.Orange1);
        
        _contentTable.AddRow(figlet);
        _contentTable.AddRow(new Markup(""));
        _contentTable.AddRow(new Markup("[orange1]●[/] Initializing..."));
        
        Panel = new Panel(_contentTable)
        {
            Width = 139
        };
        Panel.BorderColor(Color.Orange1);
    }

    public async Task<bool> ShowAsync(Task[] backgroundTasks, Func<Task>? continuationTask = null)
    {
        AnsiConsole.Clear();
        AnsiConsole.Cursor.Hide();

        ToolValidationResult? validationResult = null;
        Exception? initException = null;

        // Starta animation
        _animationHandler.AddAppDetails(SplashAnimationKey);

        await AnsiConsole.Live(Panel)
            .StartAsync(async ctx =>
            {
                ctx.Refresh();

                var validationTask = _toolValidationService.ValidateRequiredToolsAsync();
                var allTasks = Task.WhenAll(backgroundTasks.Append(validationTask));

                // Animera medan tasks körs
                while (!allTasks.IsCompleted)
                {
                    await Task.WhenAny(allTasks, _animationHandler.WaitForTriggerAsync());

                    if (_animationHandler.IsTriggered)
                    {
                        UpdateInitializingRow();
                        _animationHandler.ResetTrigger();
                        ctx.Refresh();
                    }
                }

                // Vänta på att alla tasks är klara
                try
                {
                    await allTasks;
                }
                catch (Exception ex)
                {
                    initException = ex;
                }

                validationResult = await validationTask;

                // Om validation lyckades och det finns en continuation task, kör den nu
                if (initException is null && validationResult is not null && validationResult.IsValid && continuationTask is not null)
                {
                    try
                    {
                        await continuationTask();
                    }
                    catch (Exception ex)
                    {
                        initException = ex;
                    }
                }

                // Stoppa animation
                _animationHandler.RemoveAppDetails(SplashAnimationKey);

                if (initException is not null)
                {
                    UpdatePanelWithException(initException);
                }
                else if (validationResult is null || !validationResult.IsValid)
                {
                    UpdatePanelWithErrors(validationResult);
                }
                else
                {
                    UpdatePanelWithSuccess();
                }

                ctx.Refresh();
            });


        System.Console.CursorVisible = false;
        if (initException is not null || validationResult is null || !validationResult.IsValid)
        {
            System.Console.ReadKey(true);
            return false;
        }

        System.Console.ReadKey(true);
        return true;
    }

    private void UpdateInitializingRow()
    {
        var animation = _animationHandler.GetAnimation(SplashAnimationKey);
        var frame = animation?.AnimationFrame ?? "●";
        _contentTable.Rows.Update(2, 0, new Markup($"[orange1]{frame}[/] Initializing..."));
    }

    private void UpdatePanelWithErrors(ToolValidationResult? result)
    {
        // Ta bort initializing raden
        _contentTable.Rows.RemoveAt(2);
        
        // Lägg till error information
        _contentTable.AddRow(new Markup("[bold red]Missing required tools:[/]"));
        _contentTable.AddRow(new Markup(""));
        
        if (result?.MissingTools != null)
        {
            foreach (var tool in result.MissingTools)
            {
                _contentTable.AddRow(new Markup($"  [red]✗[/] {tool}"));
            }
        }
        
        _contentTable.AddRow(new Markup(""));
        _contentTable.AddRow(new Markup("[bold yellow]Installation instructions:[/]"));
        _contentTable.AddRow(new Markup(""));
        
        if (result?.InstallInstructions != null)
        {
            foreach (var instruction in result.InstallInstructions)
            {
                _contentTable.AddRow(new Markup($"  [gray]→[/] {instruction}"));
            }
        }
        
        _contentTable.AddRow(new Markup(""));
        _contentTable.AddRow(new Markup("[bold red]Please install the missing tools and restart the application.[/]"));
        _contentTable.AddRow(new Markup("[gray]Press any key to exit...[/]"));
        
        Panel.BorderColor(Color.Red);
    }

    private void UpdatePanelWithSuccess()
    {
        // Uppdatera initializing raden till success
        _contentTable.Rows.Update(2, 0, new Markup("[green]✓[/] All required tools are installed"));
        _contentTable.AddRow(new Markup(""));
        _contentTable.AddRow(new Markup("[gray]Press any key to continue...[/]"));
    }

    private void UpdatePanelWithException(Exception exception)
    {
        var error = InitializationErrorResolver.Resolve(exception);

        _contentTable.Rows.RemoveAt(2);

        _contentTable.AddRow(new Markup($"[bold red]{Markup.Escape(error.Title)}[/]"));

        if (error.Detail is not null)
        {
            _contentTable.AddRow(new Markup(""));
            _contentTable.AddRow(new Markup($"  {Markup.Escape(error.Detail)}"));
        }

        if (error.Actions.Length > 0)
        {
            _contentTable.AddRow(new Markup(""));
            foreach (var action in error.Actions)
            {
                _contentTable.AddRow(new Markup($"  [gray]→[/] [white]{Markup.Escape(action)}[/]"));
            }
        }

        _contentTable.AddRow(new Markup(""));
        _contentTable.AddRow(new Markup("[gray]Press any key to exit...[/]"));

        Panel.BorderColor(Color.Red);
    }
}