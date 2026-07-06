namespace Funcy.Infrastructure.Azure;

// Parses the InstrumentationKey out of an APPLICATIONINSIGHTS_CONNECTION_STRING value, which is
// a semicolon separated list of key=value pairs (e.g. "InstrumentationKey=<guid>;IngestionEndpoint=...").
public static class AppInsightsConnectionString
{
    public static string? ParseInstrumentationKey(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separator = part.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = part[..separator].Trim();
            if (key.Equals("InstrumentationKey", StringComparison.OrdinalIgnoreCase))
            {
                var value = part[(separator + 1)..].Trim();
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }

        return null;
    }
}
