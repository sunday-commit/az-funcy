using Funcy.Console.Handlers;
using Funcy.Infrastructure.Shell;
using Spectre.Console;

namespace Funcy.Console.Ui;

public class SplashScreen
{
    private readonly ToolValidationService _toolValidationService;
    private readonly AnimationHandler _animationHandler;
    private const string SplashAnimationKey = "SplashScreen";

    public SplashScreen(ToolValidationService toolValidationService, AnimationHandler animationHandler)
    {
        _toolValidationService = toolValidationService;
        _animationHandler = animationHandler;
    }

    public async Task<bool> ShowAsync(Task[] backgroundTasks, Func<Task>? continuationTask = null)
    {
        AnsiConsole.Clear();
        AnsiConsole.Cursor.Hide();

        var width = AnsiConsole.Profile.Width;
        ToolValidationResult? validationResult = null;
        Exception? initException = null;

        // Start the splash spinner animation.
        _animationHandler.AddAppDetails(SplashAnimationKey);

        await AnsiConsole.Live(SplashScreenLayout.Initializing(width, CurrentFrame()))
            .StartAsync(async ctx =>
            {
                ctx.Refresh();

                var validationTask = _toolValidationService.ValidateRequiredToolsAsync();
                var allTasks = Task.WhenAll(backgroundTasks.Append(validationTask));

                // Animate while the startup tasks run.
                while (!allTasks.IsCompleted)
                {
                    await Task.WhenAny(allTasks, _animationHandler.WaitForTriggerAsync());

                    if (_animationHandler.IsTriggered)
                    {
                        ctx.UpdateTarget(SplashScreenLayout.Initializing(width, CurrentFrame()));
                        _animationHandler.ResetTrigger();
                        ctx.Refresh();
                    }
                }

                // Wait for all startup tasks to finish.
                try
                {
                    await allTasks;
                }
                catch (Exception ex)
                {
                    initException = ex;
                }

                validationResult = await validationTask;

                // If validation succeeded and there is a continuation task, run it now.
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

                // Stop the animation.
                _animationHandler.RemoveAppDetails(SplashAnimationKey);

                if (initException is not null)
                {
                    ctx.UpdateTarget(SplashScreenLayout.Exception(width, initException));
                }
                else if (validationResult is null || !validationResult.IsValid)
                {
                    ctx.UpdateTarget(SplashScreenLayout.Errors(width, validationResult));
                }
                else
                {
                    ctx.UpdateTarget(SplashScreenLayout.Success(width));
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

    private string CurrentFrame()
        => _animationHandler.GetAnimation(SplashAnimationKey)?.AnimationFrame ?? _animationHandler.CurrentFrame;
}
