# Crosshair Lite

A tiny, event-driven crosshair overlay for Windows. No game hooks, no injection, no polling (the only timer is a 1-second fullscreen check, and only when that option is enabled).

## Features
- 8 styles, size / gap / thickness / opacity sliders, outlines, center dot
- Whitelist / blacklist by process name, "only on fullscreen", "hide on desktop"
- Named presets and shareable crosshair codes (`CLX1-...`) with validation and range reconciliation
- Hotkey: Ctrl+Alt+X toggles the crosshair; tray icon opens settings

## Build
Requires only the .NET Framework compiler that ships with Windows:

```
csc /target:winexe /optimize /win32icon:icon.ico /out:CrosshairLite.exe /r:System.Windows.Forms.dll /r:System.Drawing.dll Crosshair.cs
```

## Privacy
Crosshair Lite makes no network connections and collects no data. Settings are stored locally in `%LOCALAPPDATA%\CrosshairLite.ini` and `CrosshairLite.presets`. It only reads the foreground window's process name to apply your whitelist/blacklist rules.

## Code signing policy
Free code signing provided by [SignPath.io](https://about.signpath.io/), certificate by [SignPath Foundation](https://signpath.org/).
(Applies once signed releases are published.)

## License
MIT, see [LICENSE](LICENSE).
