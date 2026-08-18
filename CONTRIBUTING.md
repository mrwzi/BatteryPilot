# Contributing

Thank you for helping make BatteryPilot safer across more hardware.

1. Open an issue describing the laptop manufacturer, model, Windows version, and observed capability report.
2. Keep universal Windows behavior separate from OEM-specific providers.
3. Do not add Device Manager, PnP-device disabling, application killing, or undocumented destructive controls.
4. Every setting change must have detection, application, verification, and restoration behavior.
5. Build with `dotnet publish src\BatteryPilot\BatteryPilot.csproj -c Release` before submitting a pull request.

OEM providers should report Working, Failed, Unsupported, or Blocked without making unsupported hardware look broken.
