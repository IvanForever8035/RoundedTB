# RoundedTB Light — experimental pre-release

First public pre-release of the **lightweight RoundedTB host**: a small .NET
Framework tray process that keeps the rounded-corner / segment regions on the
Windows 11 taskbar, with a settings window that starts on demand and exits when
closed.

## Verified scope

Single monitor, bottom taskbar:
- Bottom-edge hover reveal (no resting peek strip) and retract.
- Tray-menu toggles (Tray hover / App segment), saved to config.
- Settings window: open, Apply writes `%LOCALAPPDATA%\rtb.json`, host reloads.
- Normal exit restores the taskbar and exits cleanly.

## Not yet supported / unverified

- Multiple monitors and mixed DPI.
- Top / left / right docked taskbars (simple mode is orientation-agnostic;
  dynamic split mode is horizontal-only).
- Maximise / Alt+Tab taskbar fill.
- TranslucentTB compatibility.
- Explorer restart handling.
- Recovery after the host is force-killed (use tray → Exit for a clean restore).

## Requirements

- Windows 11; .NET Framework 4.8 (built into Windows 11).
- Settings window needs the .NET 6+ Windows Desktop Runtime (framework-dependent,
  major roll-forward enabled).

## Install / rollback

Portable: extract and run `RoundedTBHost.exe`. Uninstall by exiting from the tray
menu, turning off **Start with Windows**, and deleting the folder. See README.md
in the zip for details.

This is a pre-release; treat it as experimental.
