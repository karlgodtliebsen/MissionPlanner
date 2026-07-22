# Simulation 07 — Playback, diagnostics, and simulation regression suite

## Objective

Complete Simulation with telemetry-log playback architecture, diagnostic tooling, and a stable automated regression suite for simulator workflows.

## Dependencies

Simulation tasks 01–06 and Flight Data telemetry logging.


## Repository constraints

- Work only under `src/`, `docs/`, `scripts/`, and test-data folders belonging to the new solution.
- Treat `src-v.1.38/` as read-only reference material. Never modify, format, move, or include legacy files in commits.
- Preserve the existing layered architecture: wire protocol in `MissionPlanner.MavLink`, transport in `MissionPlanner.Transport`, application/domain behavior in `MissionPlanner.Core`, and MAUI presentation in `MissionPlanner.App`.
- Do not call MAVLink transports directly from views or code-behind. Use application/domain services injected into view models.
- Keep code-behind limited to view lifecycle and unavoidable platform/UI integration.
- Use CommunityToolkit.Mvvm patterns already present in the solution.
- Reuse `VehicleId`, `VehicleSession`, `VehicleRegistry`, command ACK tracking, parameter services, domain event hub, generated MAVLink messages, and decoder catalog rather than creating parallel abstractions.
- All vehicle-changing operations must be connection-aware, cancellation-aware, target the active `VehicleId`, and expose command acknowledgement or an explicit failure state.
- Add structured logging at workflow boundaries; do not log high-frequency telemetry on every update.
- Add unit tests for domain/application behavior and view-model tests. Add smoke/integration tests only where they are deterministic.
- Update DI registrations in the existing configurators and add DI validation tests.
- The solution must build with nullable warnings treated consistently with the repository.

## Scope

Playback must use a distinct replay source/session and cannot accidentally transmit commands to live hardware. Add deterministic CI tiers so local SITL tests do not hang indefinitely.

## Implementation requirements

1. Implement `.tlog` reader/indexer with play/pause/seek/speed and replay clock.
2. Feed replay frames through a read-only decoding/state pipeline isolated from live command services.
3. Clearly label Replay vs Live/Simulation everywhere and disable sends in replay mode.
4. Add diagnostics: runtime command line (redacted), versions, ports, process state, recent logs, heartbeat stats, and export bundle.
5. Build unit, fake-runtime integration, and opt-in real-SITL test tiers.
6. Give every process/network test strict startup, idle, and total timeouts and always capture logs on failure.
7. Mark environmental skips explicitly; never wait indefinitely for unavailable binaries/network.
8. Document developer commands and CI expectations.

## Tests

- Tlog indexing/seek/timing tests.
- Replay send-prohibition tests.
- Diagnostics redaction tests.
- Real-SITL tests with hard timeouts and cleanup verification.
- Repeated-run leak test.

## Acceptance criteria

- Replay works without any possibility of commanding live hardware.
- Simulation failures produce useful diagnostic bundles.
- Automated tests terminate predictably.
- CI can distinguish deterministic tests from optional SITL tests.
