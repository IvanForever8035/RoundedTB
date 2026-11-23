# RoundedTB Light — experimental single-EXE pre-release 0.1.0-preview.4

Minimal package: the zip now contains only `RoundedTBHost.exe` plus the required
`LICENSE` and `THIRD-PARTY-NOTICES.txt`. Usage and the runtime requirement are
described on this release page instead of bundled README/release-notes files.

This pre-release consolidates the lightweight host and settings window into one
program EXE. By default it runs the Win32 tray host; when the host launches itself
with `--settings-only`, the same executable opens the WPF settings window in a
separate child process. Closing settings leaves the host running.

## Verified scope

Windows 11, single monitor, bottom taskbar:
- User-confirmed bottom-edge hover reveal and retract.
- Tray toggles and WPF settings Apply/reload.
- Normal host exit restores taskbar state.

## Requirements and runtime warning

Requires the **.NET 9 Windows Desktop Runtime (x64)**. The EXE is framework-
dependent and does not embed or install that runtime. .NET 9 reached end of
support on 2026-11-10; this is an experimental pre-release using an unsupported
runtime. A future release should move to a supported LTS runtime or a
self-contained deployment.

## Known limitations

Multiple displays/mixed DPI, non-bottom docking, maximize/task-switch fill,
TranslucentTB compatibility, Explorer restart handling, and force-kill recovery
have not been verified. Use the tray Exit command for a clean restore.
