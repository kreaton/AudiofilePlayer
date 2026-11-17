using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;

namespace Mp3Player.Wpf;

/// <summary>
/// Keeps "Open with" (or a double-click on a music file) from starting a second player next to the one already playing:
/// the first instance listens on a named pipe, and a later launch with a path hands the path over and exits.
/// Scoped to the current user and Windows session; the pipe only accepts connections from the same user.
/// </summary>
internal sealed class SingleInstance : IDisposable
{
    private static readonly string Name = BuildName();

    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _stop = new();

    private SingleInstance(Mutex mutex) => _mutex = mutex;

    private static string BuildName()
    {
        using var process = Process.GetCurrentProcess();
        string user = WindowsIdentity.GetCurrent().User?.Value ?? Environment.UserName;
        return $"Mp3Player.{user}.{process.SessionId}";
    }

    /// <summary>Returns the claim if this is the first running instance, or null if another one is already running.</summary>
    public static SingleInstance? Claim()
    {
        var mutex = new Mutex(initiallyOwned: false, @"Local\" + Name, out bool createdNew);
        if (createdNew) return new SingleInstance(mutex);
        mutex.Dispose();
        return null;
    }

    /// <summary>Sends the path to the running instance. False if it could not be reached (then just start normally).</summary>
    public static bool TryHandOver(string path)
    {
        try
        {
            using var pipe = new NamedPipeClientStream(".", Name, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            // The other instance may itself still be starting up and not listening yet.
            pipe.Connect(TimeSpan.FromSeconds(5));

            // Windows only lets the running player come to the front if this (just launched) process passes its right on.
            if (GetNamedPipeServerProcessId(pipe.SafePipeHandle, out uint serverPid))
                AllowSetForegroundWindow(serverPid);

            pipe.Write(Encoding.UTF8.GetBytes(path));
            pipe.WaitForPipeDrain();
            return true;
        }
        catch (Exception)
        {
            return false;      // timed out, or the other instance closed meanwhile
        }
    }

    /// <summary>Calls <paramref name="onPath"/> (on a worker thread) for every path a later launch hands over.</summary>
    public void Listen(Action<string> onPath)
    {
        CancellationToken token = _stop.Token;
        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await using var pipe = new NamedPipeServerStream(Name, PipeDirection.In, 1,
                        PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                    await pipe.WaitForConnectionAsync(token);
                    using var reader = new StreamReader(pipe, Encoding.UTF8);
                    string path = (await reader.ReadToEndAsync(token)).Trim();
                    if (path.Length > 0) onPath(path);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception)
                {
                    // A client that hung up halfway, or the pipe could not be created: try again shortly.
                    try { await Task.Delay(500, token); }
                    catch (OperationCanceledException) { return; }
                }
            }
        }, token);
    }

    public void Dispose()
    {
        _stop.Cancel();
        _mutex.Dispose();
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNamedPipeServerProcessId(SafeHandle pipe, out uint serverProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(uint processId);
}
