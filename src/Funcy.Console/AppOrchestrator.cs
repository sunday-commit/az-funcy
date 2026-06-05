using Funcy.Console.Handlers;
using Funcy.Console.Ui;
using Funcy.Console.Ui.Factory;
using Funcy.Console.Ui.State;

namespace Funcy.Console;

using Spectre.Console;

public class AppOrchestrator(
    InputHandler inputHandler,
    ResizeHandler resizeHandler,
    AnimationHandler animationHandler,
    FunctionAppUpdateHandler functionAppUpdateHandler,
    IActionDispatcher actionDispatcher,
    ListPanelContextFactory listPanelContextFactory,
    UiStateMarkupProvider uiStateMarkupProvider,
    AppContext appContext)
{
    private MainContainer _mainContainer = null!;

    public async Task StartAsync()
    {
        var cts = new CancellationTokenSource();

        _mainContainer = new MainContainer(listPanelContextFactory, actionDispatcher, functionAppUpdateHandler,
            uiStateMarkupProvider, appContext);
        // InitializeAsync is now done in Program.cs before StartAsync
        _ = functionAppUpdateHandler.SynchronizeFunctionAppDataAsync();
        
        var resizeTask = resizeHandler.StartPolling(cts.Token);
        var inputTask = inputHandler.StartListeningAsync(cts.Token);
        
        await HandleInputAndRenderAsync(cts.Token);
        
        await cts.CancelAsync();
        await inputTask;
        await resizeTask;
    }
    
    private async Task WaitForAnyTriggerAsync()
    {
        await Task.WhenAny(
            resizeHandler.WaitForTriggerAsync(),
            inputHandler.WaitForTriggerAsync(),
            _mainContainer.WaitForTriggerAsync(),
        animationHandler.WaitForTriggerAsync());
    }

    private async Task HandleInputAndRenderAsync(CancellationToken token)
    {
        var mainLayout = _mainContainer.MainLayout;

        while (true)
        {
            AnsiConsole.Clear();
            await AnsiConsole.Live(mainLayout).StartAsync(async ctx =>
            {
                while (!token.IsCancellationRequested)
                {
                    ctx.Refresh();
                    await WaitForAnyTriggerAsync();

                    if (inputHandler.IsTriggered)
                    {
                        _mainContainer.HandleInput(inputHandler.TriggeredKeyInfo);
                        inputHandler.ResetTrigger();
                    }
                    
                    if (resizeHandler.IsTriggered)
                    {
                        _mainContainer.HandleResize();
                        resizeHandler.ResetTrigger();
                        break;
                    }
                    
                    if (animationHandler.IsTriggered)
                    {
                        _mainContainer.HandleAnimation();
                        animationHandler.ResetTrigger();
                    }

                    if (_mainContainer.WaitForTriggerAsync().IsCompleted)
                    {
                        _mainContainer.HandleUpdate();
                        _mainContainer.ResetTrigger();
                    }
                }

                return Task.CompletedTask;
            });
        }
    }
}