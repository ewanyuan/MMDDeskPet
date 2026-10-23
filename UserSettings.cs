using System.IO;
using System.Text.Json;

namespace MMDDeskPet;

internal sealed class UserSettings
{
    public List<string> RecentModels { get; set; } = [];

    public List<string> RecentMotions { get; set; } = [];

    public List<string> RecentMusic { get; set; } = [];

    public string? LastModelPath { get; set; }

    public string? LastMotionPath { get; set; }

    public string? LastMusicPath { get; set; }

    public string? LibraryFolder { get; set; }

    public bool AdultContentEnabled { get; set; }
}

internal static class UserSettingsStore
{
    private const int RecentLimit = 8;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    internal static string SettingsFilePath =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MMDDeskPet",
            "settings.json");

    internal static UserSettings Load()
    {
        try
        {
            var path = SettingsFilePath;
            if (!File.Exists(path))
            {
                return new UserSettings();
            }

            var json = File.ReadAllText(path);
            var settings = JsonSerializer.Deserialize<UserSettings>(json, JsonOptions) ?? new UserSettings();
            settings.RecentModels ??= [];
            settings.RecentMotions ??= [];
            settings.RecentMusic ??= [];
            return settings;
        }
        catch (IOException)
        {
            return new UserSettings();
        }
        catch (JsonException)
        {
            return new UserSettings();
        }
        catch (UnauthorizedAccessException)
        {
            return new UserSettings();
        }
    }

    internal static void Save(UserSettings settings)
    {
        try
        {
            var path = SettingsFilePath;
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(path, json);
        }
        catch (IOException)
        {
            // A settings write must not take down the pet.
        }
        catch (UnauthorizedAccessException)
        {
            // A settings write must not take down the pet.
        }
    }

    internal static void Remember(List<string> paths, string path)
    {
        var fullPath = Path.GetFullPath(path);
        paths.RemoveAll(item => string.Equals(item, fullPath, StringComparison.OrdinalIgnoreCase));
        paths.Insert(0, fullPath);
        if (paths.Count > RecentLimit)
        {
            paths.RemoveRange(RecentLimit, paths.Count - RecentLimit);
        }
    }
}
