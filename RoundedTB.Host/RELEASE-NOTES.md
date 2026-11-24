# RoundedTB Light v0.1.0

`RoundedTBHost.exe` is published directly as the release asset — there is no ZIP
archive. Download the EXE and run it; `LICENSE` and `THIRD-PARTY-NOTICES.txt` are
separate assets on this release page, and `RoundedTBHost.exe.sha256` contains the
checksum of the executable.

The EXE is fully standalone: nothing is written beside it, so it can be placed in
any folder on its own. Per-user files live together in
`%LOCALAPPDATA%\RoundedTB\`:

- `rtb.json` — settings shared by host and settings UI.
- `rtb.log` — settings UI log.
- `rtb-host.log` — host diagnostics (startup, takeover, autohide alignment).

An existing `%LOCALAPPDATA%\rtb.json` from older builds is copied into the new
folder on first run; the old file is left in place. `probe.log` beside older EXEs
can be deleted.

The same executable runs the lightweight Win32 tray host by default and opens the
WPF settings window when relaunched with `--settings-only`, in a separate child
process. Closing settings leaves the host running.

## Verified scope

Windows 11, single monitor, bottom taskbar:
- User-confirmed bottom-edge hover reveal and retract.
- Hover reveal field-tested while an elevated terminal had focus.
- Settings window opens from the tray and Apply reloads the host.
- Normal host exit restores taskbar state.

## Not verified

Multiple displays and mixed DPI, top/left/right docked taskbars, maximise and
Alt+Tab taskbar fill, TranslucentTB compatibility, Explorer restart handling, and
recovery after force-killing the host. Use the tray Exit command for a clean
restore.

## Requirements and runtime warning

Requires the **.NET 9 Windows Desktop Runtime (x64)**. The EXE is framework-
dependent and does not embed or install that runtime. .NET 9 reached end of
support on 2026-11-10, so this release uses an unsupported runtime. A future
release should move to a supported LTS runtime or a self-contained deployment.

This is the lightweight-host release line, not a replacement for the WPF stable
release `v1.0.6`. The stable WPF line remains the recommended full-featured build
until the Light platform matrix above is verified.
