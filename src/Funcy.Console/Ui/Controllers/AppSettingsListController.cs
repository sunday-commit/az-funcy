using Funcy.Console.Ui.Panels.Interfaces;
using Funcy.Core.Interfaces;
using Funcy.Core.Model;
using Microsoft.Extensions.Logging;

namespace Funcy.Console.Ui.Controllers;

public sealed class AppSettingsListController : ListPanelControllerBase<AppSettingDetails>, IMaskToggleController
{
    private static readonly string LoadingMessage = $"[{UiStyles.Hint}]Loading environment variables…[/]";
    private static readonly string EmptyMessage = $"[{UiStyles.Hint}]No environment variables found.[/]";
    private static readonly string ErrorMessage =
        $"[{UiStyles.Danger}]Failed to load environment variables. See the log for details.[/]";

    private readonly string _appArmId;
    private readonly string _appName;
    private readonly IAppSettingsService _settingsService;
    private readonly IKeyVaultSecretResolver _secretResolver;
    private readonly AppSettingsEmptyState _emptyState;
    private readonly ILogger _logger;
    private readonly Action? _invalidate;

    // Cancelled on Dispose (panel popped) so in-flight fetch/resolution can't touch a dead view.
    private readonly CancellationTokenSource _cts = new();
    private readonly Lock _gate = new();
    private readonly Dictionary<string, AppSettingDetails> _items = new(StringComparer.Ordinal);

    public AppSettingsListController(IListPanelView<AppSettingDetails> view,
        string appArmId,
        string appName,
        IAppSettingsService settingsService,
        IKeyVaultSecretResolver secretResolver,
        AppSettingsEmptyState emptyState,
        ILogger logger,
        Action? invalidate = null)
        : base(view)
    {
        _appArmId = appArmId;
        _appName = appName;
        _settingsService = settingsService;
        _secretResolver = secretResolver;
        _emptyState = emptyState;
        _logger = logger;
        _invalidate = invalidate;

        _emptyState.Message = LoadingMessage;
        View.SetAll([]);
        _invalidate?.Invoke();

        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var settings = await _settingsService.GetApplicationSettingsAsync(_appArmId, _cts.Token);
            if (_cts.IsCancellationRequested)
            {
                return;
            }

            lock (_gate)
            {
                _items.Clear();
                foreach (var setting in settings)
                {
                    _items[setting.Key] = setting;
                }
            }

            _emptyState.Message = settings.Count == 0 ? EmptyMessage : null;
            View.SetAll(settings);
            _invalidate?.Invoke();
        }
        catch (OperationCanceledException)
        {
            // Panel was popped mid-fetch — nothing to render.
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to load application settings for {FunctionAppName}", _appName);
            _emptyState.Message = ErrorMessage;
            _invalidate?.Invoke();
        }
    }

    // Toggles the mask of the selected row only. Revealing an unresolved Key Vault reference
    // kicks off resolution; re-masking keeps any already-resolved value so re-reveal is instant.
    public void ToggleSelectedMask()
    {
        var key = View.GetSelectedItemKey();
        if (string.IsNullOrEmpty(key))
        {
            return;
        }

        AppSettingDetails? item;
        lock (_gate)
        {
            _items.TryGetValue(key, out item);
        }

        if (item is null)
        {
            return;
        }

        item.Masked = !item.Masked;

        if (!item.Masked && item.IsKeyVaultReference &&
            item.ResolutionState is SecretResolutionState.Pending or SecretResolutionState.Failed)
        {
            item.ResolutionState = SecretResolutionState.Resolving;
            View.Upsert(item);
            _invalidate?.Invoke();
            _ = ResolveAsync(item);
            return;
        }

        View.Upsert(item);
        _invalidate?.Invoke();
    }

    private async Task ResolveAsync(AppSettingDetails item)
    {
        try
        {
            var value = await _secretResolver.ResolveAsync(item.KeyVaultReference!, _cts.Token);
            if (_cts.IsCancellationRequested)
            {
                return;
            }

            item.ResolvedValue = value;
            item.ResolutionState = SecretResolutionState.Resolved;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to resolve Key Vault secret for setting {SettingName}", item.Name);
            item.ResolutionState = SecretResolutionState.Failed;
        }

        View.Upsert(item);
        _invalidate?.Invoke();
    }

    public override void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
        base.Dispose();
    }
}
