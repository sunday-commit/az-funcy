using Funcy.Core.Model;

namespace Funcy.Demo;

/// <summary>
/// Builds the fabricated estate the demo renders. Composed rather than sampled from a real
/// tenant: every panel has something worth looking at (dead letters, a stopped app, a staging
/// slot, disabled functions, Key Vault references) and no real resource name can leak.
/// </summary>
internal static class DemoDataset
{
    public const string ProductionSubscriptionId = "5f2c9d41-0e7b-4a68-9c31-8b7e42d05a19";
    public const string TestSubscriptionId = "a1d84c30-6f52-4b19-8e77-2c9051fd3b64";
    public const string SandboxSubscriptionId = "c73b1e58-94af-4d20-b6e1-71f8ac5290d3";

    public static List<SubscriptionDetails> CreateSubscriptions() =>
    [
        new() { Name = "Integration Platform - Production", Id = ProductionSubscriptionId, Current = true },
        new() { Name = "Integration Platform - Test", Id = TestSubscriptionId },
        new() { Name = "Data Platform - Sandbox", Id = SandboxSubscriptionId }
    ];

    public static List<FunctionAppDetails> CreateApps() =>
    [
        // Orders: the flow the demo filters down to. The receiver carries the dead letters.
        App("func-orders-receiver-prd", "rg-orders-prd", "Orders", FunctionState.Running,
            functions:
            [
                SbQueue("ReceiveOrder", "orders-inbound", active: 4, deadLettered: 12),
                SbQueue("ReceiveOrderCancellation", "orders-cancellations", active: 0, deadLettered: 0),
                Http("HealthCheck")
            ]),
        App("func-orders-mapper-prd", "rg-orders-prd", "Orders", FunctionState.Running,
            functions:
            [
                SbQueue("MapOrder", "orders-received", active: 1, deadLettered: 0),
                SbTopic("MapOrderLine", "orders", "mapper", active: 0, deadLettered: 2)
            ]),
        App("func-orders-sender-prd", "rg-orders-prd", "Orders", FunctionState.Running,
            functions:
            [
                SbQueue("SendOrderToErp", "orders-outbound", active: 0, deadLettered: 0),
                Timer("RetryFailedOrders")
            ]),
        App("func-orders-status-prd", "rg-orders-prd", "Orders", FunctionState.Running,
            functions:
            [
                Timer("PollOrderStatus"),
                Http("GetOrderStatus"),
                Timer("PurgeStatusHistory", isDisabled: true)
            ]),

        App("func-inventory-sync-prd", "rg-inventory-prd", "Inventory", FunctionState.Running,
            functions:
            [
                Timer("SyncStockLevels"),
                Http("TriggerFullSync"),
                SbQueue("ApplyStockAdjustment", "inventory-adjustments", active: 2, deadLettered: 0)
            ]),
        App("func-inventory-receiver-prd", "rg-inventory-prd", "Inventory", FunctionState.Running,
            functions:
            [
                SbQueue("ReceiveStockEvent", "inventory-inbound", active: 37, deadLettered: 0)
            ]),

        App("func-pricing-publisher-prd", "rg-pricing-prd", "Pricing", FunctionState.Running,
            functions:
            [
                SbTopic("PublishPriceChange", "pricing", "publisher", active: 0, deadLettered: 0),
                Timer("PublishDailyPriceList")
            ]),
        // A stopped app: the list shows the state, and S starts it in the demo.
        App("func-pricing-calculator-prd", "rg-pricing-prd", "Pricing", FunctionState.Stopped,
            functions: []),

        App("func-customers-importer-prd", "rg-customers-prd", "Customers", FunctionState.Running,
            functions:
            [
                Blob("ImportCustomerFile"),
                Timer("ReconcileCustomers")
            ]),

        // Carries a staging slot so the swap action has something to act on.
        App("func-invoices-exporter-prd", "rg-invoices-prd", "Invoices", FunctionState.Running,
            functions:
            [
                Timer("ExportInvoices"),
                Http("ExportInvoiceById")
            ],
            slots: ["staging"]),

        App("func-shipments-tracker-prd", "rg-shipments-prd", "Shipments", FunctionState.Running,
            functions:
            [
                SbQueue("TrackShipment", "shipments-inbound", active: 0, deadLettered: 3),
                Http("GetTrackingNumber")
            ]),

        App("func-reporting-nightly-prd", "rg-reporting-prd", "Reporting", FunctionState.Running,
            functions:
            [
                Timer("BuildNightlyReport"),
                Timer("ArchiveReports")
            ]),

        // Pinned, so the list opens on a pinned-first row and the P shortcut has visible meaning.
        App("func-webhooks-dispatcher-prd", "rg-webhooks-prd", "Webhooks", FunctionState.Running,
            isPinned: true,
            functions:
            [
                Http("DispatchWebhook"),
                SbQueue("RetryWebhook", "webhooks-retry", active: 1, deadLettered: 0)
            ]),

        App("func-orders-receiver-tst", "rg-orders-tst", "Orders", FunctionState.Running,
            subscriptionId: TestSubscriptionId, environment: "Test",
            functions:
            [
                SbQueue("ReceiveOrder", "orders-inbound", active: 0, deadLettered: 0),
                Http("HealthCheck")
            ]),
        App("func-orders-mapper-tst", "rg-orders-tst", "Orders", FunctionState.Running,
            subscriptionId: TestSubscriptionId, environment: "Test",
            functions: [SbQueue("MapOrder", "orders-received", active: 0, deadLettered: 1)]),
        App("func-inventory-sync-tst", "rg-inventory-tst", "Inventory", FunctionState.Stopped,
            subscriptionId: TestSubscriptionId, environment: "Test",
            functions: []),
        App("func-pricing-publisher-tst", "rg-pricing-tst", "Pricing", FunctionState.Running,
            subscriptionId: TestSubscriptionId, environment: "Test",
            functions: [SbTopic("PublishPriceChange", "pricing", "publisher", active: 0, deadLettered: 0)])

        // The sandbox subscription deliberately has no function apps, so "hide empty" has an
        // effect the demo can show.
    ];

