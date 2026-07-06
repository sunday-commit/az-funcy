using System.Text.Json;
using System.Text.Json.Nodes;

namespace Funcy.Console.Settings;

// Persists the "Funcy" section of settings.json. Reads the existing file (if any), replaces
// only the Funcy object and writes the whole document back indented, so unrelated top-level
// sections (e.g. Serilog) are preserved. Writes to a temp file then moves it into place.
public static class FuncySettingsFile
{
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true };

    public static void Write(string path, FuncySettings settings)
    {
        JsonObject root;
        if (File.Exists(path))
        {
            var existing = File.ReadAllText(path);
            root = JsonNode.Parse(existing) as JsonObject ?? new JsonObject();
        }
        else
        {
            root = new JsonObject();
        }

        root["Funcy"] = JsonSerializer.SerializeToNode(settings);

        var json = root.ToJsonString(WriteOptions);
        var tempPath = path + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, path, overwrite: true);
    }
}
