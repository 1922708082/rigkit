using System.Text.Json;
using System.Text.Json.Serialization;

namespace RigKit.Infrastructure.Configuration;

public sealed record AppConfig(
    [property: JsonPropertyName("pollIntervalSeconds")] int PollIntervalSeconds = 1,
    [property: JsonPropertyName("theme")] string Theme = "light",
    [property: JsonPropertyName("historyCapacity")] int HistoryCapacity = 300)
{
    public AppConfig Normalize() => this with
    {
        PollIntervalSeconds = Math.Clamp(PollIntervalSeconds, 1, 10),
        Theme = string.IsNullOrWhiteSpace(Theme) ? "light" : Theme,
        HistoryCapacity = Math.Clamp(HistoryCapacity, 60, 3600)
    };
}

public sealed class JsonConfigStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public JsonConfigStore(string? filePath = null)
    {
        FilePath = string.IsNullOrWhiteSpace(filePath)
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "RigKit",
                "app.json")
            : filePath;
    }

    public string FilePath { get; }

    public AppConfig LoadOrCreate()
    {
        if (!File.Exists(FilePath))
        {
            var defaults = new AppConfig();
            Save(defaults);
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            return (JsonSerializer.Deserialize<AppConfig>(json, Options) ?? new AppConfig()).Normalize();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new AppConfig();
        }
    }

    public void Save(AppConfig config)
    {
        var directory = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var temporaryPath = FilePath + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(config.Normalize(), Options));
        File.Move(temporaryPath, FilePath, overwrite: true);
    }
}
