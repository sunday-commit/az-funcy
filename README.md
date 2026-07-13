# az-funcy

az-funcy is a terminal-based (TUI) tool for monitoring and administering **Azure Function Apps** across Azure subscriptions.

It is built with **Spectre.Console** and inspired by tools like **btop** and **k9s**, focusing on fast feedback, keyboard-driven workflows, and minimal friction.

---

## Features

- List Azure Function Apps in the active subscription
- Fast startup using a local cache, followed by background refresh from Azure
- Start and stop Function Apps
- Swap deployment slots to production
- Switch Azure subscriptions at runtime
- Filter and sort large lists efficiently
- View Function App environment variables and resolve Key Vault references on demand
- View Application Insights logs for individual functions
- View Service Bus active and dead-letter message counts
- Fully keyboard-driven UI

---

## Requirements

### Runtime

- **.NET 10**
- **Azure CLI (`az`)**

### Azure CLI extensions

az-funcy depends on **Azure Resource Graph**, which is **not installed by default**.

Install it explicitly:

```bash
az extension add --name resource-graph
```

Verify installation:

```bash
az extension list
```

### Azure authentication

az-funcy uses `DefaultAzureCredential`.

You must be logged in via Azure CLI:

```bash
az login
```

### Azure permissions

az-funcy keeps optional capabilities visible when access is missing. An attempted operation that
Azure rejects with `403 Forbidden` shows the required role in the UI; it does not remove the
feature. Assign roles at the narrowest practical scope.

For the main administration workflow, grant:

- **Reader** on the subscriptions or resource groups that az-funcy should inventory.
- **Website Contributor** on the Function Apps or their resource group to start and stop apps,
  swap slots, enable or disable functions, and read application settings. The broader
  **Contributor** role also works but is not required by az-funcy.

Optional capabilities require additional access:

| Capability | Minimum built-in role | Recommended scope | Behavior without access |
|------------|-----------------------|-------------------|-------------------------|
| List Function Apps and their resources | Reader | Subscription or resource group | Inventory refresh reports an authorization error |
| Administer Function Apps and read environment variables | Website Contributor | Function App or resource group | The action remains available and reports the required role |
| Read Service Bus queue/subscription message counts | Reader | Service Bus namespace or resource group | Functions remain visible; counts show as unavailable and the Issues view explains the requirement |
| Resolve Key Vault secret values | Key Vault Secrets User | Key Vault | The reference remains visible and revealing it reports the required role |
| Query Application Insights logs | Website Contributor plus Monitoring Reader | Function App plus Application Insights resource | The Logs view remains available and reports the required roles |
| Query workspace-based logs when workspace permissions are enforced | Log Analytics Reader | Log Analytics workspace | The Logs view reports the additional workspace requirement |

Service Bus counts use Azure Resource Manager metadata, so **Azure Service Bus Data Receiver** is
not required. Key Vaults using access policies instead of Azure RBAC need an equivalent secret
`Get` permission. Network rules and private endpoints can still prevent Key Vault access even when
the role assignment is correct.

---

## Getting started

1. Log in to Azure:
   ```bash
   az login
   ```

2. (Optional) Set a default subscription:
   ```bash
   az account set --subscription "<subscription name or id>"
   ```

3. Build and run:
   ```bash
   dotnet run --project src/Funcy.Console
   ```

On startup, az-funcy:
1. Loads Function Apps from a local database cache (fast)
2. Refreshes data from Azure in the background

---

## Configuration

On first run, az-funcy creates a `settings.json` file in the data directory:

| Platform | Path |
|----------|------|
| Windows  | `%LOCALAPPDATA%\Funcy\settings.json` |
| macOS    | `~/Library/Application Support/Funcy/settings.json` |
| Linux    | `~/.local/share/funcy/settings.json` |

Edit it to customize az-funcy's behavior:

```json
{
  "Funcy": {
    "TagColumns": [ "System", "Team" ]
  }
}
```

**`TagColumns`** – which Azure resource tags to display as columns in the Function App list.

---

## Subscription switching

az-funcy supports **switching Azure subscriptions at runtime**.

- The currently active subscription is shown in the top panel
- Use the subscription shortcut to open the **Switch Subscription** view
- Selecting a new subscription:
  - Updates the global application context
  - Clears cached Function Apps
  - Reloads data for the new subscription (cache → Azure)

After switching, you always return to the Function Apps view.

---

## Keyboard shortcuts

### Global

- **F** – Filter
- **R** – Refresh
- **S** – Start Function App
- **T** – Stop Function App
- **W** – Swap slot to production
- **Enter** – Navigate into selection
- **Esc / Space** – Go back
- **Delete** – Clear filter
- **↑ / ↓ / PgUp / PgDn** – Scroll
- **1..n** – Sort by column
- **U** – Open subscription switcher

---

## Notes & limitations

- Azure Resource Graph **must** be installed, or no Function Apps will be listed
- Visible subscriptions depend on the active Azure CLI account and tenant

---

## Roadmap (informal)

- Settings view
- Favorites / pinned Function Apps
- Hide functionality for subscriptions
- Throttle refresh on subscription change (max once every 5 minutes)

## License

This project is licensed under the MIT License. See [LICENSE](./LICENSE) for details.
