using Funcy.Console.Ui.Panels.Interfaces;
using Funcy.Core.Interfaces;
using Funcy.Core.Model;
using Microsoft.Extensions.Logging;
using Funcy.Infrastructure.Azure;

namespace Funcy.Console.Ui.Controllers;

public sealed class AppSettingsListController : ListPanelControllerBase<AppSettingDetails>, IMaskToggleController, IClipboardCopyController
{
    // How long the "✓ copied" confirmation stays on the row before the value reappears.
    private static readonly TimeSpan CopiedConfirmationDuration = TimeSpan.FromSeconds(1.5);

    private static readonly string LoadingMessage = $"[{UiStyles.Hint}]Loading environment variables…[/]";
    private static readonly string EmptyMessage = $"[{UiStyles.Hint}]No environment variables found.[/]";
    private static readonly string GenericErrorMessage =
        $"[{UiStyles.Danger}]Failed to load environment variables. See the log for details.[/]";

    private readonly string _appArmId;
    private readonly string _appName;
    private readonly IAppSettingsService _settingsService;
    private readonly IKeyVaultSecretResolver _secretResolver;
    private readonly IClipboardService _clipboard;
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
        IClipboardService clipboard,
        AppSettingsEmptyState emptyState,
        ILogger logger,
        Action? invalidate = null)
        : base(view)
    {
        _appArmId = appArmId;
        _appName = appName;
        _settingsService = settingsService;
        _secretResolver = secretResolver;
        _clipboard = clipboard;
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
            var message = AzurePermissionError.IsAccessDenied(e)
                ? AzurePermissionError.Required("Environment variables",
                    "Website Contributor on the Function App")
                : "Failed to load environment variables. See the log for details.";
            _emptyState.Message = AzurePermissionError.IsAccessDenied(e)
                ? $"[{UiStyles.Danger}]{message}[/]"
                : GenericErrorMessage;
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
            item.ResolutionErrorMessage = null;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to resolve Key Vault secret for setting {SettingName}", item.Name);
            item.ResolutionState = SecretResolutionState.Failed;
            item.ResolutionErrorMessage = AzurePermissionError.IsAccessDenied(e)
                ? "Access denied. Required: Key Vault Secrets User."
                : "Could not resolve secret. Check the log for details.";
        }

        View.Upsert(item);
        _invalidate?.Invoke();
    }

    // Copies the selected row's revealed value to the OS clipboard, then flashes a brief
    // confirmation on the row. Masked or unresolved rows have nothing to copy and are ignored.
    public void CopySelectedValue()
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

        if (item?.RevealedValue is not { } value)
        {
            return;
        }

        _ = CopyAsync(item, value);
    }

    private async Task CopyAsync(AppSettingDetails item, string value)
    {
        bool copied;
        try
        {
            copied = await _clipboard.TryCopyAsync(value, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (_cts.IsCancellationRequested)
        {
            return;
        }

        if (!copied)
        {
            _logger.LogWarning("Clipboard copy failed for setting {SettingName} on {FunctionAppName}",
                item.Name, _appName);
            return;
        }

        item.JustCopied = true;
        View.Upsert(item);
        _invalidate?.Invoke();

        try
        {
            await Task.Delay(CopiedConfirmationDuration, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        item.JustCopied = false;
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
