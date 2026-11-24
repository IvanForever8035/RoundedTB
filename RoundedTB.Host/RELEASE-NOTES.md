# RoundedTB Light — experimental single-EXE pre-release 0.1.0-preview.5

The download ZIP now contains only `RoundedTBHost.exe`. License files are not
bundled inside the program package: `LICENSE` and `THIRD-PARTY-NOTICES.txt` are
published as separate assets on this release page. Usage and the runtime
requirement are described here instead of bundled README/release-notes files.

This pre-release consolidates the lightweight host and settings window into one
program EXE. By default it runs the Win32 tray host; when the host launches itself
with `--settings-only`, the same executable opens the WPF settings window in a
separate child process. Closing settings leaves the host running.

Changes since `preview.4`:
- Hover reveal now samples the live cursor with `GetCursorPos` on each existing
  host tick instead of reusing the last Raw Input position. Raw Input is still an
  immediate tick signal, but no longer the position source, because it can stop
  arriving while a higher-integrity window is focused.

## Verified scope

Windows 11, single monitor, bottom taskbar:
- User-confirmed bottom-edge hover reveal and retract.
- Field-tested hover reveal while an elevated terminal had focus; the released
  `preview.4` intermittently failed, this build revealed reliably.
- Normal host exit restores taskbar state.

The WPF settings initialization fix passes the CI resource/StartupUri self-test;
manual opening, Apply and reload still have not been confirmed by the user.

## Known issue

The Settings... panel has been reported not to open in preview.4. CI verifies the
WPF resource/StartupUri initialization path, but the candidate fix has not yet
passed a manual UI smoke test. Use the stable WPF release if you need Settings
until this is confirmed.

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
