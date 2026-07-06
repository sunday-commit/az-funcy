# Funcy – Feature audit & öppna frågor

*Datum: 2026-07-06. Underlag: genomgång av hela kodbasen (arkitektur, UI-ramverk, datalager, Azure-access) samt README-roadmapen.*

## Sammanfattning

Funcy har en solid kärna: snabb cache-först-listning av Function Apps, start/stopp/slot-swap, subscription-växling och ett väl genomtänkt trådnings-/uppdateringsflöde (modellmutation på bakgrundstrådar, rendering på render-tråden). Det som saknas är främst **djup** — man ser *att* en funktion finns och vilken triggertyp den har, men inte *vad den gör just nu*. De implementerade featurerna nedan adresserar det.

## Implementerade features (en PR per feature)

| Feature | Branch | PR |
|---|---|---|
| Settings-vy i appen (ersätter handredigering av settings.json) | `feat/settings-view` | *(fylls i)* |
| Service Bus-insikt: queue/topic+subscription per SB-triggad funktion, aktiva meddelanden + DLQ | `feat/servicebus-trigger-insight` | *(fylls i)* |
| App Insights-loggvy per funktion: nära realtid, typfilter (traces/exceptions/requests), fritextsök | `feat/appinsights-logs` | *(fylls i)* |
| Favoriter/pinnade Function Apps (P), sorteras överst, persisteras | `feat/pinned-function-apps` | *(fylls i)* |

## Ytterligare features som borde finnas (ej implementerade nu)

1. **Förbättrad felytning i UI** (roadmap). Fel från Azure-anrop loggas idag mest till fil; `FunctionAppFetchResult` bär redan felmeddelanden men UI:t visar dem knappt. En dedikerad "problems"-rad/panel vore billig att bygga på samma listpanelsmönster.
2. **Function detail-vy.** Enter på en funktion går nu till loggvyn; en riktig detaljvy (bindings, senaste körningar, success rate från `requests`) vore nästa steg och kan byggas på samma App Insights-integration.
3. **Invocations/körhistorik** per funktion (`requests` + `duration`/`success`) med aggregat (t.ex. felprocent senaste timmen) direkt i funktionslistan.
4. **Åtgärder på Service Bus**: purge DLQ / re-submit av DLQ-meddelanden. Kräver data-plane-rättigheter och försiktighet (destruktivt) — bör gated:as bakom bekräftelse.
5. **Disable/enable av enskild funktion** (app setting `AzureWebJobs.<name>.Disabled`) — naturlig syskonåtgärd till start/stopp av hela appen.
6. **Health-kolumn** i app-listan (senaste deploy, runtime-version, HTTPS-only etc. finns redan i Resource Graph-svaret för liten kostnad).
7. **Resource Graph via SDK i stället för `az`-shellout** (`Azure.ResourceManager.ResourceGraph`) — tar bort beroendet på CLI-extension `resource-graph` och gör felhantering typad. (Teknisk skuld snarare än feature.)

## Öppna frågor

1. **Tangentbindningar**: jag valde `O` = Settings, `P` = Pin, `E` = cykla loggtypfilter, Enter på funktion = loggvy. Säg till om andra bindningar känns mer naturliga.
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
