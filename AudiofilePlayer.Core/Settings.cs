using System.Text.Json;

namespace AudiofilePlayer.Core;

/// <summary>
/// Remembers the last folder and options between runs (%AppData%\AudiofilePlayer\settings.json).
/// Shared by every UI, so switching between them keeps your folder, volume and options.
/// </summary>
public sealed class Settings
{
    public string? Folder { get; set; }
    public int Volume { get; set; } = 80;
    public bool Shuffle { get; set; }
    public bool Crossfade { get; set; }
    public int CrossfadeSeconds { get; set; } = 4;

    /// <summary>Show a Windows notification when a new track starts while the player window is in the background.</summary>
    public bool TrackNotifications { get; set; }

    /// <summary>Delete the current track right away, without asking first (folders are always confirmed).</summary>
    public bool DeleteTrackWithoutConfirmation { get; set; }

    private static string FilePathIn(string appFolder) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), appFolder, "settings.json");

    private static string FilePath => FilePathIn("AudiofilePlayer");

    /// <summary>Where the settings were kept when the app was called MP3 Player; read once, until the first save.</summary>
    private static string LegacyFilePath => FilePathIn("Mp3Player");

    public static Settings Load()
    {
        try
        {
            string path = File.Exists(FilePath) ? FilePath : LegacyFilePath;
            if (File.Exists(path))
            {
                var s = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) ?? new Settings();
                s.Volume = Math.Clamp(s.Volume, 0, 100);
                s.CrossfadeSeconds = Math.Clamp(s.CrossfadeSeconds, 1, 18);
                return s;
            }
        }
        catch
        {
            // Corrupt or unreadable settings: fall back to defaults.
        }
        return new Settings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Not being able to save settings is not worth bothering the user about.
        }
    }
}
