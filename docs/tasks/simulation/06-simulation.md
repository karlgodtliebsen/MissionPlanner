# Simulation 06 — Multi-vehicle simulation and instance orchestration

## Objective

Support launching, displaying, and controlling multiple SITL instances while preserving strict vehicle/session isolation.

## Dependencies

Simulation tasks 01–05.


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

Extend simulation runtime/session management and existing vehicle registry. Do not introduce global “current vehicle” assumptions into services.

## Implementation requirements

1. Allocate instance numbers, SystemIds, component endpoints, ports, home offsets, and log directories deterministically.
2. Show all sessions with process state, vehicle identity, connection state, and active selection.
3. Add start-all/stop-all with bounded concurrency and per-session failure reporting.
4. Ensure commands/scenarios explicitly target a session/VehicleId.
5. Add formation/offset launch profiles only as data; do not implement autonomous swarm control here.
6. Isolate stdout, tlogs, DataFlash logs, caches, and parameter state per instance.
7. Handle one instance crash without stopping unrelated sessions.
8. Add cleanup/recovery for orphaned owned sessions from an unclean application exit where safely identifiable.

## Tests

- Deterministic allocation and collision tests.
- Isolation tests for commands/events/logs.
- Partial start/stop failure tests.
- Crash/recovery tests.

## Acceptance criteria

- Multiple SITL vehicles can run concurrently.
- No telemetry or command crosses vehicle boundaries.
- Individual failure does not collapse the workspace.
- Resources are uniquely allocated and cleaned up.
