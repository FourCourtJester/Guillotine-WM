using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Guillotine.Core;
using Guillotine.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Guillotine.App;

/// <summary>
/// The flyout: a small always-on-top panel shown above the notification area. It hides when it loses
/// focus or on Esc, and closing it only hides it; the app keeps running in the tray.
/// </summary>
public partial class MainWindow : FluentWindow
{
    private const int HotkeyId = 0x4755; // "GU"

    private readonly SettingsStore _store;
    private readonly IntPtr _hwnd;
    private readonly GlobalHotkey _hotkey;
    private readonly DispatcherTimer _keepOnTop;

    private bool _allowClose;
    private DateTime _hiddenAt = DateTime.MinValue;
    private bool _capturingHotkey;

    // The window whose settings are shown, so a list refresh does not overwrite the user's edits.
    private IntPtr _placementWindow;

    public MainWindow(SettingsStore store)
    {
        _store = store;
        InitializeComponent();

        if (typeof(MainWindow).Assembly.GetName().Version is { } version)
            HeaderTitle.Text = $"Guillotine {version.ToString(3)}";
        AdminBadge.Visibility = StartupRegistration.IsElevated ? Visibility.Visible : Visibility.Collapsed;
        StartupHint.Text = StartupRegistration.IsElevated
            ? "Starts as administrator (scheduled task)"
            : "Starts without administrator rights";

        // The window handle exists before the flyout is first shown, so the hotkey works from --tray.
        _hwnd = new WindowInteropHelper(this).EnsureHandle();
        HwndSource.FromHwnd(_hwnd).AddHook(WndProc);
        _hotkey = new GlobalHotkey(_hwnd, HotkeyId);
        RegisterHotkeyFromSettings();

        // While open, stay above everything, including games that keep making themselves topmost.
        _keepOnTop = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _keepOnTop.Tick += (_, _) => RaiseToTop();

        ShowPlacement(_store.Settings.PlacementFor(null));
        StartupSwitch.IsChecked = StartupRegistration.IsEnabled;
    }

    // ---- Showing and hiding -------------------------------------------------------------------

    public void ToggleFlyout()
    {
        if (IsVisible)
            HideFlyout();
        else if (DateTime.UtcNow - _hiddenAt > TimeSpan.FromMilliseconds(300))
            ShowFlyout(); // A click on the tray icon first hides the open flyout (focus moves away); do not reopen it.
    }

    public void ShowFlyout()
    {
        if (!IsVisible)
            Show();
        UpdateLayout();
        PlaceAboveTray();
        Activate();
        RaiseToTop();
        _keepOnTop.Start();
    }

    public void HideFlyout()
    {
        CancelHotkeyCapture();
        _keepOnTop.Stop();
        _hiddenAt = DateTime.UtcNow;
        Hide();
    }

    public void AllowClose() => _allowClose = true;

    public void ReleaseHotkey() => _hotkey.Dispose();

    private void PlaceAboveTray()
    {
        var dpi = VisualTreeHelper.GetDpi(this);
        var size = new PixelSize((int)Math.Ceiling(ActualWidth * dpi.DpiScaleX), (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY));
        int margin = (int)Math.Round(12 * dpi.DpiScaleX);

        var taskbar = Taskbar.Get();
        var rect = taskbar is not null
            ? FlyoutPlacement.Compute(taskbar.Monitor.Bounds, taskbar.Edge, taskbar.Thickness, size, margin)
            : FlyoutPlacement.Compute(Monitors.ForWindow(_hwnd).Bounds, TaskbarEdge.Bottom, 0, size, margin);
        WindowController.MoveWindow(_hwnd, rect.Left, rect.Top, topmost: true);
    }

    private void RaiseToTop()
    {
        if (IsVisible)
            WindowController.SetTopmost(_hwnd, true); // re-enters the top of the topmost band without moving or activating
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SystemThemeWatcher.Watch(this);
        if (_store.LoadError is not null)
            ShowStatus(InfoBarSeverity.Error, "settings.json was not loaded", _store.LoadError);
    }

