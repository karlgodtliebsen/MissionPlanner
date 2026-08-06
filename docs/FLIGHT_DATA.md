# Flight Data

## Architecture and lifecycle

Flight Data tabs use `LifecycleTabView` with `TabViewLifecycleContent<TViewModel>`. A tab
creates one transient view model when selected and disposes it when it is left. View models
own subscriptions and cancellation for that lifetime. Disposal cancels work and releases
subscriptions; it does not clear UI-bound collections.

## Preflight

Preflight is conservative operator assistance, not a declaration that an aircraft is safe
to fly. It projects promoted immutable vehicle state into explainable checks with a stable
key, category, status, evidence source and timestamp, summary, and remediation. Statuses are
`Pass`, `Warning`, `Fail`, `Stale`, and `NotAvailable`; missing evidence is never treated as
a pass. Overall status is the highest actionable severity.

Heartbeat evidence is stale after five seconds. GPS, power, system-health, and estimator
evidence is stale after ten seconds. The initial rules cover connection, firmware identity,
armed state, GPS, battery, sensor masks, and EKF health. Home, fence, and storage/logging are
reported as unavailable until cohesive state is promoted.

“Run pre-arm checks” sends typed `MAV_CMD_RUN_PREARM_CHECKS` through the acknowledged,
operation-gated command path. It requires an online, disarmed vehicle, is disabled during
telemetry replay, supports cancellation/disconnect, and captures bounded recent
`STATUSTEXT` diagnostics. An ACK means the request was accepted; the resulting diagnostics
remain separate evidence.

## Gauges and shared telemetry catalog

Gauges and Status share an explicit descriptor catalog over promoted `VehicleState`; they
never reflect over MAVLink wire records. Each descriptor owns a stable key, category, raw
accessor, observation timestamp, units, formatting, recommended gauge type and range.
Projection preserves the raw SI/domain value alongside its formatted value and represents
fresh, stale, and unavailable values explicitly.

The default dashboard contains airspeed, ground speed, relative altitude, climb rate,
heading, and battery remaining. Tiles remain stable and update in place at no more than the
configured `PlannerTelemetrySettings.DisplayRateHz`. Metric and imperial display conversion
occurs only in the projector; domain values remain SI.

## Transponder and ADS-B traffic

Peripheral discovery is keyed by MAVLink system and component ID; no fixed transponder ID
or single-device assumption is used. Component heartbeat evidence stays outside the
autopilot `VehicleState`. uAvionix status is stored for its exact source component, while a
bounded vehicle-scoped traffic store deduplicates ADS-B tracks by ICAO address and expires
them after 30 seconds. The tab makes unsupported/not-discovered state explicit and presents
observed status and nearby traffic without fabricating absent fields. Configuration and
IDENT controls remain unavailable until their response-correlation workflow is completed.
