using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;
using Microsoft.Win32;

namespace Guillotine.Windows;

/// <summary>
/// "Start with Windows". A normal user gets an entry under HKCU\...\Run. When Guillotine runs as
/// administrator (needed to control games that run as administrator), a Run entry would start it
/// without admin rights, so a scheduled task that runs at sign-in with highest privileges is used
/// instead. Both start the app (Guillotine.exe) with --tray, hidden in the notification area.
/// </summary>
[SupportedOSPlatform("windows")]
public static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string Name = "Guillotine";
    public const string TrayArgument = "--tray";

    public static bool IsElevated
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    /// <summary>The app next to the running exe (the CLI and the app ship side by side).</summary>
    public static string DefaultAppPath => System.IO.Path.Combine(AppContext.BaseDirectory, "Guillotine.exe");

    private static string Command(string appPath) => $"\"{appPath}\" {TrayArgument}";

    public static bool HasRunEntry
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            return key?.GetValue(Name) is string;
        }
    }

    public static bool HasTask => RunSchtasks($"/Query /TN \"{Name}\"").ExitCode == 0;

    public static bool IsEnabled => HasRunEntry || HasTask;

    /// <summary>A short description of the current state, for display.</summary>
    public static string Describe() => (HasTask, HasRunEntry) switch
    {
        (true, _) => "on (scheduled task at sign-in, runs as administrator)",
        (_, true) => "on (starts at sign-in)",
        _ => "off",
    };

    /// <summary>
    /// Turns start-with-Windows on or off for the app at <paramref name="appPath"/>. Returns an error
    /// message, or null on success.
    /// </summary>
    public static string? Set(bool enabled, string appPath)
    {
        try
        {
            if (!enabled)
            {
                RemoveRunEntry();
                if (HasTask)
                {
                    var delete = RunSchtasks($"/Delete /TN \"{Name}\" /F");
                    if (delete.ExitCode != 0)
                        return "The startup task runs as administrator; run Guillotine as administrator to remove it. " + delete.Output;
                }
                return null;
            }

            if (IsElevated)
            {
                var create = RunSchtasks(
                    $"/Create /TN \"{Name}\" /TR \"{Command(appPath).Replace("\"", "\\\"")}\" /SC ONLOGON /RL HIGHEST /IT /F");
                if (create.ExitCode != 0)
                    return "Could not create the startup task. " + create.Output;
                RemoveRunEntry();
            }
            else
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKey);
                key.SetValue(Name, Command(appPath));
            }
            return null;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or System.ComponentModel.Win32Exception or System.IO.IOException)
        {
            return ex.Message;
        }
    }

    /// <summary>Points an existing registration at this exe, in case it was moved since it was set up.</summary>
    public static void Refresh(string appPath)
    {
        try
        {
            if (HasRunEntry)
            {
                using var key = Registry.CurrentUser.CreateSubKey(RunKey);
                if (key.GetValue(Name) as string != Command(appPath))
                    key.SetValue(Name, Command(appPath));
            }
            else if (IsElevated && HasTask)
            {
                Set(true, appPath);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException)
        {
            // Not worth bothering the user about at startup; the switch shows the real state.
        }
    }

    private static void RemoveRunEntry()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(Name, throwOnMissingValue: false);
    }

    private static (int ExitCode, string Output) RunSchtasks(string arguments)
    {
        var info = new ProcessStartInfo("schtasks.exe", arguments)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        using var process = Process.Start(info)!;
        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit(10_000);
        return (process.ExitCode, output.Trim());
    }
}
