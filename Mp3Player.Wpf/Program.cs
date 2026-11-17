using System.IO;
using Velopack;

namespace Mp3Player.Wpf;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // Must be the first thing to run: handles Velopack's install/update/uninstall hooks
        // and may exit the process. Don't swallow exceptions here.
        // The hooks add the player to Explorer's "Open with" list for MP3 files, and remove it again.
        VelopackApp.Build()
            .OnAfterInstallFastCallback(_ => FileAssociations.TryRun(FileAssociations.Register))
            .OnAfterUpdateFastCallback(_ => FileAssociations.TryRun(FileAssociations.Register))
            .OnBeforeUninstallFastCallback(_ => FileAssociations.TryRun(FileAssociations.Unregister))
            .Run();

        // Optional: a folder, or a file ("Open with"), passed on the command line is opened right away.
        string? startPath = args.Select(TryGetFullPath)
            .FirstOrDefault(p => p != null && (File.Exists(p) || Directory.Exists(p)));

        // If the player is already running, give it the path instead of starting a second one.
        using SingleInstance? instance = SingleInstance.Claim();
        if (instance == null && startPath != null && SingleInstance.TryHandOver(startPath))
            return;

        var app = new App { StartPath = startPath, Instance = instance };
        app.InitializeComponent();
        app.Run();
    }

    private static string? TryGetFullPath(string arg)
    {
        try { return Path.GetFullPath(arg); }
        catch (Exception) { return null; }     // not a path (an option, or invalid characters)
    }
}
