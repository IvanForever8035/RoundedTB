# RoundedTB Light — lightweight host release

The release asset is **one program EXE**: `RoundedTBHost.exe`, published directly
(no ZIP). The same file runs the lightweight Win32 tray host by default and opens
the WPF settings UI when relaunched with `--settings-only`. Host and settings
remain separate processes; closing the settings window does not stop the host. Tray-launched settings use `--host-pid` and close when the host
exits.

Light **v0.1.1** is a formal release under the `light-v0.1.1` tag. Its EXE,
checksum and license assets have been verified. It is not marked Latest;
WPF v1.0.6 remains the Latest stable release. Light is not a full replacement for WPF.

`LICENSE`, `THIRD-PARTY-NOTICES.txt` and `RoundedTBHost.exe.sha256` are separate
assets on the release page.

## Requirements

- Windows 11.
- **.NET 9 Windows Desktop Runtime (x64)** must already be installed. This is a
  framework-dependent build; it does not install the runtime. .NET 9 reached end
  of support on 2026-11-10, so this release uses an unsupported runtime and is
  for evaluation only.

## Verified scope

The user currently reports no issues in their usage. This is not a full platform
acceptance result. Previously confirmed scope is a single monitor, bottom taskbar:

- Bottom-edge hover reveal (no resting peek strip) and native retract.
- Hover reveal with an elevated terminal focused; cursor position is sampled
  every tick rather than depending on Raw Input delivery.
- Normal host exit restores the taskbar; tray Settings opens and Apply reloads
  the host.

## Current behavior

- Hover activates only enabled segments under the cursor. After leaving a
  segment, the last region is retained for Explorer's native retract and cleared
  once hidden; regions are not reset during animation.
- Required geometry must be ready before custom segmentation is applied. If it
  is missing, the host restores Explorer's native region and retries, retaining
  valid previous measurements. A zero-width hidden `TrayNotifyWnd` rectangle
  falls back to UIA tray bounds.
- Missing configuration is created with safe defaults after any legacy-file
  migration. Existing invalid configuration is preserved, not overwritten.
- Tray-launched settings close with the host via `--host-pid`.
- **Start with Windows** stores the current EXE path when enabled. Diagnostics
  and the tray menu dynamically check whether it matches the running EXE; running
  a copy elsewhere does not automatically rewrite the startup entry.


## Not yet verified

- Multiple monitors and mixed DPI.
- Top / left / right docked taskbars.
- Maximise / Alt+Tab taskbar fill.
- TranslucentTB compatibility.
- Explorer restart handling.
- Recovery after force-killing the host (use tray → Exit for a clean restore).

## Run

Download `RoundedTBHost.exe` and run it. There is no fixed installation path; it
does not write anything beside the executable and can live alone in any folder.
Per-user files live in
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
