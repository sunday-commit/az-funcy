using System.Text.Json;
using Funcy.Console.Settings;
using Xunit;

namespace Funcy.Tests.Settings;

public class FuncySettingsFileTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"funcy-settings-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    private static FuncySettings Sample() => new()
    {
        TagColumns = ["System", "Team"],
        SubscriptionRefreshIntervalMinutes = 30,
        DefaultTagColumnWidth = 25,
        TagColumnWidths = new Dictionary<string, int> { ["System"] = 40 }
    };

    [Fact]
    public void Write_NewFile_ProducesValidJsonWithFuncySection()
    {
        FuncySettingsFile.Write(_path, Sample());

        using var doc = JsonDocument.Parse(File.ReadAllText(_path));
        var funcy = doc.RootElement.GetProperty("Funcy");
        Assert.Equal(30, funcy.GetProperty("SubscriptionRefreshIntervalMinutes").GetInt32());
        Assert.Equal("System", funcy.GetProperty("TagColumns")[0].GetString());
        Assert.Equal(40, funcy.GetProperty("TagColumnWidths").GetProperty("System").GetInt32());
    }

    [Fact]
    public void Write_ExistingFileWithForeignSection_PreservesForeignSection()
    {
        File.WriteAllText(_path,
            """
            {
              "Serilog": { "MinimumLevel": "Information" },
              "Funcy": { "SubscriptionRefreshIntervalMinutes": 10 }
            }
            """);

        FuncySettingsFile.Write(_path, Sample());

        using var doc = JsonDocument.Parse(File.ReadAllText(_path));
        Assert.Equal("Information",
            doc.RootElement.GetProperty("Serilog").GetProperty("MinimumLevel").GetString());
        Assert.Equal(30,
            doc.RootElement.GetProperty("Funcy").GetProperty("SubscriptionRefreshIntervalMinutes").GetInt32());
    }

    [Fact]
    public void Write_Twice_ReplacesFuncySectionWithoutDuplicating()
    {
        FuncySettingsFile.Write(_path, Sample());
        var updated = Sample();
        updated.SubscriptionRefreshIntervalMinutes = 99;
        FuncySettingsFile.Write(_path, updated);

        using var doc = JsonDocument.Parse(File.ReadAllText(_path));
        Assert.Equal(99,
            doc.RootElement.GetProperty("Funcy").GetProperty("SubscriptionRefreshIntervalMinutes").GetInt32());
    }
}
