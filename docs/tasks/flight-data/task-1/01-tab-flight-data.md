# Flight Data 01 — Shared tab infrastructure and active-vehicle lifecycle

**Implementation status:** Completed 2026-07-22.

## Objective

Establish the common infrastructure required by every unfinished Flight Data tab: active-vehicle binding, connection state, lifecycle-safe subscriptions, command execution state, notifications, and reusable UI states.


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

Review `FlightDataView`, `FlightDataViewModel`, all tab view models, `ApplicationStateService`, vehicle registry/session events, and existing Quick/HUD implementations. Introduce only the shared primitives needed by later tasks.

## Implementation requirements

1. Add an active-vehicle context abstraction that exposes the current `VehicleId`, current immutable `VehicleState`, online/offline state, and change notifications.
2. Ensure subscriptions are disposed when a view model is deactivated or the selected vehicle changes.
3. Add reusable `AsyncOperationState`/command-result presentation models for Busy, Success, Warning, Error, Timeout, and Disconnected.
4. Add a user-notification abstraction suitable for toast/banner/dialog presentation without referencing MAUI from Core.
5. Add a base/composition helper for Flight Data tab view models; do not create a deep inheritance hierarchy.
6. Make tabs lazy-load expensive data on first activation and stop background work when hidden.
7. Replace placeholder status-bar labels in `FlightDataView.xaml` with active vehicle display name, connection state, and map/telemetry freshness.
8. Register and validate all services in DI.

## Tests

- Active vehicle changes update consumers exactly once.
- Disconnect cancels in-flight operations and leaves a stable offline snapshot.
- Reconnect does not retain subscriptions to disposed sessions.
- Tab activation/deactivation starts and stops work deterministically.
- DI graph resolves all Flight Data view models.

## Acceptance criteria

- Existing Quick and HUD behavior remains operational.
- All remaining tab view models can consume one consistent active-vehicle context.
- No tab accesses transport or `IServiceProvider` directly.
- No leaked event subscriptions across repeated navigation/reconnect cycles.
