# Flight Data 01 — Preflight readiness and pre-arm diagnostics

Status: **Completed.**

## Objective

Replace the placeholder `PreflightTabView` and minimal `PreflightTabViewModel` with an explainable, telemetry-based readiness assessment for the active vehicle. It is operator assistance and must never claim that an aircraft is safe to fly.

Apply all constraints from `00-README.md`.

## Existing implementation to reuse

```text
VehicleState.Connection / Identity / Flight
VehicleState.Gps / Power / Radio / Navigation / Health
VehicleState.Estimator / Vibration / Environment
IVehicleParameterRegistry
IVehicleMessageStore and STATUSTEXT history
MavCmd.RunPrearmChecks = 401
MAVLink command encoder and COMMAND_ACK tracking
```

## Domain/application work

1. Add explicit Core models:

   ```text
   PreflightAssessment
   PreflightCheckResult
   PreflightCheckCategory
   PreflightCheckStatus
   PreflightEvidence
   ```

2. Required statuses: `Pass`, `Warning`, `Fail`, `Stale`, and `NotAvailable`.
3. Every check must expose a stable key, category, title, summary, evidence/source, timestamp, remediation text, and optional related parameter names.
4. Add `IPreflightAssessmentService`; implement small deterministic rules rather than one large ViewModel method.
5. Overall severity is the highest actionable severity. `NotAvailable` must never become an implicit pass.
6. Use the timestamps already present in promoted state to evaluate freshness.
7. Make rules firmware-family and capability aware.

## Initial rule catalog

Implement only rules supported by real state. Mark unsupported rules `NotAvailable` and document the missing source.

```text
connection and heartbeat freshness
firmware identity available
armed/disarmed state
SYS_STATUS sensor present/enabled/health masks
GPS fix, satellites and freshness
home position availability when required
EKF status and freshness
battery voltage/remaining and warning state
RC input and radio-link freshness when relevant
compass, accelerometer and gyro health
vibration thresholds
fence state when available
mission availability when relevant
storage/logging health when available
recent ArduPilot pre-arm STATUSTEXT failures
```

## Run pre-arm checks

1. Add a typed service for generated `MavCmd.RunPrearmChecks`; prefer a dedicated `IPreflightCommandService` if that keeps `IVehicleCommandService` cohesive.
2. Require connected active vehicle, replay disabled, vehicle disarmed, and no conflicting operation.
3. Correlate `COMMAND_ACK`.
4. Capture pre-arm `STATUSTEXT` messages during a bounded request window.
5. Do not make string matching the sole source of truth.
6. Return structured ACK, timeout, cancellation, disconnect, unsupported and captured-diagnostic results.

## UI

Create a responsive tab with:

```text
overall readiness banner
last-updated timestamp
Refresh
Run pre-arm checks
category/status filters
check list with evidence and remediation
copy/export report
```

Use accessible text/icons in addition to color. Coalesce `VehicleStateUpdated` events rather than updating at packet rate.

## Lifecycle

- Subscribe only for the transient ViewModel lifetime.
- Cancel active pre-arm checks on disposal.
- Do not clear the bound check collection from `Dispose()`.
- Rebuild correctly on active-vehicle change and reconnect.

## Tests

Cover every rule boundary, severity aggregation, stale telemetry, unsupported families, command target/ACK, status-text capture window, replay denial, cancellation/disconnect, vehicle switching, throttling and disposal.

## Documentation

- Create `docs/FLIGHT_DATA.md` and link it from `docs/README.md`.
- Document readiness semantics, evidence sources, stale thresholds and the safety disclaimer.
- Update the PreFlight section in `docs/FEATURES.md`.
- Update MAVLink promotion documents only if new state is promoted.

## Acceptance criteria

- No placeholder content remains.
- Every result has explainable evidence.
- Missing/unsupported data is explicit.
- Pre-arm execution is typed, acknowledged and cancellable.
- Documentation and tests are committed with the feature.
