# RoundedTB Light v0.1.1

A maintenance release for dynamic hover, Explorer startup timing, and first-run recovery.

## Fixes

- Restrict dynamic hover reveal to enabled segments. Disabled app segments and gaps no longer expose the whole taskbar.
- Preserve the last visible segment region for Explorer's native retract animation; do not replace regions mid-animation.
- Measure Windows 11 tray bounds using UI Automation when the legacy `TrayNotifyWnd` has zero width while auto-hidden.
- Wait and retry when Explorer has not yet created the taskbar at sign-in. Retry missing app/tray geometry, refresh during initialization, and preserve previous valid measurements.
- Temporarily keep Explorer's native edge available when enabled target geometry is unknown, then restore segment clipping once measured. This prioritizes access to the taskbar during initialization.
- Create default settings if `rtb.json` is missing, including after clearing app data. Existing malformed settings are preserved rather than overwritten.
- Close host-launched Settings windows when their owning host exits.
- Check the registered startup command against the current EXE path and show a tray-menu warning when they differ. Starting an EXE from another directory does not silently overwrite an existing startup command; select **Start with Windows** in the intended instance to register it.
- Make **Settings...** and **Start with Windows** available in both simple and dynamic modes.
- Add startup, geometry-readiness and region-action diagnostics and regression self-tests.
- Correct the executable's embedded version metadata to `0.1.1`.

## Download and requirements

Download **RoundedTBHost.exe** directly. No ZIP is required. The release also provides `RoundedTBHost.exe.sha256`, `LICENSE`, and `THIRD-PARTY-NOTICES.txt`.

The same EXE runs the Win32 tray host by default and the independent WPF Settings UI with `--settings-only`. It can live in any directory and writes no runtime files beside itself. Settings and logs are in `%LOCALAPPDATA%\RoundedTB\`; an older `%LOCALAPPDATA%\rtb.json` is migrated when needed.

Requires Windows 11 and **.NET 9 Windows Desktop Runtime x64**. This is a framework-dependent EXE, not a self-contained runtime bundle. .NET 9 reached end of support on 2026-11-10 and remains an unsupported dependency.

## Validation and limitations

The user reports no current issues in their tested single-monitor, bottom-taskbar environment after the tray measurement fix. This is not a complete platform certification. CI builds and safe self-tests do not replace interactive Explorer testing.

Multiple displays/mixed DPI, non-bottom docking, maximize/Alt+Tab fill, TranslucentTB integration, Explorer restart, and recovery after force termination remain outside the verified matrix. Use tray **Exit** for a clean restore before replacing the EXE.

This release does **not** replace WPF `v1.0.6` as GitHub Latest and is not a full replacement for that stable line.
