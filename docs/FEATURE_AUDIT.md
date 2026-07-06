# Funcy – Feature audit & öppna frågor

*Datum: 2026-07-06. Underlag: genomgång av hela kodbasen (arkitektur, UI-ramverk, datalager, Azure-access) samt README-roadmapen.*

## Sammanfattning

Funcy har en solid kärna: snabb cache-först-listning av Function Apps, start/stopp/slot-swap, subscription-växling och ett väl genomtänkt trådnings-/uppdateringsflöde (modellmutation på bakgrundstrådar, rendering på render-tråden). Det som saknas är främst **djup** — man ser *att* en funktion finns och vilken triggertyp den har, men inte *vad den gör just nu*. De implementerade featurerna nedan adresserar det.

## Implementerade features (en PR per feature)

| Feature | Branch | PR |
|---|---|---|
| Settings-vy i appen (ersätter handredigering av settings.json), öppnas med `O` | `feat/settings-view` | [#25](https://github.com/Rudenw/Funcy/pull/25) |
| Service Bus-insikt: queue/topic+subscription per SB-triggad funktion, aktiva meddelanden + DLQ | `feat/servicebus-trigger-insight` | [#27](https://github.com/Rudenw/Funcy/pull/27) |
| App Insights-loggvy per funktion (Enter på funktion): nära realtid, `E` typfilter, `F` fritextsök | `feat/appinsights-logs` | [#28](https://github.com/Rudenw/Funcy/pull/28) |
| Favoriter/pinnade Function Apps (`P`), sorteras överst, persisteras | `feat/pinned-function-apps` | [#26](https://github.com/Rudenw/Funcy/pull/26) |

Alla fyra implementerades av delegerade agenter och granskades därefter manuellt: varje diff kodgranskades, byggdes om från scratch och testkördes. Två granskningsfynd åtgärdades innan PR: en saknad exception-guard i pin-togglen (#26) och en disposal-race i loggvyns poll-loop (#28).

### Omgång 2 (efter feedback)

| Feature | Branch | PR |
|---|---|---|
| Appar med pågående operation stannar synliga genom filterbyten (swap-scenariot) | `feat/keep-active-operations-visible` | [#29](https://github.com/Rudenw/Funcy/pull/29) |
| Disable/enable av enskild funktion (`D`), via `AzureWebJobs.<namn>.Disabled` | `feat/function-disable-toggle` | [#30](https://github.com/Rudenw/Funcy/pull/30) |
| Felytning i UI: `⚠ N errors`-indikator + Issues-panel (`I`, rensa med `C`) | `feat/error-surfacing` | [#31](https://github.com/Rudenw/Funcy/pull/31) |
| Environment variables-vy (`V`), maskerade värden, `M` visar vald rad, Key Vault-referenser löses upp | `feat/app-settings-view` | [#32](https://github.com/Rudenw/Funcy/pull/32) |

Beslut i omgång 2: **DLQ-åtgärder skippas** (purge/resubmit — destruktivt, låg nytta just nu). **Live Metrics skippas** — det är genuint realtid (~1 s, QuickPulse-streaming) men flyktigt och utan historik, och huvudbehovet är felsökning i efterhand, vilket loggvyn i #28 täcker.

### Omgång 3

| Leverans | Branch | PR |
|---|---|---|
| Karakteriseringstestsvit: 243 nya tester som låser mains beteende (283 totalt, ~2 s) | `test/characterization-suite` | [#33](https://github.com/Rudenw/Funcy/pull/33) |
| Azure session health: proaktiv probe, expired-banner, `L` = device-code-relogin i appen, auto refresh-all | `feat/azure-session-health` | [#34](https://github.com/Rudenw/Funcy/pull/34) |

Testsviten är **inmergad i alla feature-brancher**. Utfall per branch: 6 av 9 hade avvikelser — **samtliga var avsiktliga featureändringar** (nya genvägar i grid:arna, nya kolumner i funktionslistan, den avsiktliga fixen av Enter-kraschen i #28); testerna uppdaterades i respektive branch med dokumenterande commits. Inga oavsiktliga beteendeförändringar hittades. `feat/keep-active-operations-visible`, `feat/error-surfacing` och `feat/azure-session-health` gick igenom helt orörda.

### Misstänkta buggar i main (hittade av testsviten, karakteriserade men inte fixade)

1. `SearchInputManager` escapar inte input — att skriva `[` i sökrutan ger ogiltig Spectre-markup och kastar exception.
2. `TryGetActionNavigationRequest` kastar på tomt urval (Enter-varianten fixades i #28, action-varianten är kvar men skyddas av `IsActionValid`).
3. `UiStatusState`: "senaste inventory-refresh"-tidsstämpeln sätts i själva verket av details-refresh, aldrig av inventory-valideringen.
4. `StateOnly`-uppdateringar i koordinatorn tappar cachade functions/slots (bara `Inventory` bevarar dem).
5. `ListPanelPaginator.MaxVisibleRows` kan bli negativ i terminaler lägre än 8 rader.
6. Selektion är indexbaserad — tar man bort vald rad glider nästa rad in under markören.

Beslut: **1–5 fixade** i [#35](https://github.com/Rudenw/Funcy/pull/35) (baserad på testsviten — merga #33 först). **6 behålls medvetet**: indexbaserad selektion är standardbeteende i TUI-verktyg (k9s gör likadant), och negativa `MaxVisibleRows` kastade aldrig (LINQ `Take(-n)` ger tom sekvens) men golvas nu på 0 ändå.

## Ytterligare features som borde finnas (ej implementerade)

1. **Function detail-vy.** Enter på en funktion går nu till loggvyn; en riktig detaljvy (bindings, senaste körningar, success rate från `requests`) vore nästa steg och kan byggas på samma App Insights-integration.
2. **Invocations/körhistorik** per funktion (`requests` + `duration`/`success`) med aggregat (t.ex. felprocent senaste timmen) direkt i funktionslistan.
3. **Health-kolumn** i app-listan (senaste deploy, runtime-version, HTTPS-only etc. finns redan i Resource Graph-svaret för liten kostnad).
4. **Resource Graph via SDK i stället för `az`-shellout** (`Azure.ResourceManager.ResourceGraph`) — tar bort beroendet på CLI-extension `resource-graph` och gör felhantering typad. (Teknisk skuld snarare än feature.)

*(Felytning och disable/enable implementerades i omgång 2; DLQ-åtgärder och Live Metrics är aktivt bortvalda, se ovan.)*

## Öppna frågor

1. **Tangentbindningar**: totalt nu `O` = Settings, `P` = Pin, `E` = cykla loggtypfilter, Enter på funktion = loggvy, `D` = disable/enable funktion, `V` = environment variables, `M` = maskera/visa värde, `I` = Issues-panel, `C` = rensa fellogg. Säg till om något känns fel — och tangentkartan börjar bli full, så en k9s-liknande kommandorad kan bli aktuell på sikt.
2. **Settings-vyns "live apply"**: TagColumns-ändringar slår igenom när en panel skapas om (inte retroaktivt på öppen panel). Räcker det, eller ska rotpanelen byggas om direkt vid ändring?
3. **Service Bus-namespace i annan subscription**: namespace-uppslag görs via Resource Graph över alla åtkomliga subscriptions. Om samma namespace-namn finns i flera tenants/subscriptions tas första träffen — behövs disambiguering?
4. **Rättigheter för SB-counts**: räkningarna går via ARM (`Microsoft.ServiceBus/namespaces/.../read`), inte data-plane. Har alla tänkta användare Reader på namespace:en? Annars visas `-`.
5. **App Insights-latens**: ingestion-latens är typiskt 1–3 min; "realtid" betyder polling var ~5:e sekund på redan ingesterad data. Är det acceptabelt, eller ska vi även titta på Live Metrics-API:et (ett helt annat, mer komplext API)?
6. **Loggvyns omfång**: filtret är `operation_Name == funktionsnamn` + `cloud_RoleName == appnamn`. Vill du även ha en app-övergripande loggvy (alla funktioner i appen)?
7. **Merge-ordning**: de fyra PR:arna är oberoende grenar från main och kan ge små konflikter i gemensamma filer (`ListPanelShortcuts`, `MainContainer.HandleInput`, `TopPanel`, EF-modellsnapshot). Merga en i taget och bygg om mellan; säg till om du vill att jag rebase:ar de kvarvarande efter varje merge.
8. **Pin-persistens vid app-borttag**: om en app försvinner ur Azure och senare återkommer (nytt inventory-row) är pinnen borta — medvetet val (pinnen sitter på DB-raden). OK?

## Noterat under genomgången (ej åtgärdat)

- `FunctionAppManagementService` skapar en egen `ArmClient`/`DefaultAzureCredential` i stället för att ta den injicerade — inkonsekvens som kan ge extra token-hämtningar.
- `UpdateSource`-enumen verkar vara vestigial (refereras inte i koordinatorflödet).
- `Funcy.sln.DotSettings.user` (39 kB) ligger i repot — bör troligen gitignoreras.
- Branchen `fix/dispose-popped-panel-controllers` på GitHub är redan inmergad via PR #20 och kan tas bort.
