# Config 04 — Extended tuning and advanced controller configuration

## Objective

Complete Extended Tuning with structured advanced control, navigation, estimator, and filtering groups while avoiding an unmaintainable hard-coded form.

## Dependencies

Config tasks 01 and 03.


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

Build on the tuning catalog from task 03 and metadata-driven reusable editors. Clearly separate standard and expert controls.

## Implementation requirements

1. Add advanced controller groups appropriate to each firmware family: rate/attitude PIDs, position/velocity controllers, feed-forward, filters/notches, autotune parameters, TECS/L1 for Plane, steering/speed for Rover, and depth control for Sub where present.
2. Generate repeated axis/instance editors from descriptors.
3. Add cross-field validation and normalized comparison views.
4. Support copy-axis values only with explicit preview.
5. Integrate live response metrics only as read-only context; do not implement autotune execution here.
6. Add parameter search within the curated advanced set.
7. Provide expert warnings and change summaries.
8. Ensure large forms are virtualized/lazy and responsive.

## Tests

- Descriptor expansion and family selection tests.
- PID/filter cross-field validation.
- Copy-axis preview/apply tests.
- Performance test for large descriptor sets.

## Acceptance criteria

- Advanced tuning covers relevant parameter families without duplicating Full Parameters List.
- The form remains responsive.
- Every edit is metadata validated and reviewable.
- Expert parameters are clearly identified.

## Completion

Completed 2026-07-22. Extended Tuning now selects family-specific advanced descriptors for
controllers, navigation/depth, estimators, filters/notches, and autotune configuration;
expands repeated axes and sensor instances; presence-gates every field; and edits through
the shared confirmed session. The virtualized page lazily materializes groups, searches the
curated set, validates PID/filter relationships, compares normalized axes, requires an
explicit copy-axis preview and confirmation, displays expert change summaries, and observes
live PID response metrics without executing autotune. Descriptor, validation, preview,
comparison, metrics, performance, lifecycle, and DI tests are included.
