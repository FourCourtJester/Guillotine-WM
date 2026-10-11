# Handoff: Window Manager for Borderless Ultrawide Game Windows

## Project Name

**Guillotine** (working name). It describes the main job: cutting off the title bar and borders.

Before creating the repo, check whether the name is already in use:
- GitHub: search repository names and topics for "guillotine" to see how crowded the space is.
- NuGet (if using C#): check whether a package named Guillotine already exists.
- Package managers (Chocolatey, Scoop) if you plan to distribute there later.

Why it matters: a name collision makes the tool harder to find in search, can block a package publish, and can make it harder to build a brand or website later. If the name is taken, options include adding a qualifier (for example, "guillotine-window" or "GuillotineWM") or choosing a new name.

Suggested repo description: "Borderless, exact-resolution window placement for ultrawide streaming."

## Goal

A small Windows utility that detects a running game window, moves it to a zone on the monitor, resizes it to a chosen resolution, strips the window border so the client area is the full size, and optionally keeps it always on top. The primary use case is streaming at true 1440p from a super-ultrawide monitor, where window borders currently consume pixels and make the stream less than 1440p.

## Problem Statement

- Older games (example: Heroes of the Storm) do not support ultrawide. Their borderless windowed mode does not offer resolution options, so the window cannot be sized to 2560x1440.
- Placing a windowed game at 2560x1440 with a tool like PowerToys FancyZones positions it correctly, but the title bar and borders remain. The client area ends up smaller than 2560x1440, so the resolution is not exact and the stream loses pixels.
- The Windows taskbar overlaps windowed games when it slides in, which interferes with mouse scroll.

## Requirements

### Must have
1. Detect a target window (by process name or window title) when it opens, or pick it from a list of open windows.
2. Take a **desired resolution** (for example, 2560x1440) and place it in a **zone** on the monitor: left, center, or right, with the vertical position set to top, center, or bottom.
3. Remove window borders and the title bar so the client area equals the desired resolution exactly (borderless).
4. Toggle borderless on and off per window.
5. Toggle always-on-top per window.

### Nice to have
- Additional zones (for example, a quarter-screen or custom offset zone).
- Changing the desired resolution from the UI without editing files.
- Remember the last-used zone and resolution per game.
- System tray icon with a menu.
- Hotkeys to apply zones or toggle settings.
- Auto-apply when a configured game launches.

## Core Design Principle: No Static Presets

Zones are **never stored as fixed pixel coordinates**. They are computed at runtime from the current monitor geometry, so they stay correct if the resolution, scaling, or monitor changes.

The user supplies only the **desired window size**. Everything else is derived:

1. Read the monitor's current bounds with `GetMonitorInfo` (use `rcMonitor` for full bounds, or `rcWork` if the taskbar should be excluded).
2. Compute each zone from those bounds and the desired size:
   - `left X   = monitor.Left`
   - `center X = monitor.Left + (monitor.Width - desired.Width) / 2`
   - `right X  = monitor.Right - desired.Width`
   - `top Y    = monitor.Top`
   - `center Y = monitor.Top + (monitor.Height - desired.Height) / 2`
   - `bottom Y = monitor.Bottom - desired.Height`
3. Apply the chosen zone with `SetWindowPos` to the computed X and Y, at the desired width and height.

Zones are a function of (monitor bounds, desired size). Nothing is hardcoded.

### Worked example for the current setup
- Monitor: 5120x1440 (assumed to be a single monitor at 100% scaling; confirm)
- Desired size: 2560x1440
- Derived zones:
  - Left: X = 0, Y = 0
  - Center: X = (5120 - 2560) / 2 = **1280**, Y = 0
  - Right: X = 5120 - 2560 = **2560**, Y = 0

Because the desired height equals the monitor height, vertical zones collapse to Y = 0. The tool should still compute them, so a shorter desired height (for example, 1080) gets top, middle, and bottom zones without code changes.

## Initial Settings

| Setting | Value |
|---|---|
| Monitor resolution | 5120x1440 (confirm; also confirm it is one display, not two combined) |
| Desired resolution (default) | 2560x1440 |
| Default zone | Center |
| Borderless | On |
| Always on top | Off by default, toggleable |
| DPI scaling | Assumed 100%. Keep at 100% for pixel-exact results. |

## Technical Approach

### Stack
- **C# with WPF or WinForms** (recommended). Good Windows API support and easy to compile to a single `.exe`.
- Alternative: C++ with Win32 for a lighter binary.

### Windows API calls
- `EnumWindows`, `GetWindowText`, `GetWindowThreadProcessId`: enumerate windows and identify the target.
- `FindWindow`: quick lookup by class or title.
- `MonitorFromWindow`, `GetMonitorInfo`: read the current monitor bounds for zone calculation.
- `GetWindowRect`, `GetClientRect`: read the current window and client area.
- `SetWindowLong` / `SetWindowLongPtr` with `GWL_STYLE`: remove `WS_CAPTION`, `WS_THICKFRAME`, and related flags. Setting `WS_POPUP` is a common way to get a clean borderless window.
- `SetWindowPos` with `HWND_TOPMOST` / `HWND_NOTOPMOST`: toggle always-on-top.
- `SetWindowPos` with `SWP_FRAMECHANGED`: required after changing style flags so Windows redraws the frame.
- `AdjustWindowRectEx`: needed only if the window keeps a frame, so the client area still ends up at the desired size. For a borderless window, window rect and client rect match, so this is not needed.

### Process detection
- Start with title matching for simplicity.
- Process name matching (via `Process.GetProcesses()`) is more reliable when titles change.
- A polling timer (every 1–2 seconds) is simple and sufficient for auto-apply.

### Key implementation detail
After removing the frame, call `SetWindowPos` with the computed zone position, the desired size, and `SWP_FRAMECHANGED`. Then verify with `GetClientRect` that the client area equals the desired size. Log a warning if it does not, since some games or DPI settings can interfere.

## Known Risks and Open Questions

1. **Anti-cheat.** Some modern games block or flag window manipulation on their process. Heroes of the Storm is expected to be fine, but test per game. The app uses only documented window APIs and does not inject code.
2. **Game-side resets.** Some games reset their window style or size on focus change or when a setting is reapplied. The app may need to re-apply on a timer or on focus-change events.
3. **Taskbar overlap.** A 1440-tall window on a 1440-tall monitor will overlap the taskbar if it is visible. Options to evaluate:
   - Set the taskbar to auto-hide.
   - Compute zones from `rcWork` instead of `rcMonitor`, which may shrink the usable height.
   - Accept the overlap for streaming and use a hotkey to hide the taskbar.
4. **Monitor assumptions.** Confirm the monitor is 5120x1440 as a single display and that scaling is 100%. If the scaling is not 100%, the pixel math changes.
5. **Mouse behavior.** Confirm that cursor clipping and mouse input behave correctly after the window is moved and resized.
6. **Windows version.** Target Windows 11. Test on the current build.

## Testing Plan

1. Open Heroes of the Storm in windowed mode.
2. Run the tool with desired size 2560x1440 and zone Center, borderless on.
3. Confirm with `GetClientRect` that the client area is exactly 2560x1440.
4. Confirm X = 1280 and Y = 0 for Center. Repeat for Left (X = 0) and Right (X = 2560).
5. Toggle always-on-top and confirm behavior.
6. Open OBS or a similar capture tool and confirm the captured frame is 2560x1440 with no border pixels.
7. Check taskbar behavior with auto-hide on and off.
8. Change the desired size to 1920x1080 and confirm the zones recompute correctly without code changes.
9. Repeat with a second game that has a native borderless option to compare.

## Repository Setup (suggested)

- `README.md`: purpose, screenshot, usage, supported Windows version
- `WindowManager.sln` (or equivalent)
- `src/`: the WinForms/WPF project
- `settings.json`: user settings only (desired width and height, default zone, borderless and topmost defaults, per-game overrides). No zone coordinates.
- `LICENSE`
- `docs/handoff.md`: this document

### Suggested `settings.json` starting point
```json
{
  "desiredWidth": 2560,
  "desiredHeight": 1440,
  "defaultZone": "center",
  "borderless": true,
  "topmost": false,
  "games": [
    { "match": "Heroes of the Storm", "by": "title" }
  ]
}
```

## Prior Art and Positioning

- PowerToys FancyZones handles snapping and resizing well but does not remove borders or give exact client-area sizing for streaming.
- AutoHotkey scripts exist but are often unmaintained and not packaged for non-technical users.
- No widely known, maintained tool targets streamers who need exact-resolution borderless windows on ultrawide monitors. This is the gap the project would fill.

## Next Steps

1. Create the repository and the solution.
2. Build a console prototype that reads the monitor bounds and prints the derived zones for a given desired size. This verifies the math before any window code.
3. Add window enumeration and apply a zone to Heroes of the Storm.
4. Add the borderless toggle and verify the client area.
5. Add always-on-top, then the small GUI.
6. Test the stream capture end-to-end.
