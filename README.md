# BatteryPilot

![BatteryPilot logo](assets/batterypilot-logo.svg)

BatteryPilot is a tiny Windows tray app that applies safe, verified laptop battery settings and restores the user's previous AC configuration.

> **Project status: public beta.** The universal Windows core works on many Windows 10/11 laptops, but no software can guarantee identical behavior across every firmware, display topology, or OEM utility.

## Features

- Event-driven AC/battery switching without constant polling.
- Active internal-display discovery and dynamic refresh-rate targets.
- Saved AC refresh rate with verified battery/AC restoration.
- Verified Energy Saver, CPU battery limit, and Windows power configuration.
- Battery health and capability diagnostics.
- `AUTO`, forced `BATTERY`, and monitor-only `OFF` modes.
- Optional OEM providers; unavailable OEM controls never count as Windows failures.
- No Device Manager/PnP GPU disabling, application killing, or network telemetry.

## Install

### Direct download

[Download BatteryPilot-Setup.exe](https://github.com/mrwzi/BatteryPilot/raw/main/downloads/BatteryPilot-Setup.exe), then double-click it. No ZIP extraction or companion files are required. The standalone installer:

- requests administrator permission;
- installs under `C:\Program Files\BatteryPilot`;
- creates a Desktop shortcut;
- registers BatteryPilot in Windows Settings → Apps.

Uninstall through Windows Settings → Apps. No Windows restart is required to remove BatteryPilot.

## Support

| Capability | Status |
|---|---|
| Windows power events | Supported |
| Display discovery/switching | Supported when Windows exposes compatible modes |
| CPU DC power policy | Supported when `powercfg` exposes the settings |
| Energy Saver | Experimental across Windows versions |
| Battery health | Depends on firmware/WMI support |
| ASUS Quiet/GPU Eco | Optional through G-Helper |
| Other OEM controls | Planned |

Unknown laptops can still use safe Windows optimizations. Missing OEM integration is shown as unavailable, not failed.

## Build

Requirements: Windows and the .NET 8 SDK. Target computers require .NET Framework 4.8, included with current Windows 10/11 installations.

```powershell
scripts\Prepare-Assets.ps1
dotnet publish src\BatteryPilot\BatteryPilot.csproj -c Release -o artifacts\payload
dotnet publish src\BatteryPilot.Uninstaller\Uninstaller.csproj -c Release -o artifacts\payload
dotnet publish src\BatteryPilot.Installer\Installer.csproj -c Release -o artifacts\release /p:PayloadDirectory="$PWD\artifacts\payload"
```

The installed app is approximately 126 KB, and the complete standalone installer is approximately 334 KB. It does not bundle Electron, Node.js, Python, a browser, server, or .NET runtime.

## Safety

BatteryPilot captures restorable user state, verifies changes, blocks OEM GPU Eco with external displays, and isolates optional OEM actions behind providers. Read [SECURITY.md](SECURITY.md) before reporting a security-sensitive issue.

## License

MIT — see [LICENSE](LICENSE).
