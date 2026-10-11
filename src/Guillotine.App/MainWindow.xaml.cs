using System.IO;
using System.Windows;
using System.Windows.Controls;
using Guillotine.Core;
using Guillotine.Windows;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace Guillotine.App;

public partial class MainWindow : FluentWindow
{
    private GuillotineSettings _settings = new();
    private string? _settingsError;

    // The window whose settings are shown, so a list refresh does not overwrite the user's edits.
    private IntPtr _placementWindow;

    public MainWindow()
    {
        InitializeComponent();
        LoadSettings();
        ShowPlacement(_settings.PlacementFor(null));
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        SystemThemeWatcher.Watch(this);
        if (_settingsError is not null)
            ShowStatus(InfoBarSeverity.Error, "settings.json was not loaded", _settingsError);
        RefreshWindows();
    }

    // Pick up windows that opened while Guillotine was in the background.
    private void OnActivated(object? sender, EventArgs e) => RefreshWindows();

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
            ShowPlacement(_settings.PlacementFor(_settings.FindGame(window.Title, window.ProcessName)));
        }

        ShowDetails(window);
    }

    private void OnPresetClicked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: string text } && PixelSize.TryParse(text, out var size))
        {
            WidthBox.Value = size.Width;
            HeightBox.Value = size.Height;
        }
    }

    private void OnApplyClicked(object sender, RoutedEventArgs e)
    {
        if (SelectedWindow() is not { } window || ReadPlacement() is not { } placement)
            return;

        Run(() =>
        {
            var result = WindowController.Apply(window.Handle, placement);
            if (result.IsExact)
                ShowStatus(InfoBarSeverity.Success, "Applied",
                    $"Client area is exactly {result.Actual}.");
            else
                ShowStatus(InfoBarSeverity.Warning, "Client area does not match",
                    $"Wanted {result.Target}, got {result.Actual}. The game may limit its own size, or DPI scaling may interfere.");
            RefreshWindows();
        });
    }

    private void OnRestoreClicked(object sender, RoutedEventArgs e)
    {
        if (SelectedWindow() is not { } window)
            return;

        Run(() =>
        {
            var restored = WindowController.Restore(window.Handle);
            ShowStatus(InfoBarSeverity.Informational, "Restored", $"Window is now {restored.Bounds} with its frame back.");
            RefreshWindows();
        });
    }

    private void LoadSettings()
    {
        foreach (var path in new[] { Path.Combine(AppContext.BaseDirectory, "settings.json"), "settings.json" })
        {
            if (!File.Exists(path))
                continue;
            try
            {
                _settings = GuillotineSettings.Load(path);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or System.Text.Json.JsonException or FormatException)
            {
                _settingsError = $"{path}: {ex.Message} Using defaults.";
            }
            return;
        }
    }

    private void RefreshWindows()
    {
        var selected = (WindowList.SelectedItem as WindowInfo)?.Handle;
        var windows = WindowController.GetWindows();
        WindowList.ItemsSource = windows;

        // Keep the current selection; otherwise pick the first configured game that is running.
        WindowList.SelectedItem =
            windows.FirstOrDefault(w => w.Handle == selected)
            ?? windows.FirstOrDefault(w => _settings.FindGame(w.Title, w.ProcessName) is not null);
    }

    private void ShowDetails(WindowInfo window)
    {
        var flags = new List<string>();
        if (window.IsBorderless) flags.Add("borderless");
        if (window.IsTopmost) flags.Add("always on top");

        var game = _settings.FindGame(window.Title, window.ProcessName);
        WindowDetails.Text =
            $"Client {window.Client}" +
            (flags.Count > 0 ? $" · {string.Join(", ", flags)}" : "") +
            (game is not null ? $" · matches \"{game.Match}\" in settings" : "");
    }

    private void ShowPlacement(Placement placement)
    {
        WidthBox.Value = placement.Size.Width;
        HeightBox.Value = placement.Size.Height;
        BorderlessSwitch.IsChecked = placement.Borderless;
        TopmostSwitch.IsChecked = placement.Topmost;
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

    private WindowInfo? SelectedWindow()
    {
        if (WindowList.SelectedItem is WindowInfo window)
            return window;
        ShowStatus(InfoBarSeverity.Error, "No window selected", "Pick a window from the list first.");
        return null;
    }

    private void Run(Action action)
    {
        try
        {
            action();
        }
        catch (WindowOperationException ex)
        {
            ShowStatus(InfoBarSeverity.Error, "Could not change the window", ex.Message);
            RefreshWindows();
        }
    }

    private void ShowStatus(InfoBarSeverity severity, string title, string message)
    {
        Status.Severity = severity;
        Status.Title = title;
        Status.Message = message;
        Status.IsOpen = true;
    }
}
