# RoundedTB Light (lightweight host)

An **experimental** lightweight Windows taskbar host for RoundedTB. It runs a
small .NET Framework tray process that applies rounded-corner / segment regions
to the Windows 11 taskbar, plus a settings window (`RoundedTB.exe`) that is
started only when needed and exits when closed.

This is a pre-release. See **Known limitations** before relying on it.

## Requirements

- Windows 11.
- .NET Framework 4.8 (included with Windows 11) for the host.
- The settings window (`RoundedTB.exe`) is framework-dependent and needs the
  **.NET 6 or newer Windows Desktop Runtime** (it uses major roll-forward).

## Run

1. Extract the zip to a folder of your choice (portable; no installer).
2. Run `RoundedTBHost.exe`.
3. Use the tray icon (right-click) for:
   - **Tray hover** / **App segment** – toggle segments (saved to config).
   - **Settings...** – open the settings window; Apply writes the config and the
     host reloads it. Closing the settings window exits it.
   - **Start with Windows** – toggle the per-user startup entry (off by default).
   - **Exit** – stop the host and restore the taskbar.

Configuration: `%LOCALAPPDATA%\rtb.json` (shared with the WPF build).
Log: `probe.log` in this folder.

Only one host instance runs at a time; launching it again opens the running
instance's settings window.

## Known limitations (pre-release)

Verified on a single monitor with a bottom taskbar: bottom-edge hover reveal,
tray-menu toggles, settings apply/reload, and normal-exit restore.

Not yet verified / not supported:
- Multiple monitors and mixed DPI.
- Taskbars docked on the top or sides.
- Maximise / Alt+Tab taskbar fill behaviour.
- TranslucentTB compatibility.
- Explorer restart handling.
- Recovery after the host is force-killed (normal Exit restores the taskbar).

## Uninstall / rollback

- Exit the host from the tray menu (this restores the taskbar).
- Turn off **Start with Windows** before deleting the folder.
- Delete the folder to remove it. `%LOCALAPPDATA%\rtb.json` is left in place.

## Troubleshooting

If the taskbar does not respond, press `Ctrl+Shift+Esc` to open Task Manager,
end `RoundedTBHost.exe`, and run the host again. No permanent system changes are
made beyond the optional per-user startup entry.

## License

GPL-3.0 (see `LICENSE`). Bundled third-party components are listed in
`THIRD-PARTY-NOTICES.txt`.
