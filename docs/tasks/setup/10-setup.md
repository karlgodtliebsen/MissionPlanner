# Setup 10 — Safety, arming, geofence prerequisites, and setup summary

## Objective

Complete Setup with safety-related parameters, arming prerequisites, failsafe overview, and a consolidated exportable setup summary.

## Dependencies

Setup tasks 01–09 and Flight Data preflight assessment.


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

Aggregate existing setup outcomes and live preflight assessment. Reuse Config GeoFence rather than duplicating fence editing.

## Implementation requirements

1. Present arming checks, safety switch/options, crash/termination capabilities, and relevant firmware-family-specific safety parameters.
2. Summarize RC, battery, GCS, GPS/EKF, fence, and throttle failsafe configuration with links to detailed pages.
3. Validate contradictory or missing safety configuration using evidence-based rules.
4. Add links into Config GeoFence and other relevant setup workflows.
5. Build a setup summary containing identity, firmware, frame, calibrations, RC, battery, peripherals, safety, warnings, and timestamps.
6. Export summary as JSON and readable text/Markdown.
7. Mark unsupported/not assessed distinctly from pass.
8. Do not provide a single “safe to fly” certification.

## Tests

- Aggregation and contradiction-rule tests.
- Export snapshot tests.
- Link/navigation and vehicle-switch tests.
- Stale evidence handling.

## Acceptance criteria

- Setup has a coherent final overview.
- Safety configuration gaps are visible and actionable.
- Reports are vehicle- and firmware-identifiable.
- No unsupported check is represented as successful.
