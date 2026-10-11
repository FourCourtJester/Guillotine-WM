# Guillotine

Borderless, exact-resolution window placement for ultrawide streaming.

Guillotine moves a game window to a zone on your monitor, resizes it to an exact resolution, and removes the title bar and borders so the client area is the full size. The main use is streaming at true 1440p from a super-ultrawide monitor, where window borders would otherwise take pixels off the captured frame.

> **Status:** early prototype. The zone math and a console tool that prints zones are done. Window detection, borderless and always-on-top come next (see [docs/handoff.md](docs/handoff.md)).

## How zones work

Zones are never stored as pixel coordinates. Each time, they are computed from the monitor's current bounds and the size you want:

- left X = `monitor.Left`, center X = `monitor.Left + (monitor.Width - width) / 2`, right X = `monitor.Right - width`
- top Y = `monitor.Top`, center Y = `monitor.Top + (monitor.Height - height) / 2`, bottom Y = `monitor.Bottom - height`

On a 5120x1440 monitor with a 2560x1440 window, that gives left at X = 0, center at X = 1280 and right at X = 2560. Zone names are `left`, `center`, `right`, `top`, `bottom`, or a pair with the vertical part first, such as `top-left` or `bottom-right`.

## Usage

Requires Windows 10 or 11 and the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```
dotnet run --project src/Guillotine.Cli -- monitors
dotnet run --project src/Guillotine.Cli -- zones
dotnet run --project src/Guillotine.Cli -- zones --size 1920x1080 --work-area
```

- `monitors` prints each monitor's bounds, work area (without the taskbar) and DPI scaling. Use it to confirm your monitor is one 5120x1440 display at 100% scaling.
- `zones` prints the derived zone positions for every monitor. `--size` overrides the desired size, `--zone` shows one zone, and `--work-area` computes zones from the work area instead of the full monitor.
- `--monitor 5120x1440` (optionally `5120x1440@X,Y`) uses the bounds you give instead of reading real monitors, so you can check the math on any OS.

## Settings

`settings.json` holds only preferences. It never holds zone coordinates.

```json
{
  "desiredWidth": 2560,
  "desiredHeight": 1440,
  "defaultZone": "center",
  "borderless": true,
  "topmost": false,
  "useWorkArea": false,
  "games": [
    { "match": "Heroes of the Storm", "by": "title" }
  ]
}
```

Each entry in `games` matches by `title` (part of the window title) or `process` (process name without `.exe`) and can override `desiredWidth`, `desiredHeight`, `zone`, `borderless` and `topmost`.

## Project layout

| Path | Contents |
|---|---|
| `src/Guillotine.Core` | Zone math, sizes and settings. Platform independent. |
| `src/Guillotine.Windows` | Win32 interop (monitors so far). |
| `src/Guillotine.Cli` | Console prototype (`guillotine.exe`). |
| `tests/Guillotine.Core.Tests` | xUnit tests for the core library. |

Run the tests with `dotnet test`.