    // Pick up windows that opened while the flyout was hidden.
    private void OnActivated(object? sender, EventArgs e) => RefreshWindows();

    private void OnDeactivated(object? sender, EventArgs e)
    {
        if (IsVisible && !_allowClose)
            HideFlyout();
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_allowClose)
            return;
        e.Cancel = true;
        HideFlyout();
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (IsVisible)
            PlaceAboveTray();
    }

    private void OnHideClicked(object sender, RoutedEventArgs e) => HideFlyout();

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && !_capturingHotkey)
        {
            HideFlyout();
            e.Handled = true;
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (_hotkey.IsHotkeyMessage(msg, wParam))
        {
            ToggleFlyout();
            handled = true;
        }
        return IntPtr.Zero;
    }

    // ---- Target window ------------------------------------------------------------------------

    private void OnRefreshClicked(object sender, RoutedEventArgs e) => RefreshWindows();

    private void OnWindowSelected(object sender, SelectionChangedEventArgs e)
    {
        if (WindowList.SelectedItem is not WindowInfo window)
        {
            WindowDetails.Text = "";
            return;
        }

        if (window.Handle != _placementWindow)
        {
            _placementWindow = window.Handle;
            ShowPlacement(_store.Settings.PlacementFor(_store.Settings.FindGame(window.Title, window.ProcessName)));
        }

        ShowDetails(window);
    }

    private void RefreshWindows()
    {
        var selected = (WindowList.SelectedItem as WindowInfo)?.Handle;
        var windows = WindowController.GetWindows();
        WindowList.ItemsSource = windows;

        // Keep the current selection; otherwise pick the running window that best matches a configured game.
        WindowList.SelectedItem =
            windows.FirstOrDefault(w => w.Handle == selected)
            ?? windows.Where(w => _store.Settings.MatchQuality(w.Title, w.ProcessName) > 0)
                      .OrderByDescending(w => _store.Settings.MatchQuality(w.Title, w.ProcessName))
                      .FirstOrDefault();
    }

    /// <summary>Shows the window's size and mirrors its live borderless and topmost state in the switches.</summary>
    private void ShowDetails(WindowInfo window)
    {
        var game = _store.Settings.FindGame(window.Title, window.ProcessName);
        WindowDetails.Text = $"Client {window.Client}" + (game is not null ? $" · matches \"{game.Match}\" in settings" : "");
        BorderlessSwitch.IsChecked = window.IsBorderless;
        TopmostSwitch.IsChecked = window.IsTopmost;
    }

    // ---- Size, zone, apply ----------------------------------------------------------------------

    private void OnPresetClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string text } && PixelSize.TryParse(text, out var size))
        {
            WidthBox.Value = size.Width;
            HeightBox.Value = size.Height;
        }
    }

    /// <summary>Loads the size, zone and taskbar option. Borderless and topmost always show the window's real state.</summary>
    private void ShowPlacement(Placement placement)
    {
        WidthBox.Value = placement.Size.Width;
        HeightBox.Value = placement.Size.Height;
        WorkAreaSwitch.IsChecked = placement.UseWorkArea;

        foreach (var button in ZoneGrid.Children.OfType<RadioButton>())
            button.IsChecked = Zone.TryParse(button.Tag as string, out var zone) && zone == placement.Zone;
    }

    private Placement? ReadPlacement()
    {
        if (WidthBox.Value is not >= 1 || HeightBox.Value is not >= 1)
        {
            ShowStatus(InfoBarSeverity.Error, "Size needed", "Enter a width and height of at least 1 pixel.");
            return null;
        }

        var zoneButton = ZoneGrid.Children.OfType<RadioButton>().FirstOrDefault(b => b.IsChecked == true);
        var zone = zoneButton is not null ? Zone.Parse((string)zoneButton.Tag) : Zone.Center;

        return new Placement(
            new PixelSize((int)WidthBox.Value.Value, (int)HeightBox.Value.Value),
            zone,
            BorderlessSwitch.IsChecked == true,
            TopmostSwitch.IsChecked == true,
            WorkAreaSwitch.IsChecked == true);
    }

    private void OnApplyClicked(object sender, RoutedEventArgs e)
    {
        if (SelectedWindow() is not { } window || ReadPlacement() is not { } placement)
            return;

        Run(() =>
        {
            // Settings that cannot work together are explained instead of applied.
            var monitor = Monitors.ForWindow(window.Handle);
            if (PlacementDiagnostics.CheckFits(monitor.Bounds, monitor.WorkArea, placement.Size, placement.UseWorkArea) is { } fitProblem)
            {
                ShowStatus(InfoBarSeverity.Warning, "Size does not fit", fitProblem);
                return;
            }

            var result = WindowController.Apply(window.Handle, placement);
            if (result.IsExact)
                ShowStatus(InfoBarSeverity.Success, "Applied",
                    $"Client area is exactly {result.Actual}." +
                    (result.Forced ? " The game refused this size at first, so its size limit was bypassed." : ""));
            else if (result.Fallback is not null && result.Actual == result.Fallback)
                ShowStatus(InfoBarSeverity.Warning, $"The game chose {result.Actual.Size}", result.Problem!);
            else
                ShowStatus(InfoBarSeverity.Warning, "Client area does not match", result.Problem!);
        });
    }

    private void OnRestoreClicked(object sender, RoutedEventArgs e)
    {
        if (SelectedWindow() is not { } window)
            return;

        Run(() =>
        {
            var restored = WindowController.Restore(window.Handle);
            ShowStatus(InfoBarSeverity.Informational, "Restored", $"Frame is back and always on top is off; window is {restored.Bounds}.");
        });
    }

    // ---- Immediate switches -------------------------------------------------------------------

    private void OnBorderlessClicked(object sender, RoutedEventArgs e)
    {
        bool borderless = BorderlessSwitch.IsChecked == true;
        if (SelectedWindow() is not { } window)
        {
            BorderlessSwitch.IsChecked = !borderless;
            return;
        }

        Run(() =>
        {
            var info = WindowController.SetBorderlessKeepingClient(window.Handle, borderless);
            ShowStatus(InfoBarSeverity.Informational, borderless ? "Borderless" : "Frame restored",
                $"Client area is {info.Client}.");
        });
    }

    private void OnTopmostClicked(object sender, RoutedEventArgs e)
    {
        bool topmost = TopmostSwitch.IsChecked == true;
        if (SelectedWindow() is not { } window)
        {
            TopmostSwitch.IsChecked = !topmost;
            return;
        }

        Run(() =>
        {
            WindowController.SetTopmost(window.Handle, topmost);
            ShowStatus(InfoBarSeverity.Informational, topmost ? "Always on top" : "No longer always on top",
                $"{window.ProcessName}: \"{window.Title}\"");
        });
    }

    // ---- Hotkey -------------------------------------------------------------------------------

    private void RegisterHotkeyFromSettings()
    {
        var text = _store.Settings.Hotkey;
        if (string.IsNullOrEmpty(text))
        {
            ShowHotkey();
            return;
        }

        if (Hotkey.TryParse(text, out var hotkey) && TryRegister(hotkey, out var error))
        {
            ShowHotkey();
            return;
        }

        ShowHotkey();
        ShowStatus(InfoBarSeverity.Warning, "Hotkey not active", error ?? $"'{text}' is not a valid hotkey.");
    }

    private bool TryRegister(Hotkey hotkey, out string? error)
    {
        if (!HotkeyKeys.TryGetVirtualKey(hotkey.Key, out uint vk))
        {
            error = $"'{hotkey.Key}' is not a key Guillotine can use.";
            return false;
        }
        return _hotkey.TryRegister(hotkey, vk, out error);
    }

    private void ShowHotkey() => HotkeyButton.Content = _hotkey.Current?.ToString() ?? "Off";

    private void OnHotkeyClicked(object sender, RoutedEventArgs e)
    {
        if (_capturingHotkey)
            return;
        _capturingHotkey = true;
        _hotkey.Unregister(); // so pressing the current combination is captured, not acted on
        HotkeyButton.Content = "Press keys…";
        HotkeyButton.Focus();
    }

    private void OnHotkeyPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!_capturingHotkey)
            return;
        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
        {
            CancelHotkeyCapture();
            return;
        }
        if (key is Key.Back or Key.Delete)
        {
            _capturingHotkey = false;
            SaveHotkey(null);
            ShowStatus(InfoBarSeverity.Informational, "Hotkey off", "Use the tray icon to open Guillotine.");
            return;
        }

        if (HotkeyKeys.NameFor(key) is not { } name)
            return; // only a modifier so far; wait for the key

        var hotkey = new Hotkey(HotkeyKeys.ToModifiers(Keyboard.Modifiers), name);
        if (!hotkey.IsUsable)
        {
            HotkeyButton.Content = "Add Ctrl, Alt or Win…";
            return;
        }

        _capturingHotkey = false;
        if (TryRegister(hotkey, out var error))
        {
            SaveHotkey(hotkey);
            ShowStatus(InfoBarSeverity.Success, "Hotkey set", $"{hotkey} shows or hides Guillotine.");
        }
        else
        {
            RegisterHotkeyFromSettings();
            ShowStatus(InfoBarSeverity.Warning, "Hotkey not changed", error!);
        }
    }

    private void OnHotkeyLostFocus(object sender, KeyboardFocusChangedEventArgs e) => CancelHotkeyCapture();

    private void CancelHotkeyCapture()
    {
        if (!_capturingHotkey)
            return;
        _capturingHotkey = false;
        RegisterHotkeyFromSettings();
    }

    private void SaveHotkey(Hotkey? hotkey)
    {
        if (hotkey is null)
            _hotkey.Unregister();
        ShowHotkey();
        if (_store.Save("hotkey", JsonValue.Create(hotkey?.ToString() ?? "")) is { } error)
            ShowStatus(InfoBarSeverity.Warning, "Hotkey not saved", error);
    }

    // ---- Start with Windows -------------------------------------------------------------------

    private void OnStartupClicked(object sender, RoutedEventArgs e)
    {
        bool enable = StartupSwitch.IsChecked == true;
        var error = Environment.ProcessPath is { } exe
            ? StartupRegistration.Set(enable, exe)
            : "Could not find Guillotine.exe.";

        StartupSwitch.IsChecked = StartupRegistration.IsEnabled;
        if (error is not null)
            ShowStatus(InfoBarSeverity.Error, "Start with Windows not changed", error);
        else
            ShowStatus(InfoBarSeverity.Informational, "Start with Windows", StartupRegistration.Describe());
    }

    // ---- Helpers ------------------------------------------------------------------------------

    private WindowInfo? SelectedWindow()
    {
        if (WindowList.SelectedItem is WindowInfo window)
            return window;
        ShowStatus(InfoBarSeverity.Error, "No window selected", "Pick a window from the list first.");
        return null;
    }

    /// <summary>Runs a window operation, reports failures, refreshes, and puts the flyout back on top.</summary>
    private void Run(Action action)
    {
        try
        {
            action();
        }
        catch (WindowOperationException ex)
        {
            ShowStatus(InfoBarSeverity.Error, "Could not change the window", ex.Message);
        }
        RefreshWindows();
        RaiseToTop();
    }

    private void ShowStatus(InfoBarSeverity severity, string title, string message)
    {
        Status.Severity = severity;
        Status.Title = title;
        Status.Message = message;
        Status.IsOpen = true;
    }
}
