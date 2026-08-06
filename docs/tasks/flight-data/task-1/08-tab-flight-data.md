# Flight Data 08 — Scripts, telemetry logs, and DataFlash logs

## Objective

Complete the Scripts, Telemetry Logs, and DataFlash Logs tabs with cross-platform storage abstractions, bounded operations, progress, cancellation, and clear separation between local telemetry recording and FC-resident logs.

## Dependencies

Flight Data task 01, MAVFTP ownership fix, and generated log-protocol coverage.


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

Use `MAVFTP` only when capability-supported, legacy log protocols otherwise, and existing storage/file-system abstractions. Scripts must begin as a safe local automation facility; do not embed arbitrary unrestricted code execution into the MAUI process.

## Implementation requirements

1. Telemetry Logs: implement start/stop local `.tlog` recording of raw MAVLink frames with metadata, file naming, duration/size, reveal/open folder, and retention settings.
2. Add playback/export architecture hooks without coupling playback into the live connection in this task.
3. DataFlash Logs: list remote logs using supported log protocol or filesystem path, show size/date where available, download with resumable/progress/cancel behavior, erase only with confirmation, and open/reveal local copy.
4. Scripts: define a constrained command-script format or existing safe scripting abstraction with explicit permitted actions, validation, dry run, cancellation, and execution log.
5. Do not run untrusted C#/Python/Lua inside the application process.
6. Ensure local paths use MAUI storage abstractions and are platform-safe.
7. Serialize bulk transfers and coordinate bandwidth with parameter/MAVFTP operations.
8. Add diagnostics and recovery for partial files and disconnects.

## Tests

- Tlog writer integrity and lifecycle tests.
- Log-list/download resume/cancel tests using fake vehicle/file services.
- Script parser/validator and forbidden-operation tests.
- Storage path and filename sanitization tests.
- Disconnect during each operation.

## Acceptance criteria

- All three tabs are functional.
- Local telemetry logs are valid and closed cleanly.
- FC logs can be listed/downloaded without UI blocking.
- Script execution is constrained, observable, and cancellable.
- Destructive log erase requires explicit confirmation.
