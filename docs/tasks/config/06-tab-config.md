# Config 06 — Planner application preferences

## Objective

Complete Planner configuration as local MissionPlanner application preferences, clearly separated from vehicle parameters.

## Dependencies

Config task 01 only for consistent navigation; otherwise independent.


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

Implement `PlannerTabView` with a typed settings service backed by MAUI Preferences/configuration as appropriate. Include migration/versioning and immediate preview where safe.

## Implementation requirements

1. Inventory current `appsettings.json`, theme, map, connection, logging, units, update, and UI preferences.
2. Define typed settings sections with defaults and validation.
3. Implement units, map provider/style/default zoom, telemetry display rates, theme, logging level/retention, connection defaults, parameter cache policy, and confirmation preferences.
4. Mark settings requiring restart and apply live settings through observable options/services.
5. Keep credentials/secrets in secure storage, never plain preferences/logs.
6. Add reset section/all, import/export with secret exclusion, and schema migration.
7. Ensure settings are platform-safe.
8. Add accessibility options relevant to telemetry display.

## Tests

- Default/migration/validation tests.
- Import/export and secret-exclusion tests.
- Live option update tests.
- Corrupt settings recovery.

## Acceptance criteria

- Planner tab changes application settings, not FC parameters.
- Settings persist and migrate safely.
- Secrets are excluded from export/logging.
- Restart-required changes are clear.
