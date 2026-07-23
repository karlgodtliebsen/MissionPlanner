# Simulation 03 — ArduPilot SITL launch profiles and MAVLink connection

## Objective

Implement launching ArduPilot SITL for supported vehicle families and automatically connecting the new MissionPlanner transport to the correct MAVLink endpoint.

## Dependencies

Simulation tasks 01–02 and vehicle connection infrastructure.


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

Create an ArduPilot SITL runtime adapter. Use typed argument construction; never concatenate unescaped shell commands. Integrate with existing UDP transport/session ownership.

## Implementation requirements

1. Support Copter, Plane, Rover, and Sub model/frame selections verified against installed SITL help/defaults.
2. Build launch arguments for instance, home location, model/frame, speedup, defaults/parameter files, serial endpoints, and console/map options as supported.
3. Reserve ports and support multiple instances without collisions.
4. Wait for heartbeat with timeout and verify expected firmware family/SystemId.
5. Establish/offer connection using existing connection services; do not create a second MAVLink stack.
6. Distinguish simulator process running from vehicle connected.
7. Capture startup failures and actionable stderr.
8. Stop connection/session in correct ownership order.

## Tests

- Typed argument and escaping tests.
- Port allocation/multi-instance tests.
- Heartbeat verification/timeout/wrong-family tests.
- End-to-end SITL smoke tests for available families.

## Acceptance criteria

- A profile can launch SITL and connect MissionPlanner.
- Multiple instances use distinct ports/SystemIds.
- Failure messages identify launch vs connection problems.
- Disconnect/restart does not leak dispatchers/transports.

## Completion

Completed 2026-07-23. Desktop composition now uses a direct ArduPilot SITL runtime for
Copter, Plane, Rover, and Sub. A conservative family/frame catalog and typed launch settings
cover model, invariant home, speedup, instance, SystemId, defaults files, wipe, visible
console, MissionPlanner map integration, MAVLink serial zero, and validated additional
serial client endpoints. Free-form tokens cannot override typed identity or endpoints, and
the platform process host uses argument-list APIs without a shell in an isolated session
directory.

Endpoint identities are atomically leased and checked against the host. The runtime starts
the exact process, then connects through the existing UDP vehicle connection service and
verifies both SystemId and firmware family. An existing live connection fails explicitly
instead of being replaced. Opaque connection-generation identities make cleanup safe when
disconnect/reconnect races occur. Stop order is exact owned connection, exact process tree,
then endpoint lease, with cleanup attempts continuing after individual failures. Early
process exit includes bounded stderr; heartbeat timeouts are reported separately.

Deterministic tests cover token preservation and protected overrides, required-family frame
catalogs, multi-lease collisions/release, heartbeat success/timeout/wrong family, early
stderr, ownership order, view-model persistence, and DI. Opt-in real-SITL smoke cases cover
all available families with hard startup/total/cleanup bounds and explicit environmental
skips. See [SIMULATION.md](../../SIMULATION.md) for runtime details and commands.
