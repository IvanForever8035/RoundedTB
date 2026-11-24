# RoundedTB Light — experimental pre-release

This package contains **one program EXE**: `RoundedTBHost.exe`. The same file
runs the lightweight Win32 tray host by default and opens the WPF settings UI
when relaunched with `--settings-only`. Host and settings remain separate
processes; closing the settings window does not stop the host.

License files are not bundled in the program ZIP. `LICENSE` and
`THIRD-PARTY-NOTICES.txt` are published as separate assets on the release page.

## Requirements

- Windows 11.
- **.NET 9 Windows Desktop Runtime (x64)** must already be installed. This is a
  framework-dependent build; it does not install the runtime. .NET 9 reached end
  of support on 2026-11-10, so this pre-release uses an unsupported runtime and
  is for evaluation only.

## Verified scope

Single monitor, bottom taskbar:
- Bottom-edge hover reveal (no resting peek strip) and retract.
- Hover reveal field-tested with an elevated terminal focused; cursor position is
  sampled every tick, so it no longer depends on Raw Input delivery.
- Normal host exit restores the taskbar.

The settings initialization/resource path is covered by CI self-test. **Known
issue:** the user reported the Settings window does not open in preview.4; the
candidate initialization fix has not yet passed a manual UI smoke test.

## Not yet verified

- Multiple monitors and mixed DPI.
- Top / left / right docked taskbars.
- Maximise / Alt+Tab taskbar fill.
- TranslucentTB compatibility.
- Explorer restart handling.
- Recovery after force-killing the host (use tray → Exit for a clean restore).

## Run

Extract the package and run `RoundedTBHost.exe`. It does not write anything beside
the executable; the EXE can live alone in any folder. Per-user files live in
`%LOCALAPPDATA%\RoundedTB\`: `rtb.json` (settings), `rtb.log` (settings UI log) and
`rtb-host.log` (host diagnostics). A settings file left by older builds at
`%LOCALAPPDATA%\rtb.json` is copied there on first run. Use the tray icon's
**Settings...** item to open the settings child process. The host watches
`rtb.json` and reloads after Apply.

## Uninstall / rollback

Exit the host from the tray menu first, then turn off **Start with Windows** if
enabled, and delete the folder. The settings/config file is not removed.

## License

GPL-3.0 (see the `LICENSE` asset on the release page). Bundled third-party
components are listed in `THIRD-PARTY-NOTICES.txt`, also a separate release asset.
