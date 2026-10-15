using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using Guillotine.Windows;

namespace Guillotine.App;

/// <summary>
/// A tray app: one instance per user session, an icon in the notification area, and a flyout that
/// opens from the icon or the global hotkey. Started with --tray (as at sign-in) it stays hidden.
/// </summary>
public partial class App : Application
{
    private const string InstanceName = @"Local\Guillotine.App";
    private const string ShowEventName = @"Local\Guillotine.App.Show";

    private static readonly string CrashLog = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Guillotine", "crash.log");

    private Mutex? _instance;
    private EventWaitHandle? _showEvent;
    private RegisteredWaitHandle? _showWait;
    private TrayIcon? _tray;
    private MainWindow? _flyout;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnUnhandledException;

        if (!TryBecomeTheOnlyInstance())
        {
            Shutdown();
            return;
        }

        var store = new SettingsStore();
        store.Load();

        _flyout = new MainWindow(store);
        MainWindow = _flyout;
        _tray = new TrayIcon(toggle: _flyout.ToggleFlyout, open: _flyout.ShowFlyout, exit: ExitApp);

        // A second launch signals this event instead of starting another copy.
        _showWait = ThreadPool.RegisterWaitForSingleObject(_showEvent!, (_, _) =>
            Dispatcher.BeginInvoke(() => _flyout.ShowFlyout()), null, Timeout.Infinite, executeOnlyOnce: false);

        if (Environment.ProcessPath is { } exe)
            StartupRegistration.Refresh(exe);

        if (!e.Args.Contains(StartupRegistration.TrayArgument, StringComparer.OrdinalIgnoreCase))
            _flyout.ShowFlyout();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _showWait?.Unregister(null);
        _tray?.Dispose();
        _flyout?.ReleaseHotkey();
        _showEvent?.Dispose();
        _instance?.Dispose();
        base.OnExit(e);
    }

    private void ExitApp()
    {
        _flyout?.AllowClose();
        Shutdown();
    }

    private bool TryBecomeTheOnlyInstance()
    {
        try
        {
            _instance = new Mutex(initiallyOwned: true, InstanceName, out bool createdNew);
            if (createdNew)
            {
                _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
                return true;
            }

            using var existing = EventWaitHandle.OpenExisting(ShowEventName);
            existing.Set();
            return false;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or WaitHandleCannotBeOpenedException)
        {
            // The running copy was started as administrator, and this one cannot signal it.
            MessageBox.Show(
                "Guillotine is already running as administrator. Use its icon in the notification area, or press its hotkey.",
                "Guillotine", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(CrashLog)!);
            File.AppendAllText(CrashLog, $"[{DateTime.Now:u}] {e.Exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch (IOException)
        {
        }

        MessageBox.Show($"Something went wrong:\n\n{e.Exception.Message}\n\nDetails were saved to {CrashLog}.",
            "Guillotine", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
