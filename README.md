# Guillotine

Borderless, exact-resolution window placement for ultrawide streaming.

Guillotine moves a game window to a zone on your monitor, resizes it to an exact resolution, and removes the title bar and borders so the client area is the full size. The main use is streaming at true 1440p from a super-ultrawide monitor, where window borders would otherwise take pixels off the captured frame.

> **Status:** early. Placement, borderless and always-on-top work from both the app and the command line. Auto-apply on launch, hotkeys and a tray icon are not built yet (see [docs/handoff.md](docs/handoff.md)).

## Download

Each push builds `Guillotine.exe` (the app) and `guillotine-cli.exe` (the command line) for 64-bit Windows 10 or 11. Get them from the **guillotine-win-x64** artifact on the latest [Build run](../../actions/workflows/build.yml), or from [Releases](../../releases) once a version is tagged. Both are single, self-contained files; .NET does not need to be installed.

Keep `settings.json` next to the exe to set your defaults and games.

## Using the app

1. Start the game in windowed mode, then open `Guillotine.exe`.
2. Pick the game from the window list. A game listed in `settings.json` is picked automatically, along with its settings.
3. Choose the client size, the zone and the options, then click **Apply**. The status bar confirms that the client area is exactly the size you asked for.
4. **Restore** puts the title bar and borders back and turns off always on top.

If the game runs as administrator, run Guillotine as administrator too, or Windows will block the changes.

## How zones work

Zones are never stored as pixel coordinates. Each time, they are computed from the monitor's current bounds and the size you want:

- left X = `monitor.Left`, center X = `monitor.Left + (monitor.Width - width) / 2`, right X = `monitor.Right - width`
- top Y = `monitor.Top`, center Y = `monitor.Top + (monitor.Height - height) / 2`, bottom Y = `monitor.Bottom - height`

On a 5120x1440 monitor with a 2560x1440 window, that gives left at X = 0, center at X = 1280 and right at X = 2560. Zone names are `left`, `center`, `right`, `top`, `bottom`, or a pair with the vertical part first, such as `top-left` or `bottom-right`.

## Command line

```
guillotine-cli monitors                                   # bounds, work area and DPI scaling per monitor
guillotine-cli zones --size 1920x1080                     # derived zone positions per monitor
guillotine-cli windows                                    # open windows with process, client size and flags
guillotine-cli apply                                      # place the first running game from settings.json
guillotine-cli apply --title "Heroes of the Storm" --zone right --wait
guillotine-cli apply --process HeroesOfTheStorm_x64 --size 2560x1440 --framed
guillotine-cli borderless on|off --title "Heroes of the Storm"
guillotine-cli topmost on|off --title "Heroes of the Storm"
guillotine-cli restore --title "Heroes of the Storm"
```

- Target a window with `--title` (part of the title), `--process` (process name) or `--handle` (from `windows`). With no target, the first running game from `settings.json` is used. `--wait` keeps checking until the window appears.
- `apply` takes `--size`, `--zone`, `--borderless`/`--framed`, `--topmost`/`--no-topmost` and `--work-area` to override the settings. It checks the client area afterwards and exits with code 2 if it does not match.
- `zones --monitor 5120x1440` (optionally `5120x1440@X,Y`) uses the bounds you give instead of reading real monitors, so you can check the math on any OS.
- `monitors` is the quickest way to confirm your monitor is one 5120x1440 display at 100% scaling.

### How placement works

- **Borderless** removes the title bar, resize frame and edge styles and makes the window a popup, so the whole window is client area. The original style is saved on the window itself, so `restore` (from the app or the command line) puts it back exactly.
- **Framed** keeps the frame and sizes the window so the *client area* is still the exact size and sits on the zone; the frame extends outside it.
- The window is placed on the monitor it is currently on. Move it to another monitor first to use that one.

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
| `src/Guillotine.Windows` | Win32 interop: monitors, window enumeration and placement. |
| `src/Guillotine.App` | The app (`Guillotine.exe`), WPF with [WPF-UI](https://github.com/lepoco/wpfui). |
| `src/Guillotine.Cli` | The command line (`guillotine-cli.exe`). |
| `tests/Guillotine.Core.Tests` | xUnit tests for the core library. |

## Building

Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0). `dotnet build` and `dotnet test` work on any OS; the app and window commands only run on Windows.

To make the single-file exes yourself:

```
dotnet publish src/Guillotine.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o publish
dotnet publish src/Guillotine.Cli -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o publish
```

To publish a release, push a tag such as `v0.1.0`. The Build workflow attaches a zip of both exes to a GitHub release.

## License

[MIT](LICENSE)
