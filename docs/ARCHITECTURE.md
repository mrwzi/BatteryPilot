# Architecture

BatteryPilot is intentionally split conceptually into three layers while the public beta stabilizes:

1. **Windows core** — power events, battery health, display topology/modes, Energy Saver, CPU power settings, saved user state, application and verification.
2. **Policy** — combines physical power source, selected mode, detected capabilities, saved state, and safety blockers into the desired configuration.
3. **OEM providers** — optional manufacturer-specific requests such as Quiet mode and GPU Eco. Unsupported OEM capabilities never reduce the Windows score.

The current beta keeps these components in one WinForms project to minimize behavioral regressions while hardware testing expands. Contributions should preserve the boundaries and move provider implementations into separate projects as additional manufacturers are added.

## Capability states

- **Working** — supported, requested where appropriate, and verified.
- **Failed** — supported and attempted, but verification failed.
- **Unsupported** — unavailable on this machine; not an error.
- **Blocked** — supported but unsafe in the current topology, such as GPU Eco with an external display.

## Safety invariant

Every mutable Windows feature must support: discover → capture previous state → apply → verify → restore. OEM providers must never use Device Manager or PnP-device disabling as a substitute for firmware/vendor controls.
