# Roadmap

Ideas and requests for later, roughly in priority order.

## Custom resolution presets

The app has three fixed size buttons (2560 × 1440, 1920 × 1080, 1280 × 720). Users need to add their own.

- An **Add** button next to the presets saves the current width × height as a new preset.
- Presets are stored in `settings.json` (for example `"presets": ["2560x1440", "1920x1080", "1280x720"]`), with those three as the defaults when the list is missing.
- Each custom preset can be removed (a small × or a right-click menu); the defaults can be removed too, and reset if the list is emptied.
- The app needs to write `settings.json`, so saving should keep the user's other settings and games intact.

## From the original handoff

- **Keep in place** switch in the app: re-apply when the game moves, resizes or resets its window (the CLI already has `apply --hold`).
- **Auto-apply** when a configured game launches.
- **Remember** the last-used zone and size per game.
- **Hotkeys** to apply a zone or toggle borderless and always on top.
- **Tray icon** with a menu.
- More zones, such as quarter-screen or a custom offset.
