# Flight Data missing-tab completion tasks

Source baseline: `MissionPlanner-202600806-v1`.

Status: **Completed 2026-08-07.** Tasks 01–09 were implemented sequentially with separate
commits. Known limitations are recorded in `docs/FLIGHT_DATA.md` and `docs/FEATURES.md`.

Tabs covered:

```text
Preflight
Gauges
Transponder
Status
Servo/Relay
Aux Function
Scripts
Payload Control
```

Execution order:

```text
01 Preflight
02 Gauges and shared telemetry catalog
03 Transponder and shared component discovery
04 Status using the telemetry catalog
05 Servo/Relay
06 Aux Function
07 Scripts
08 Payload Control using component discovery
09 Integration, documentation and completion audit
```

Commit and verify after each task. Do not run the complete set in one Codex session.

## Common repository constraints

- Modify only the new solution under `src/`, `docs/`, `scripts/`, and test-data folders.
- Treat `src-v.1.38/` as read-only reference material. Never modify or include legacy files in commits.
- Preserve layering: MAVLink wire protocol in `MissionPlanner.MavLink`, connection/transport ownership in `MissionPlanner.Transport`, domain/application workflows in `MissionPlanner.Core`, and Avalonia presentation in `MissionPlanner.AvaloniaUI.App`.
- Views and code-behind must not send MAVLink or resolve services directly.
- Reuse the existing generated `ardupilotmega` dialect, `IActiveVehicleContext`, `IDomainEventHub`, `AsyncOperationRunner`, `IVehicleOperationGate`, confirmation/notification services, parameter services, command ACK tracking and replay safety.
- Use the existing transient tab lifecycle: `Avalonia TabControl` and `TabItemViewBase<TViewModel>`.
- `Dispose()` cancels work, releases leases/timers and unsubscribes events. It must not clear or mutate UI-bound collections.
- All outbound operations must target the current vehicle/component, support cancellation/disconnect, be serialized when required, and be prohibited during telemetry-log replay.
- Promote cohesive current state into domain aggregates. Keep component request/response workflows in dedicated services.
- Throttle/coalesce UI updates using `PlannerTelemetrySettings.DisplayRateHz`; do not render at packet rate.
- Add structured boundary logs, deterministic unit/view-model tests, DI validation, and only bounded opt-in SITL tests.
- Keep nullable/analyzer settings green.

## Documentation policy

- Task 01 creates `docs/FLIGHT_DATA.md` and adds it to `docs/README.md`.
- Every task updates its section in `docs/FLIGHT_DATA.md` and its status in `docs/FEATURES.md`.
- MAVLink ownership changes update `docs/MAVLINK_DOMAIN_PROMOTION.md` and `docs/mavlink-promotion-catalog.json`.
- Update `docs/MAVLINK.md`, `docs/PARAMETERS.md`, `docs/PLANNER_SETTINGS.md`, `docs/MAVFTP.md`, and ADRs whenever the implementation changes those contracts.
