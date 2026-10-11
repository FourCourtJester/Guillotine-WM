using System.Globalization;
using Guillotine.Core;
using Guillotine.Windows;

namespace Guillotine.Cli;

internal static class Program
{
    private const string Usage = """
        Usage:
          guillotine zones [options]    Print the derived zones for each monitor (default command)
          guillotine monitors           Print monitor bounds, work area and DPI scaling

        Options for zones:
          --size WxH          Desired window size (default: from settings.json, else 2560x1440)
          --zone NAME         Show only this zone (left, center, right, top-left, ...)
          --work-area         Compute zones from the work area (excludes the taskbar)
          --monitor WxH[@X,Y] Use these bounds instead of reading real monitors (works on any OS)
          --settings PATH     Settings file (default: ./settings.json, then next to the exe)
        """;

    private static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidDataException or IOException)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        var queue = new Queue<string>(args);
        var command = queue.Count > 0 && !queue.Peek().StartsWith('-') ? queue.Dequeue() : "zones";

        PixelSize? size = null;
        Zone? onlyZone = null;
        bool? useWorkArea = null;
        PixelRect? manualMonitor = null;
        string? settingsPath = null;

        while (queue.Count > 0)
        {
            var option = queue.Dequeue();
            switch (option)
            {
                case "--size": size = PixelSize.Parse(Next(queue, option)); break;
                case "--zone": onlyZone = Zone.Parse(Next(queue, option)); break;
                case "--work-area": useWorkArea = true; break;
                case "--monitor": manualMonitor = ParseMonitor(Next(queue, option)); break;
                case "--settings": settingsPath = Next(queue, option); break;
                case "-h" or "--help": Console.WriteLine(Usage); return 0;
                default: throw new ArgumentException($"Unknown option '{option}'.\n\n{Usage}");
            }
        }

        if (OperatingSystem.IsWindows())
            Monitors.EnablePerMonitorDpiAwareness();

        switch (command)
        {
            case "monitors":
                PrintMonitors(RequireMonitors());
                return 0;

            case "zones":
                var settings = LoadSettings(settingsPath);
                var desired = size ?? settings.DesiredSize;
                var workArea = useWorkArea ?? settings.UseWorkArea;
                var monitors = manualMonitor is { } bounds
                    ? [new MonitorInfo(IntPtr.Zero, "--monitor", bounds, bounds, true, 96)]
                    : RequireMonitors();

                foreach (var monitor in monitors)
                    PrintZones(monitor, desired, workArea, onlyZone);
                return 0;

            case "help":
                Console.WriteLine(Usage);
                return 0;

            default:
                throw new ArgumentException($"Unknown command '{command}'.\n\n{Usage}");
        }
    }

    private static string Next(Queue<string> queue, string option) =>
        queue.Count > 0 ? queue.Dequeue() : throw new ArgumentException($"{option} needs a value.");

    /// <summary>Parses "5120x1440" or "5120x1440@-1920,0".</summary>
    private static PixelRect ParseMonitor(string text)
    {
        var at = text.Split('@');
        var size = PixelSize.Parse(at[0]);
        if (at.Length == 1)
            return new PixelRect(0, 0, size.Width, size.Height);

        var origin = at.Length == 2 ? at[1].Split(',') : [];
        if (origin.Length != 2
            || !int.TryParse(origin[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var x)
            || !int.TryParse(origin[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var y))
            throw new FormatException($"Expected --monitor like 5120x1440 or 5120x1440@-1920,0, got '{text}'.");

        return new PixelRect(x, y, size.Width, size.Height);
    }

    private static IReadOnlyList<MonitorInfo> RequireMonitors()
    {
        if (!OperatingSystem.IsWindows())
            throw new ArgumentException("Reading real monitors needs Windows. Pass --monitor WxH to try the math elsewhere.");
        return Monitors.GetAll();
    }

    private static GuillotineSettings LoadSettings(string? explicitPath)
    {
        if (explicitPath is not null)
            return GuillotineSettings.Load(explicitPath);

        foreach (var candidate in new[] { "settings.json", Path.Combine(AppContext.BaseDirectory, "settings.json") })
        {
            if (File.Exists(candidate))
                return GuillotineSettings.Load(candidate);
        }
        return new GuillotineSettings();
    }

    private static void PrintMonitors(IReadOnlyList<MonitorInfo> monitors)
    {
        foreach (var m in monitors)
        {
            Console.WriteLine($"{m.DeviceName}{(m.IsPrimary ? " (primary)" : "")}");
            Console.WriteLine($"  bounds     {m.Bounds}");
            Console.WriteLine($"  work area  {m.WorkArea}");
            Console.WriteLine($"  scaling    {m.ScalePercent}% ({m.Dpi} DPI)");
            if (m.ScalePercent != 100)
                Console.WriteLine("  warning    scaling is not 100%; games that are not DPI aware may not size pixel-exact");
        }
    }

    private static void PrintZones(MonitorInfo monitor, PixelSize desired, bool useWorkArea, Zone? onlyZone)
    {
        var bounds = useWorkArea ? monitor.WorkArea : monitor.Bounds;

        Console.WriteLine($"{monitor.DeviceName}{(monitor.IsPrimary ? " (primary)" : "")}: {(useWorkArea ? "work area" : "bounds")} {bounds}, scaling {monitor.ScalePercent}%");
        Console.WriteLine($"Desired size {desired}");
        if (!ZoneCalculator.Fits(bounds, desired))
            Console.WriteLine("  warning: desired size is larger than the area, so the window will extend past it");

        Console.WriteLine($"  {"zone",-15}{"x",8}{"y",8}");
        foreach (var zone in onlyZone is { } z ? [z] : Zone.All)
        {
            var rect = ZoneCalculator.Compute(bounds, desired, zone);
            Console.WriteLine($"  {zone,-15}{rect.Left,8}{rect.Top,8}");
        }
        Console.WriteLine();
    }
}
