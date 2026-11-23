# RoundedTB Light — experimental pre-release

This package contains **one program EXE**: `RoundedTBHost.exe`. The same file
runs the lightweight Win32 tray host by default and opens the WPF settings UI
when relaunched with `--settings-only`. Host and settings remain separate
processes; closing the settings window does not stop the host.

## Requirements

- Windows 11.
- **.NET 9 Windows Desktop Runtime (x64)** must already be installed. This is a
  framework-dependent build; it does not install the runtime. .NET 9 reached end
  of support on 2026-11-10, so this pre-release uses an unsupported runtime and
  is for evaluation only.

## Verified scope

Single monitor, bottom taskbar:
- Bottom-edge hover reveal (no resting peek strip) and retract.
- Tray-menu toggles (Tray hover / App segment), saved to config.
- Settings UI opens from the same EXE; Apply writes `%LOCALAPPDATA%\rtb.json`
  and the host reloads it.
- Closing the settings UI exits its child process while the host remains running.
- Normal host exit restores the taskbar.

## Not yet verified

- Multiple monitors and mixed DPI.
- Top / left / right docked taskbars.
- Maximise / Alt+Tab taskbar fill.
- TranslucentTB compatibility.
- Explorer restart handling.
- Recovery after force-killing the host (use tray → Exit for a clean restore).

## Run

Extract the package and run `RoundedTBHost.exe`. Use the tray icon's **Settings...**
item to open the settings child process. The settings and host share
`%LOCALAPPDATA%\rtb.json`; logs are written to `probe.log` beside the EXE.

## Uninstall / rollback

Exit the host from the tray menu first, then turn off **Start with Windows** if
enabled, and delete the folder. The settings/config file is not removed.

## License

GPL-3.0 (see `LICENSE`). Bundled third-party components are listed in
`THIRD-PARTY-NOTICES.txt`.
