using System.Globalization;
using System.Runtime.Versioning;
using Guillotine.Core;
using Guillotine.Windows;

namespace Guillotine.Cli;

internal static class Program
{
    private static readonly string Usage = $"""
        Guillotine {typeof(Program).Assembly.GetName().Version?.ToString(3)}

        Usage:
          guillotine-cli zones [options]          Print the derived zones for each monitor (default command)
          guillotine-cli monitors                 Print monitor bounds, work area and DPI scaling
          guillotine-cli windows                  List open windows with their size and process
          guillotine-cli apply [target] [options] Place a window in a zone at an exact client size
          guillotine-cli borderless on|off [target]
          guillotine-cli topmost on|off [target]
          guillotine-cli restore [target]         Put back the original frame and clear always on top
          guillotine-cli startup on|off|status    Start the app (Guillotine.exe) in the tray when you sign in

        Target (default: the first running game from settings.json):
          --title TEXT        Window whose title contains TEXT
          --process NAME      Window owned by process NAME (with or without .exe)
          --handle 0x1234     Window with this handle (from `guillotine-cli windows`)
          --wait              Keep checking every second until the window appears

        Options:
          --size WxH          Desired client size (default: from settings.json, else 2560x1440)
          --zone NAME         left, center, right, top, bottom, or a pair such as top-left
          --borderless / --framed     Remove or keep the title bar and borders (apply)
          --topmost / --no-topmost    Turn always on top on or off (apply)
          --work-area         Compute zones from the work area (excludes the taskbar)
          --hold SECONDS      apply: keep watching and put the window back if the game changes it
          --monitor WxH[@X,Y] zones only: use these bounds instead of real monitors (works on any OS)
          --settings PATH     Settings file (default: ./settings.json, then next to the exe)
        """;

