# Flight Data 04 — Preflight checks and readiness assessment

## Objective

Complete the Preflight tab with an explainable readiness model derived from ArduPilot pre-arm telemetry, health flags, EKF/GPS/battery state, parameters, and status text.

## Dependencies

Flight Data tasks 01 and 03; generated telemetry handlers from the MAVLink task series.


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

Build a domain-level preflight assessment service. Reuse `SYS_STATUS`, `EKF_STATUS_REPORT`, GPS observations, battery state, vibration, fence state, home position, heartbeat mode/armed state, parameter registry, and status text. Do not scrape rendered text from another tab.

## Implementation requirements

1. Define check categories: connection, firmware identity, sensor health, GPS/home, EKF, battery, RC, compass, accelerometers, fence, mission, storage/logging, and arming checks.
2. Model each result as Pass, Warning, Fail, NotAvailable, or Stale with evidence and remediation text.
3. Subscribe to state changes and recompute incrementally with throttling.
4. Incorporate ArduPilot pre-arm `STATUSTEXT` messages without making string matching the sole truth source.
5. Add Refresh, Run Pre-arm Check command where supported, and copy/export report.
6. Show last-updated time and stale telemetry indicators.
7. Make checks firmware-family/capability aware.
8. Never claim the aircraft is safe to fly; label the result as telemetry-based readiness assistance.

## Tests

- Deterministic rule tests for every check and stale-data boundary.
- Mixed states produce correct overall severity.
- Vehicle switch/disconnect tests.
- SITL smoke test against healthy and deliberately misconfigured states where feasible.

## Acceptance criteria

- The tab provides actionable checks rather than raw values.
- Every failure shows its evidence/source.
- Unsupported checks show NotAvailable, not false Pass.
- No safety decision depends solely on UI state.
