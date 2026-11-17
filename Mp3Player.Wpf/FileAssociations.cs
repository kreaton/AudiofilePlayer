using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Mp3Player.Wpf;

/// <summary>
/// Lists the player under "Open with" for .mp3 files in Explorer, for the current user only (no admin rights).
/// It does not take over as the default app: Windows only lets the user choose that.
/// Called from the installer hooks in <see cref="Program"/>; Windows' own "Choose another app" also works without it,
/// because the app accepts a file path on the command line.
/// </summary>
internal static class FileAssociations
{
    private const string Classes = @"Software\Classes\";
    private const string ProgId = "Mp3Player.mp3";
    private const string Extension = ".mp3";
    private static readonly string ExeName = Path.GetFileName(Environment.ProcessPath ?? "Mp3PlayerWpf.exe");

    public static void Register()
    {
        string? exe = Environment.ProcessPath;
        if (exe == null) return;
        string command = $"\"{exe}\" \"%1\"";
        string icon = $"\"{exe}\",0";

        using (var progId = Registry.CurrentUser.CreateSubKey(Classes + ProgId))
        {
            progId.SetValue("", "MP3 audio file");
            progId.SetValue("FriendlyTypeName", "MP3 audio file");
            using (var defaultIcon = progId.CreateSubKey("DefaultIcon")) defaultIcon.SetValue("", icon);
            using (var open = progId.CreateSubKey(@"shell\open"))
            {
                open.SetValue("FriendlyAppName", AppInfo.Name);
                using var cmd = open.CreateSubKey("command");
                cmd.SetValue("", command);
            }
        }

        using (var openWith = Registry.CurrentUser.CreateSubKey(Classes + Extension + @"\OpenWithProgids"))
            openWith.SetValue(ProgId, Array.Empty<byte>(), RegistryValueKind.None);

        // The name and icon Windows shows for the app in the "Open with" list.
        using (var app = Registry.CurrentUser.CreateSubKey(Classes + @"Applications\" + ExeName))
        {
            app.SetValue("FriendlyAppName", AppInfo.Name);
            using (var defaultIcon = app.CreateSubKey("DefaultIcon")) defaultIcon.SetValue("", icon);
            using (var cmd = app.CreateSubKey(@"shell\open\command")) cmd.SetValue("", command);
            using (var types = app.CreateSubKey("SupportedTypes")) types.SetValue(Extension, "");
        }

        NotifyShell();
    }

    public static void Unregister()
    {
        Registry.CurrentUser.DeleteSubKeyTree(Classes + ProgId, throwOnMissingSubKey: false);
        Registry.CurrentUser.DeleteSubKeyTree(Classes + @"Applications\" + ExeName, throwOnMissingSubKey: false);
        using (var openWith = Registry.CurrentUser.OpenSubKey(Classes + Extension + @"\OpenWithProgids", writable: true))
            openWith?.DeleteValue(ProgId, throwOnMissingValue: false);
        NotifyShell();
    }

    /// <summary>Runs a registration step from an installer hook, where a failure must never break the install.</summary>
    public static void TryRun(Action action)
    {
        try { action(); }
        catch (Exception) { /* the player still works; only the "Open with" entry is missing */ }
    }

    private static void NotifyShell() => SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST, IntPtr.Zero, IntPtr.Zero);

    private const int SHCNE_ASSOCCHANGED = 0x08000000;
    private const uint SHCNF_IDLIST = 0;

    [DllImport("shell32.dll")]
    private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);
}
