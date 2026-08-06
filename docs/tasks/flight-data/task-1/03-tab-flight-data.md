# Flight Data 03 — Messages and status-text history

**Implementation status:** Completed 2026-07-22.

## Objective

Complete the Messages tab as a bounded, filterable, vehicle-scoped stream of MAVLink `STATUSTEXT` and important application/protocol messages.

## Dependencies

Flight Data task 01.


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

Use the existing `StatusTextMessage`, `StatusTextHandler`, `VehicleStatusText`, domain event hub, and active-vehicle context. Extend the model only for missing MAVLink 2 chunking semantics and message identity.

## Implementation requirements

1. Verify correct assembly of MAVLink 2 `STATUSTEXT` chunks using id/chunk sequence; handle MAVLink 1 single-frame messages.
2. Add a bounded per-vehicle message store with configurable capacity and timestamps.
3. Preserve severity, source system/component, text, and whether a message was assembled/truncated.
4. Add severity filtering, text search, pause/resume auto-scroll, clear-current-view, and export to UTF-8 text/JSON.
5. Visually distinguish Emergency/Alert/Critical/Error/Warning/Notice/Info/Debug without relying only on color.
6. Keep histories isolated between vehicles and stable through temporary disconnects.
7. Route relevant command/workflow failures into a separate application-notification stream; do not forge them as MAVLink status text.
8. Add copy-selected and copy-all operations.

## Tests

- Chunk assembly including duplicates, missing chunks, timeout, and interleaved IDs.
- Bounded store eviction and per-vehicle isolation.
- Filtering/search/export tests.
- View-model reconnect and auto-scroll behavior.

## Acceptance criteria

- Messages appear live and in severity order of arrival.
- Memory use is bounded.
- MAVLink text and local application messages remain distinguishable.
- Export contains complete timestamps and source identity.