    /// <summary>Application settings for one app. The two Key Vault references drive the reveal
    /// flow; every value here is fabricated.</summary>
    public static List<AppSettingDetails> CreateSettings(string appName)
    {
        var vault = $"kv-{Domain(appName)}-prd";
        return
        [
            Setting("APPINSIGHTS_INSTRUMENTATIONKEY", "8d3f1a92-5c74-42be-9f10-6ab27de54c83"),
            Setting("APPLICATIONINSIGHTS_CONNECTION_STRING",
                "InstrumentationKey=8d3f1a92-5c74-42be-9f10-6ab27de54c83;IngestionEndpoint=https://westeurope-5.in.applicationinsights.azure.com/"),
            Setting("AzureWebJobsStorage",
                $"DefaultEndpointsProtocol=https;AccountName=st{Domain(appName)}prd;AccountKey=Ex4mpleD3moK3yNotR3al==;EndpointSuffix=core.windows.net"),
            Setting("FUNCTIONS_EXTENSION_VERSION", "~4"),
            Setting("FUNCTIONS_WORKER_RUNTIME", "dotnet-isolated"),
            Setting("ServiceBusConnection__fullyQualifiedNamespace", "sb-integration-prd.servicebus.windows.net"),
            Setting("ErpBaseUrl", "https://erp.internal.example.com/api/v2"),
            Setting("RetryCount", "5"),
            Setting("WEBSITE_RUN_FROM_PACKAGE", "1"),
            KeyVaultSetting("ErpClientSecret", vault, "erp-client-secret"),
            KeyVaultSetting("SigningCertificatePassword", vault, "signing-cert-password")
        ];
    }

    /// <summary>Log rows for one function, spread backwards over <paramref name="lookback"/> from
    /// <paramref name="now"/> so the panel's range shortcuts change what is shown.</summary>
    public static List<LogEntryDetails> CreateLogEntries(string functionName, DateTimeOffset now, TimeSpan lookback)
    {
        var templates = LogTemplates(functionName);
        var entries = new List<LogEntryDetails>(templates.Count);

        // Spread the rows evenly across the window so a shorter range keeps the newest ones and a
        // longer one reaches further back.
        var step = lookback / (templates.Count + 1);
        for (var i = 0; i < templates.Count; i++)
        {
            var (itemType, severity, message) = templates[i];
            var timestamp = now - step * (i + 1);
            entries.Add(new LogEntryDetails
            {
                Timestamp = timestamp,
                ItemType = itemType,
                Severity = severity,
                Message = message,
                OperationId = OperationId(functionName, i),
                Key = $"demo:{functionName}:{i}"
            });
        }

        return entries;
    }