    private static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidDataException or IOException or WindowOperationException)
        {
            Console.Error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    private sealed class Options
    {
        public bool? Toggle { get; set; }
        public PixelSize? Size { get; set; }
        public Zone? Zone { get; set; }
        public bool? UseWorkArea { get; set; }
        public bool? Borderless { get; set; }
        public bool? Topmost { get; set; }
        public PixelRect? ManualMonitor { get; set; }
        public string? SettingsPath { get; set; }
        public string? Title { get; set; }
        public string? Process { get; set; }
        public IntPtr? Handle { get; set; }
        public bool Wait { get; set; }
        public int HoldSeconds { get; set; }
    }

    private static int Run(string[] args)
    {
        var queue = new Queue<string>(args);
        var command = queue.Count > 0 && !queue.Peek().StartsWith('-') ? queue.Dequeue() : "zones";
        if (command == "startup")
            return RunStartup(queue.Count > 0 ? queue.Dequeue() : "status");

        var options = new Options
        {
            Toggle = command is "borderless" or "topmost" ? ParseOnOff(queue, command) : null,
        };

        while (queue.Count > 0)
        {
            var option = queue.Dequeue();
            switch (option)
            {
                case "--size": options.Size = PixelSize.Parse(Next(queue, option)); break;
                case "--zone": options.Zone = Zone.Parse(Next(queue, option)); break;
                case "--work-area": options.UseWorkArea = true; break;
                case "--monitor": options.ManualMonitor = ParseMonitor(Next(queue, option)); break;
                case "--settings": options.SettingsPath = Next(queue, option); break;
                case "--borderless": options.Borderless = true; break;
                case "--framed": options.Borderless = false; break;
                case "--topmost": options.Topmost = true; break;
                case "--no-topmost": options.Topmost = false; break;
                case "--title": options.Title = Next(queue, option); break;
                case "--process": options.Process = Next(queue, option); break;
                case "--handle": options.Handle = ParseHandle(Next(queue, option)); break;
                case "--wait": options.Wait = true; break;
                case "--hold": options.HoldSeconds = int.TryParse(Next(queue, option), out var seconds) && seconds > 0
                    ? seconds : throw new FormatException("--hold needs a number of seconds."); break;
                case "-h" or "--help": Console.WriteLine(Usage); return 0;
                default: throw new ArgumentException($"Unknown option '{option}'.\n\n{Usage}");
            }
        }

        switch (command)
        {
            case "zones" when options.ManualMonitor is { } bounds:
                PrintAllZones([new MonitorInfo(IntPtr.Zero, "--monitor", bounds, bounds, true, 96)], options);
                return 0;

            case "zones" or "monitors" or "windows" or "apply" or "borderless" or "topmost" or "restore":
                if (!OperatingSystem.IsWindows())
                    throw new ArgumentException($"`{command}` reads or changes real windows and needs Windows. `zones --monitor WxH` works anywhere.");
                Monitors.EnablePerMonitorDpiAwareness();
                return RunOnWindows(command, options);

            case "help":
                Console.WriteLine(Usage);
                return 0;

            default:
                throw new ArgumentException($"Unknown command '{command}'.\n\n{Usage}");
        }
    }

    private static int RunStartup(string action)
    {
        if (!OperatingSystem.IsWindows())
            throw new ArgumentException("`startup` needs Windows.");

        if (action is "on" or "off")
        {
            var app = StartupRegistration.DefaultAppPath;
            if (action == "on" && !File.Exists(app))
                throw new ArgumentException($"Guillotine.exe was not found next to guillotine-cli ({app}).");
            if (StartupRegistration.Set(action == "on", app) is { } error)
                throw new IOException(error);
        }
        else if (action != "status")
        {
            throw new ArgumentException("Use `guillotine-cli startup on`, `off` or `status`.");
        }

        Console.WriteLine($"Start with Windows: {StartupRegistration.Describe()}");
        return 0;
    }

    [SupportedOSPlatform("windows")]
    private static int RunOnWindows(string command, Options options)
    {
        if (command == "zones")
        {
            PrintAllZones(Monitors.GetAll(), options);
            return 0;
        }
        if (command == "monitors")
        {
            PrintMonitors(Monitors.GetAll());
            return 0;
        }
        if (command == "windows")
        {
            PrintWindows(WindowController.GetWindows());
            return 0;
        }

        var settings = LoadSettings(options.SettingsPath);
        var (window, game) = ResolveTarget(options, settings);

        switch (command)
        {
            case "apply":
                var placement = settings.PlacementFor(game);
                placement = placement with
                {
                    Size = options.Size ?? placement.Size,
                    Zone = options.Zone ?? placement.Zone,
                    Borderless = options.Borderless ?? placement.Borderless,
                    Topmost = options.Topmost ?? placement.Topmost,
                    UseWorkArea = options.UseWorkArea ?? placement.UseWorkArea,
                };
                var monitor = Monitors.ForWindow(window.Handle);
                if (PlacementDiagnostics.CheckFits(monitor.Bounds, monitor.WorkArea, placement.Size, placement.UseWorkArea) is { } fitProblem)
                    Console.WriteLine($"warning  {fitProblem}");
                var result = WindowController.Apply(window.Handle, placement);
                int exitCode = PrintApplyResult(result);
                return options.HoldSeconds > 0 ? Hold(window.Handle, placement, result, options.HoldSeconds) : exitCode;

            case "borderless":
                PrintWindows([WindowController.SetBorderlessKeepingClient(window.Handle, options.Toggle!.Value)]);
                return 0;

            case "topmost":
                WindowController.SetTopmost(window.Handle, options.Toggle!.Value);
                PrintWindows([WindowController.Describe(window.Handle)]);
                return 0;

            case "restore":
                PrintWindows([WindowController.Restore(window.Handle)]);
                return 0;

            default:
                throw new ArgumentException($"Unknown command '{command}'.");
        }
    }

    private static void PrintAllZones(IEnumerable<MonitorInfo> monitors, Options options)
    {
        var settings = LoadSettings(options.SettingsPath);
        foreach (var monitor in monitors)
            PrintZones(monitor, options.Size ?? settings.DesiredSize, options.UseWorkArea ?? settings.UseWorkArea, options.Zone);
    }

    private static bool ParseOnOff(Queue<string> queue, string command) =>
        (queue.Count > 0 ? queue.Dequeue() : "") switch
        {
            "on" => true,
            "off" => false,
            _ => throw new ArgumentException($"Use `guillotine-cli {command} on` or `guillotine-cli {command} off`."),
        };

    private static IntPtr ParseHandle(string text)
    {
        var hex = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text[2..] : text;
        return long.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value)
            ? new IntPtr(value)
            : throw new FormatException($"Expected a window handle like 0x1A2B, got '{text}'.");
    }

    /// <summary>
    /// Finds the window to act on, plus the matching game rule from settings (for per-game overrides).
    /// </summary>
    [SupportedOSPlatform("windows")]
    private static (WindowInfo Window, GameRule? Game) ResolveTarget(Options target, GuillotineSettings settings)
    {
        if (target.Handle is { } handle)
        {
            var window = WindowController.Describe(handle);
            return (window, settings.FindGame(window.Title, window.ProcessName));
        }

        GameRule[] rules = target switch
        {
            { Title: { } title } => [new GameRule { Match = title, By = MatchBy.Title }],
            { Process: { } process } => [new GameRule { Match = process, By = MatchBy.Process }],
            _ when settings.Games.Count > 0 => [.. settings.Games],
            _ => throw new ArgumentException("No target. Pass --title, --process or --handle, or add a game to settings.json."),
        };
        var description = string.Join(", ", rules.Select(r => $"{r.By.ToString().ToLowerInvariant()} \"{r.Match}\""));

        bool announced = false;
        while (true)
        {
            var matches = WindowController.Find(rules);
            if (matches.Count > 0)
            {
                var window = matches[0];
                if (matches.Count > 1)
                    Console.WriteLine($"note: {matches.Count} windows match {description}; using {window.HandleText} \"{window.Title}\". Pass --handle to pick another.");

                // An explicit --title/--process still picks up per-game overrides from settings.
                return (window, settings.FindGame(window.Title, window.ProcessName));
            }

            if (!target.Wait)
                throw new ArgumentException($"No open window matches {description}. Run `guillotine-cli windows` to see what is open, or add --wait.");
            if (!announced)
            {
                Console.WriteLine($"Waiting for a window matching {description} (Ctrl+C to stop)...");
                announced = true;
            }
            Thread.Sleep(TimeSpan.FromSeconds(1));
        }
    }

    private static void PrintWindows(IEnumerable<WindowInfo> windows)
    {
        Console.WriteLine($"{"handle",-12}{"pid",8}  {"process",-22}{"client",-24}{"flags",-20}title");
        foreach (var w in windows)
        {
            var flags = string.Join(",", new[] { w.IsBorderless ? "borderless" : null, w.IsTopmost ? "topmost" : null }.OfType<string>());
            Console.WriteLine($"{w.HandleText,-12}{w.ProcessId,8}  {Truncate(w.ProcessName, 21),-22}{w.Client,-24}{flags,-20}{w.Title}");
        }
    }

    private static int PrintApplyResult(ApplyResult result)
    {
        var w = result.Window;
        Console.WriteLine($"Window   {w.HandleText} {w.ProcessName}: \"{w.Title}\"");
        Console.WriteLine($"Monitor  {result.Monitor.DeviceName} {result.Monitor.Bounds}, scaling {result.Monitor.ScalePercent}%");
        Console.WriteLine($"Target   client {result.Target}");
        Console.WriteLine($"Actual   client {result.Actual}, window {w.Bounds}{(w.IsBorderless ? ", borderless" : "")}{(w.IsTopmost ? ", topmost" : "")}");

        if (result.Forced)
            Console.WriteLine("note     the window refused this size when asked normally, so its size limits were bypassed");

        if (result.IsExact)
        {
            Console.WriteLine("OK       client area is exactly the target size and position");
            return 0;
        }

        Console.WriteLine($"warning  {result.Problem}");
        return 2;
    }

    /// <summary>Watches the window and puts it back whenever it changes, logging each correction.</summary>
    [SupportedOSPlatform("windows")]
    private static int Hold(IntPtr hwnd, Placement placement, ApplyResult first, int seconds)
    {
        Console.WriteLine($"Holding for {seconds} s (Ctrl+C to stop)...");
        var keeper = new WindowKeeper(hwnd, placement, first.Settled);
        var clock = System.Diagnostics.Stopwatch.StartNew();

        while (clock.Elapsed < TimeSpan.FromSeconds(seconds))
        {
            Thread.Sleep(100);
            var change = keeper.Check();
            if (change is null)
            {
                if (keeper.GaveUp)
                    break;
                continue;
            }

            var actual = change.Result.Actual;
            Console.WriteLine($"{clock.Elapsed.TotalSeconds,6:0.0}s  window changed ({change.Changed}); put back, now {actual}" +
                              (change.Result.IsExact ? "" : " (still not exact)"));
        }

        var final = WindowController.Describe(hwnd);
        if (keeper.GaveUp)
            Console.WriteLine($"gave up   the window changed itself {keeper.Reapplies} times; the game keeps resizing its own window");
        else
            Console.WriteLine($"done      corrected {keeper.Reapplies} time(s)");
        Console.WriteLine($"Final     client {final.Client}{(final.IsBorderless ? ", borderless" : "")}{(final.IsTopmost ? ", topmost" : "")}");
        return final.Client == first.Target ? 0 : 2;
    }

    private static string Truncate(string text, int length) =>
        text.Length <= length ? text : text[..(length - 1)] + "…";

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
