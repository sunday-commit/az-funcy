namespace Funcy.Core.Model;

// A single application setting (environment variable) of a function app. Sorted and keyed
// by name. Values are masked by default; view-facing reveal/resolution state lives here too.
public class AppSettingDetails : IComparable<AppSettingDetails>, IHasKey
{
    public required string Name { get; init; }
    public required string Value { get; init; }

    public string Key => Name;

    public KeyVaultReference? KeyVaultReference { get; init; }
    public bool IsKeyVaultReference => KeyVaultReference is not null;

    // Reveal state — only the selected row is ever toggled.
    public bool Masked { get; set; } = true;

    // Resolved Key Vault secret value, cached for the panel's lifetime once fetched.
    public string? ResolvedValue { get; set; }
    public SecretResolutionState ResolutionState { get; set; } = SecretResolutionState.Pending;
    public string? ResolutionErrorMessage { get; set; }

    // Transient "copied to clipboard" confirmation flag, cleared shortly after the copy.
    public bool JustCopied { get; set; }

    // The real value the user can currently see, or null when the row is masked or its Key Vault
    // secret hasn't resolved. Copy-to-clipboard uses this so it never leaks a hidden value.
    public string? RevealedValue => Masked
        ? null
        : IsKeyVaultReference
            ? ResolutionState == SecretResolutionState.Resolved ? ResolvedValue : null
            : Value;

    public int CompareTo(AppSettingDetails? other)
        => other is null ? 1 : StringComparer.Ordinal.Compare(Name, other.Name);
}