    private static List<(LogItemType ItemType, string Severity, string Message)> LogTemplates(string functionName) =>
    [
        (LogItemType.Request, "Information", $"{functionName} completed in 142 ms"),
        (LogItemType.Trace, "Information", "Message received, correlation 4f8c1d20-77ae-4c93-b6c2-0e1a5d9b3f47"),
        (LogItemType.Trace, "Information", "Mapped 3 order lines"),
        (LogItemType.Trace, "Information", "Sent to sb-integration-prd/orders-outbound"),
        (LogItemType.Request, "Information", $"{functionName} completed in 118 ms"),
        (LogItemType.Trace, "Warning", "Retry 1 of 5: upstream returned 429 Too Many Requests"),
        (LogItemType.Trace, "Information", "Retry succeeded after 2.4 s"),
        (LogItemType.Request, "Information", $"{functionName} completed in 2611 ms"),
        (LogItemType.Trace, "Warning", "Order 100482 has no matching customer, routed to review"),
        (LogItemType.Exception, "Error",
            "System.TimeoutException: The operation was canceled after 00:00:30 at ErpClient.PostOrderAsync"),
        (LogItemType.Trace, "Error", "Moving message to dead letter after 10 delivery attempts"),
        (LogItemType.Request, "Information", $"{functionName} completed in 96 ms"),
        (LogItemType.Trace, "Information", "Message received, correlation 91b7e0c4-2d5f-4a18-83be-6c40f2e7a915"),
        (LogItemType.Trace, "Information", "Mapped 1 order line"),
        (LogItemType.Request, "Information", $"{functionName} completed in 131 ms")
    ];

    private static FunctionAppDetails App(
        string name,
        string resourceGroup,
        string system,
        FunctionState state,
        List<FunctionDetails> functions,
        string subscriptionId = ProductionSubscriptionId,
        string environment = "Production",
        bool isPinned = false,
        List<string>? slots = null)
    {
        var armId =
            $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/Microsoft.Web/sites/{name}";

        foreach (var function in functions)
        {
            function.FunctionAppName = name;
        }

        return new FunctionAppDetails
        {
            Name = name,
            Id = armId,
            State = state,
            ResourceGroup = resourceGroup,
            Subscription = subscriptionId,
            IsPinned = isPinned,
            LastUpdated = DateTime.UtcNow,
            Tags = new Dictionary<string, string>
            {
                ["System"] = system,
                ["Environment"] = environment,
                ["Owner"] = "Integrations"
            },
            Functions = functions,
            Slots = (slots ?? [])
                .Select(slot => new FunctionAppSlotDetails
                {
                    Id = $"{armId}/slots/{slot}",
                    Name = slot,
                    FullName = $"{name}/{slot}",
                    State = FunctionState.Stopped
                })
                .ToList()
        };
    }

    private static FunctionDetails SbQueue(string name, string queue, long active, long deadLettered) =>
        new()
        {
            FunctionAppName = "",
            Name = name,
            Trigger = "ServiceBusTrigger",
            QueueName = queue,
            ConnectionSetting = "ServiceBusConnection",
            ActiveMessages = active,
            DeadLetteredMessages = deadLettered,
            CountStatus = ServiceBusCountStatus.Loaded
        };

    private static FunctionDetails SbTopic(string name, string topic, string subscription, long active,
        long deadLettered) =>
        new()
        {
            FunctionAppName = "",
            Name = name,
            Trigger = "ServiceBusTrigger",
            TopicName = topic,
            SubscriptionName = subscription,
            ConnectionSetting = "ServiceBusConnection",
            ActiveMessages = active,
            DeadLetteredMessages = deadLettered,
            CountStatus = ServiceBusCountStatus.Loaded
        };

    private static FunctionDetails Timer(string name, bool isDisabled = false) =>
        new() { FunctionAppName = "", Name = name, Trigger = "TimerTrigger", IsDisabled = isDisabled };

    private static FunctionDetails Http(string name) =>
        new() { FunctionAppName = "", Name = name, Trigger = "HttpTrigger" };

    private static FunctionDetails Blob(string name) =>
        new() { FunctionAppName = "", Name = name, Trigger = "BlobTrigger" };

    private static AppSettingDetails Setting(string name, string value) =>
        new() { Name = name, Value = value };

    private static AppSettingDetails KeyVaultSetting(string name, string vault, string secret) =>
        new()
        {
            Name = name,
            Value = $"@Microsoft.KeyVault(SecretUri=https://{vault}.vault.azure.net/secrets/{secret}/)",
            KeyVaultReference = new KeyVaultReference(vault, new Uri($"https://{vault}.vault.azure.net/"), secret,
                null)
        };

    // "func-orders-receiver-prd" -> "orders", used to keep vault and storage names in the same family.
    private static string Domain(string appName)
    {
        var parts = appName.Split('-');
        return parts.Length > 1 ? parts[1] : appName;
    }

    private static string OperationId(string functionName, int index)
    {
        // Stable per (function, row) so repeated renders show the same ids.
        var hash = Math.Abs(HashCode.Combine(functionName, index));
        return hash.ToString("x8") + "d4e1c07b9a3f6250";
    }
}
