# Flight Data

## Architecture and lifecycle

Flight Data uses Avalonia `TabControl` with `TabItemViewBase<TViewModel>` children. The
application view base resolves each registered ViewModel and bridges the Avalonia loaded and
unloaded lifecycle to `ActivateAsync` and `DeactivateAsync`. ViewModels own their event
subscriptions and use a fresh cancellation source for each activation.

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

## Payload Control

Payload components are discovered from component heartbeats and keyed by vehicle system
and exact component ID. Camera and gimbal workflow state remains outside general autopilot
`VehicleState`. The tab never assumes a single payload or fixed component ID and commands
always encode the selected component as the MAVLink target.

The initial camera workflow supports acknowledged single-image capture and video start/stop.
The initial gimbal-manager workflow supports bounded pitch/yaw and vehicle-frame or
earth-frame yaw-lock flags; unused rates are encoded as MAVLink NaN. Writes are blocked
during replay, serialized per vehicle, cancelled on disconnect or tab disposal, and ACK is
kept distinct from observed payload state. Zoom, focus, camera mode, status promotion,
continuous pointer control, and legacy mount fallback remain explicit limitations pending
capability-information/state handlers.

## Integration and replay safety

All Flight Data tabs inherit `TabItemViewBase<TViewModel>`. Deactivation cancels work and
releases activation-owned timers and subscriptions without publishing late collection
changes. Active vehicle
changes rebuild vehicle/component selections; connection-lifetime tokens cancel outbound
work on disconnect or vehicle switch.

Every vehicle-changing tab uses a typed service, replay prohibition, and the shared
per-vehicle operation gate. “Accepted” consistently means a MAVLink ACK was received; it
does not claim that physical or component state changed. Telemetry presentation is bounded
by planner display-rate settings and stale/unavailable evidence is explicit.

## Verification matrix and troubleshooting

Deterministic automated coverage verifies Windows-target compilation, DI resolution,
catalog/policy behavior, constrained script validation, multi-component discovery, replay
policies, and lifecycle infrastructure. Future platform builds, SITL payload
plugins, real serial flight controllers, light/dark themes, touch/keyboard interaction,
disconnect/reconnect, and active-vehicle switching remain manual release checks.

If a tab shows offline or unavailable, confirm the intended vehicle is active and emitting
fresh heartbeat/state evidence. Payload and transponder tabs require component heartbeats;
no fixed component ID is assumed. A busy result means another operation owns that vehicle's
gate. A timeout means no matching ACK arrived and must not be interpreted as failure or
success of the physical action. During telemetry replay all write controls are intentionally
read-only.
