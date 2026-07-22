# Config 03 — Basic tuning by firmware family

## Objective

Complete Basic Tuning with curated, metadata-backed parameter groups for Copter, Plane, Rover, and Sub rather than a static universal form.

## Dependencies

Config task 01.


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

Create tuning profiles/catalogs keyed by firmware family and parameter presence. Use the shared editing session and live telemetry where useful.

## Implementation requirements

1. Define curated basic groups such as responsiveness, climb/descent, navigation speed, throttle/cruise, loiter/turn behavior, and pilot feel.
2. Map fields to official parameter names with version-aware aliases only when justified.
3. Derive editors from metadata types/ranges/units and provide plain-language explanations.
4. Show current/live/pending values and recommended/default values only when authoritative.
5. Add coupled validation rules such as min/max ordering and acceleration/speed consistency.
6. Allow per-group apply/revert and export/import of only presented parameters.
7. Hide absent or expert-only fields.
8. Add explicit warnings where tuning can destabilize control.

## Tests

- Catalog selection per firmware family.
- Parameter-presence and alias tests.
- Coupled validation tests.
- Import/export and apply/readback tests.

## Acceptance criteria

- Basic Tuning is useful on each supported family.
- No raw numeric fields lack units/descriptions.
- Unsupported parameters are not displayed.
- Writes use the shared safe editing session.
