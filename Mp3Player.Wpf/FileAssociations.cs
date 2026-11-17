using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Mp3Player.Core;

namespace Mp3Player.Wpf;

/// <summary>
/// Lists the player under "Open with" in Explorer for every audio file type it plays, for the current user only
/// (no admin rights). It does not take over as the default app: Windows only lets the user choose that.
/// Called from the installer hooks in <see cref="Program"/>; Windows' own "Choose another app" also works without it,
/// because the app accepts a file path on the command line.
/// </summary>
internal static class FileAssociations
{
    private const string Classes = @"Software\Classes\";
    private static readonly string ExeName = Path.GetFileName(Environment.ProcessPath ?? "Mp3PlayerWpf.exe");

    /// <summary>One ProgID per type ("Mp3Player.m4a"), so Explorer can name the type of each file correctly.</summary>
    private static string ProgId(string extension) => "Mp3Player" + extension;

    public static void Register()
    {
        string? exe = Environment.ProcessPath;
        if (exe == null) return;
        string command = $"\"{exe}\" \"%1\"";
        string icon = $"\"{exe}\",0";

        foreach (string ext in MusicPlayer.SupportedExtensions)
        {
            string typeName = ext.TrimStart('.').ToUpperInvariant() + " audio file";
            using (var progId = Registry.CurrentUser.CreateSubKey(Classes + ProgId(ext)))
            {
                progId.SetValue("", typeName);
                progId.SetValue("FriendlyTypeName", typeName);
                using (var defaultIcon = progId.CreateSubKey("DefaultIcon")) defaultIcon.SetValue("", icon);
                using (var open = progId.CreateSubKey(@"shell\open"))
                {
                    open.SetValue("FriendlyAppName", AppInfo.Name);
                    using var cmd = open.CreateSubKey("command");
                    cmd.SetValue("", command);
                }
            }

            using var openWith = Registry.CurrentUser.CreateSubKey(Classes + ext + @"\OpenWithProgids");
            openWith.SetValue(ProgId(ext), Array.Empty<byte>(), RegistryValueKind.None);
        }

        // The name and icon Windows shows for the app in the "Open with" list.
        using (var app = Registry.CurrentUser.CreateSubKey(Classes + @"Applications\" + ExeName))
        {
            app.SetValue("FriendlyAppName", AppInfo.Name);
            using (var defaultIcon = app.CreateSubKey("DefaultIcon")) defaultIcon.SetValue("", icon);
            using (var cmd = app.CreateSubKey(@"shell\open\command")) cmd.SetValue("", command);
            using var types = app.CreateSubKey("SupportedTypes");
            foreach (string ext in MusicPlayer.SupportedExtensions) types.SetValue(ext, "");
        }

        NotifyShell();
    }

    public static void Unregister()
    {
        foreach (string ext in MusicPlayer.SupportedExtensions)
        {
            Registry.CurrentUser.DeleteSubKeyTree(Classes + ProgId(ext), throwOnMissingSubKey: false);
            using (var openWith = Registry.CurrentUser.OpenSubKey(Classes + ext + @"\OpenWithProgids", writable: true))
                openWith?.DeleteValue(ProgId(ext), throwOnMissingValue: false);

            // Register created these keys if they weren't there; don't leave them behind empty.
            DeleteIfEmpty(Classes + ext + @"\OpenWithProgids");
            DeleteIfEmpty(Classes + ext);
        }
        Registry.CurrentUser.DeleteSubKeyTree(Classes + @"Applications\" + ExeName, throwOnMissingSubKey: false);
        NotifyShell();
    }

    private static void DeleteIfEmpty(string subKey)
    {
        bool empty;
        using (var key = Registry.CurrentUser.OpenSubKey(subKey))
            empty = key != null && key.ValueCount == 0 && key.SubKeyCount == 0;
        if (empty) Registry.CurrentUser.DeleteSubKey(subKey, throwOnMissingSubKey: false);
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
