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

`HeartbeatMessage` retains exactly one dispatcher owner: `HeartbeatVehicleHandler`. That
handler updates both the vehicle registry and peripheral component discovery. The separate
peripheral handler owns only ADS-B/uAvionix messages, preventing duplicate-handler failure
during USB, UDP, or TCP connection startup.


## Status

Status uses the same explicit promoted-state descriptors as Gauges. Rows are created once,
sorted stably, updated in place at the configured display rate, and searchable by label or
category. Raw SI/domain values, formatted values, units, freshness and observation time are
kept distinct. Its versioned JSON export contains capture and vehicle identity plus those
field values; it contains no transport credentials. Status is not a MAVLink packet inspector.

## Servo and relay

Servo output telemetry is presented as observed PWM, separately from requested commands.
Typed set-servo and set-relay workflows validate channel/value bounds, require a disarmed
vehicle and explicit confirmation, use the shared acknowledged operation path, and block
replay. An ACK is reported as accepted but unconfirmed; it is never presented as measured
output. Motor-function mapping and relay-status promotion remain explicit limitations.

## Auxiliary functions

Aux Function sends generated `MAV_CMD_DO_AUX_FUNCTION` commands through the shared
acknowledged and per-vehicle operation-gated command path. The reviewed catalog classifies
functions as safe, warning, or high risk and describes three-position or momentary switch
semantics. Warning and high-risk actions require explicit confirmation. An ACK confirms
only command acceptance, never the resulting switch state.

The generic catalog is deliberately not a second Actions menu. Arm, mode, takeoff, land,
RTL, servo/relay, camera, and gimbal operations point to their typed workflow. Emergency
motor stop, GPS disable, parachute release, unknown IDs, and other unreviewed hazardous
functions are shown as unavailable rather than exposed as one-click actions. Catalog
contents can differ by firmware; unknown parameter-derived IDs must remain identifiable
and disabled until reviewed.

## Scripts

Scripts are versioned declarative JSON documents, not arbitrary C#, Python, or Lua. The
engine validates the complete document before running, resolves every action through an
allow-list, executes sequentially through typed services, applies a bounded timeout per
step, links cancellation to the active vehicle connection, stops on the first failure,
and retains a bounded ordered log. Dry run performs full validation without execution.

Version 1 permits `notify`, bounded `delay`, `waitForConnection`, `arm`, `disarm`, `land`,
`rtl`, `hold`, and reviewed `auxFunction` steps. Arbitrary MAVLink command IDs, IO,
reflection, dynamic compilation, loops, and parallel execution are prohibited. See
`VEHICLE_SCRIPTS.md` for the schema.

