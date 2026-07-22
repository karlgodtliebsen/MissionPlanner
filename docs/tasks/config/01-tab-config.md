# Config 01 — Shared parameter editing session and navigation consistency

## Objective

Create a shared, safe parameter-editing session used by all Config tabs while preserving the completed MAV FTP and Full Parameters List behavior.


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

Review `AppShell.xaml`, `FullParametersListTabViewModel`, parameter registry/service/metadata, file handler, and placeholder Config views. Build reusable parameter-field models, dirty tracking, validation, write batching, readback, reboot aggregation, and vehicle lifecycle handling.

## Implementation requirements

1. Add a configuration editing session scoped to active vehicle and firmware identity.
2. Model original/live/pending values, validation, modified state, write status, and reboot required.
3. Support grouped apply, revert, refresh, and confirmed readback.
4. Prevent writes to stale/disconnected/different vehicle sessions.
5. Reuse metadata ranges, increments, enums, bitmasks, units, read-only flags, and descriptions.
6. Add parameter aliases/presence predicates for firmware variation without silent guessing.
7. Ensure navigation warns about unapplied changes.
8. Refactor Full Parameters List only as necessary to share the service; preserve existing features/tests.

## Tests

- Dirty tracking, validation, apply/revert/readback tests.
- Vehicle switch/disconnect with pending edits.
- Duplicate/partial write failure tests.
- Existing Full Parameters List tests remain green.

## Acceptance criteria

- Every later Config page uses one consistent editing model.
- No config page writes parameters directly from code-behind.
- Partial failures remain visible and retryable.
- MAV FTP and Full Parameters List continue working.

## Completion

Completed 2026-07-22. The shared Core editing session, active-vehicle/firmware lifecycle
guard, metadata validation, alias/presence definitions, grouped confirmed apply/revert/
refresh behavior, Config navigation warning, Full Parameters List integration, DI
registrations, and deterministic tests are implemented.
