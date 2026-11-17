using System.Windows;
using System.Windows.Threading;

namespace Mp3Player.Wpf;

internal partial class App : Application
{
    /// <summary>A folder or file from the command line, opened (and played) right away; null opens the saved folder.</summary>
    public string? StartPath { get; init; }

    /// <summary>Set when this is the first running instance: later launches hand their path to this window.</summary>
    public SingleInstance? Instance { get; init; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // MusicPlayer raises its events through the SynchronizationContext current when it is created.
        SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher));

        var window = new MainWindow(StartPath);
        window.Show();
        Instance?.Listen(path => Dispatcher.BeginInvoke(() => window.OpenFromOtherInstance(path)));
    }
}
