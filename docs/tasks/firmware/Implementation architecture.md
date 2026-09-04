# Firmware implementation architecture

This note records the repository discovery and baseline performed before implementing the
firmware roadmap. It is intentionally implementation-oriented; the product scope and safety
rules remain authoritative in `MissionPlanner.Firmware.ScopeAndRoadmap.md`.

## Existing extension points

- `MissionPlanner.Transport` owns byte transport. `ISerialMavLinkTransport` and
  `SerialMavLinkTransport` already provide serial ownership for normal MAVLink traffic.
- `MissionPlanner.MavLink` owns protocol framing, generated messages, `MavLinkConnection`, and
  the connection session. Firmware code must not duplicate MAVLink framing.
- `MissionPlanner.Core` owns vehicle sessions, `IVehicleConnectionService`, active-vehicle
  state, `ICommandAckTracker`, serial-port discovery, and the existing preliminary firmware
  catalogue/package/coordinator contracts.
- `MissionPlanner.AvaloniaUI.App` owns Avalonia views, Setup navigation, platform adapters, preferences-based
  caches, dialogs, dispatching, and composition in `ApplicationConfigurator`.
- Tests use xUnit v3 with FluentAssertions, Shouldly, NSubstitute, and Microsoft logging/DI test
  support, with versions centralized in `src/Directory.Packages.props`.

The current Core firmware implementation downloads and verifies packages and guards transition
from a connected vehicle, but its flashing adapter is deliberately unsupported. The new library
will provide the complete protocol- and platform-neutral workflow. Existing Core types should be
adapted or retired at the composition boundary rather than expanded into a second competing
workflow.

## Proposed project references

```text
MissionPlanner.Library
        ^
MissionPlanner.Transport
        ^
MissionPlanner.MavLink
        ^
MissionPlanner.Core

MissionPlanner.Firmware -> MissionPlanner.Transport
MissionPlanner.Firmware -> MissionPlanner.MavLink (only for the connected bootloader gateway)
MissionPlanner.AvaloniaUI.App      -> MissionPlanner.Core + MissionPlanner.Firmware
MissionPlanner.Firmware.Tests -> MissionPlanner.Firmware
```

`MissionPlanner.Firmware` remains `net10.0` and UI-independent. Windows device enumeration and
serial implementations will be isolated behind firmware-owned narrow interfaces and registered
by the host. The library must not reference Core because Core will eventually consume or adapt
the firmware subsystem; referencing each other would create a circular dependency. Connected
bootloader operations will use a narrow gateway implemented over the existing connection and
command/ACK services.

## Proposed namespaces

- `MissionPlanner.Firmware` — options and DI entry point.
- `MissionPlanner.Firmware.Model` — immutable public requests, results, identities, progress,
  compatibility decisions, and state values.
- `MissionPlanner.Firmware.Catalog` — manifests, caching, parsing, and package selection.
- `MissionPlanner.Firmware.Images` — APJ/PX4 parsing, decompression, and integrity checks.
- `MissionPlanner.Firmware.Devices` — device discovery models and platform-neutral contracts.
- `MissionPlanner.Firmware.Protocol` — bootloader protocol transport and uploader.
- `MissionPlanner.Firmware.Operations` — leases, state machine, orchestration, recovery, and
  connected bootloader update.
- `MissionPlanner.Firmware.Diagnostics` — structured operation records and redaction.
- `MissionPlanner.Firmware.Windows` — Windows-specific implementations, without leaking Windows
  types through public contracts.

## Ownership and safety boundaries

- Normal MAVLink and bootloader flashing never own the same serial device concurrently.
- Compatibility is evaluated before opening a destructive stage and before erase.
- Cancellation is honored through discovery/download/validation and becomes recovery guidance
  after erase begins.
- UI observes immutable state and invokes use cases; it does not contain flashing logic.
- Existing `HttpClient`, logging, DI, clock/time-provider, and filesystem capabilities are used
  directly or through existing host abstractions. No duplicate generic wrappers are introduced.

## Baseline — 2026-08-03

- `dotnet build .\Core\MissionPlanner.Core\MissionPlanner.Core.csproj --no-restore` succeeds with
  zero warnings and zero errors.
- `dotnet build .\MissionPlanner.slnx --no-restore` reaches all managed projects but fails in the
  two pre-existing Android packaging targets because `java.exe` exits with code 2:
  the Avalonia desktop application and the applicable firmware test projects.
- `dotnet test .\Tests\MissionPlanner.Core.Tests\MissionPlanner.Core.Tests.csproj --no-build
  --no-restore` reports 444 passed, 11 skipped, and 11 failed. The failures precede firmware work:
  two command cancellation-token assertions, seven recently changed vehicle-display-name
  expectations, one autopilot-version request assertion, and one mission-download timeout.

The firmware implementation will use focused builds and tests on every task. Full-solution
results will continue to be reported separately so Android tooling or unrelated baseline test
failures cannot conceal firmware regressions.
