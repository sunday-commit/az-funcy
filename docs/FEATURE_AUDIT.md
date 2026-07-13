# az-funcy – Feature audit & open questions

*Date: 2026-07-06. Basis: a walkthrough of the whole codebase (architecture, UI framework, data layer, Azure access) plus the README roadmap.*

## Summary

az-funcy has a solid core: fast cache-first listing of Function Apps, start/stop/slot-swap, subscription switching, and a well-thought-out threading/update flow (model mutation on background threads, rendering on the render thread). What's mainly missing is **depth** — you can see *that* a function exists and which trigger type it has, but not *what it is doing right now*. The implemented features below address that.

## Implemented features (one PR per feature)

| Feature | Branch | PR |
|---|---|---|
| In-app settings view (replaces hand-editing settings.json), opened with `O` | `feat/settings-view` | [#25](https://github.com/sunday-commit/az-funcy/pull/25) |
| Service Bus insight: queue/topic+subscription per SB-triggered function, active messages + DLQ | `feat/servicebus-trigger-insight` | [#27](https://github.com/sunday-commit/az-funcy/pull/27) |
| App Insights log view per function (Enter on a function): near real-time, `E` type filter, `F` free-text search | `feat/appinsights-logs` | [#28](https://github.com/sunday-commit/az-funcy/pull/28) |
| Favorites/pinned Function Apps (`P`), sorted to the top, persisted | `feat/pinned-function-apps` | [#26](https://github.com/sunday-commit/az-funcy/pull/26) |

All four were implemented by delegated agents and then reviewed manually: every diff was code-reviewed, rebuilt from scratch, and test-run. Two review findings were fixed before the PR: a missing exception guard in the pin toggle (#26) and a disposal race in the log view's poll loop (#28).

### Round 2 (after feedback)

| Feature | Branch | PR |
|---|---|---|
| Apps with an in-progress operation stay visible across filter changes (the swap scenario) | `feat/keep-active-operations-visible` | [#29](https://github.com/sunday-commit/az-funcy/pull/29) |
| Disable/enable an individual function (`D`), via `AzureWebJobs.<name>.Disabled` | `feat/function-disable-toggle` | [#30](https://github.com/sunday-commit/az-funcy/pull/30) |
| Error surfacing in the UI: `⚠ N errors` indicator + Issues panel (`I`, clear with `C`) | `feat/error-surfacing` | [#31](https://github.com/sunday-commit/az-funcy/pull/31) |
| Environment variables view (`V`), masked values, `M` reveals the selected row, Key Vault references are resolved | `feat/app-settings-view` | [#32](https://github.com/sunday-commit/az-funcy/pull/32) |

Round 2 decisions: **DLQ actions are skipped** (purge/resubmit — destructive, low value right now). **Live Metrics is skipped** — it is genuinely real-time (~1 s, QuickPulse streaming) but volatile and without history, and the main need is after-the-fact troubleshooting, which the log view in #28 covers.

### Round 3

| Deliverable | Branch | PR |
|---|---|---|
| Characterization test suite: 243 new tests that lock in main's behavior (283 total, ~2 s) | `test/characterization-suite` | [#33](https://github.com/sunday-commit/az-funcy/pull/33) |
| Azure session health: proactive probe, expired banner, `L` = in-app device-code re-login, auto refresh-all | `feat/azure-session-health` | [#34](https://github.com/sunday-commit/az-funcy/pull/34) |

The test suite is **merged into every feature branch**. Outcome per branch: 6 of 9 had deviations — **all were intentional feature changes** (new shortcuts in the grids, new columns in the function list, the intentional fix of the Enter crash in #28); the tests were updated in each branch with documenting commits. No unintended behavior changes were found. `feat/keep-active-operations-visible`, `feat/error-surfacing`, and `feat/azure-session-health` passed completely untouched.

### Suspected bugs in main (found by the test suite, characterized but not fixed)

1. `SearchInputManager` does not escape input — typing `[` in the search box produces invalid Spectre markup and throws an exception.
2. `TryGetActionNavigationRequest` throws on an empty selection (the Enter variant was fixed in #28; the action variant remains but is guarded by `IsActionValid`).
3. `UiStatusState`: the "last inventory refresh" timestamp is actually set by the details refresh, never by the inventory validation.
4. `StateOnly` updates in the coordinator drop cached functions/slots (only `Inventory` preserves them).
5. `ListPanelPaginator.MaxVisibleRows` can go negative in terminals shorter than 8 rows.
6. Selection is index-based — removing the selected row slides the next row in under the cursor.

Decision: **1–5 fixed** in [#35](https://github.com/sunday-commit/az-funcy/pull/35) (based on the test suite — merge #33 first). **6 kept deliberately**: index-based selection is standard behavior in TUI tools (k9s does the same), and a negative `MaxVisibleRows` never threw (LINQ `Take(-n)` yields an empty sequence) but is now floored at 0 anyway.

## Additional features that should exist (not implemented)

1. **Function detail view.** Enter on a function now goes to the log view; a real detail view (bindings, latest runs, success rate from `requests`) would be the next step and can build on the same App Insights integration.
2. **Invocations/run history** per function (`requests` + `duration`/`success`) with aggregates (e.g. error rate over the last hour) directly in the function list.
3. **Health column** in the app list (last deploy, runtime version, HTTPS-only, etc. are already in the Resource Graph response for little cost).
4. **Resource Graph via the SDK instead of an `az` shell-out** (`Azure.ResourceManager.ResourceGraph`) — removes the dependency on the `resource-graph` CLI extension and makes error handling typed. (Technical debt rather than a feature.)

*(Error surfacing and disable/enable were implemented in round 2; DLQ actions and Live Metrics are actively opted out, see above.)*

## Open questions

1. **Key bindings**: the full set is now `O` = Settings, `P` = Pin, `E` = cycle log type filter, Enter on a function = log view, `D` = disable/enable function, `V` = environment variables, `M` = mask/reveal value, `I` = Issues panel, `C` = clear error log. Let me know if anything feels off — and the key map is getting full, so a k9s-like command line may become relevant down the line.
2. **The settings view's "live apply"**: TagColumns changes take effect when a panel is rebuilt (not retroactively on an open panel). Is that enough, or should the root panel be rebuilt immediately on change?
3. **Service Bus namespace in another subscription**: namespace lookup is done via Resource Graph across all accessible subscriptions. If the same namespace name exists in several tenants/subscriptions, the first match is taken — is disambiguation needed?
4. **Permissions for SB counts**: the counts go via ARM (`Microsoft.ServiceBus/namespaces/.../read`), not the data plane. Do all intended users have Reader on the namespaces? Otherwise `-` is shown.
5. **App Insights latency**: ingestion latency is typically 1–3 min; "real-time" means polling roughly every 5 seconds over already-ingested data. Is that acceptable, or should we also look at the Live Metrics API (a wholly different, more complex API)?
6. **Log view scope**: the filter is `operation_Name == function name` + `cloud_RoleName == app name`. Do you also want an app-wide log view (all functions in the app)?
7. **Merge order**: the four PRs are independent branches off main and may cause small conflicts in shared files (`ListPanelShortcuts`, `MainContainer.HandleInput`, `TopPanel`, the EF model snapshot). Merge one at a time and rebuild in between; let me know if you want me to rebase the remaining ones after each merge.
8. **Pin persistence on app removal**: if an app disappears from Azure and later reappears (a new inventory row), the pin is gone — a deliberate choice (the pin sits on the DB row). OK?

## Noted during the walkthrough (not addressed)

- `FunctionAppManagementService` creates its own `ArmClient`/`DefaultAzureCredential` instead of taking the injected one — an inconsistency that can cause extra token fetches.
- The `UpdateSource` enum appears to be vestigial (not referenced in the coordinator flow).
- `Funcy.sln.DotSettings.user` (39 kB) is in the repo — should probably be gitignored.
- The branch `fix/dispose-popped-panel-controllers` on GitHub is already merged via PR #20 and can be deleted.
