# MAVLink Domain Promotion Policy

MAVLink wire coverage and MissionPlanner domain coverage are deliberately different.
Every selected-dialect packet can be parsed and decoded, but only messages that represent
durable product state or a meaningful transition belong in the vehicle aggregate. The
default for a new dialect message is diagnostic telemetry, not a new observation and
handler.

The authoritative machine-readable catalog is
[`mavlink-promotion-catalog.json`](mavlink-promotion-catalog.json). It contains all 325
messages from the pinned `ardupilotmega` include graph and records a single owner,
observation/model type when promoted, expected frequency, stale timeout, and intended UI
consumers. Regenerate it with:

```powershell
dotnet run --project src/Tools/MissionPlanner.MavLink.Generator/MissionPlanner.MavLink.Generator.csproj -- promotion . docs/mavlink-promotion-catalog.json
```

## Categories

### Vehicle-state telemetry

These messages update durable or latest-known state used by normal product surfaces: vehicle
identity, attitude, position, power, GPS, radio, landed/flight state, navigation, and
estimator health. A cohesive handler translates the wire message into an immutable
observation; `VehicleSession` owns the state transition and unit normalization. Current
values include an explicit stale timeout when an old value must not look live forever.

### Domain event

These messages represent meaningful transitions rather than continuously sampled state,
such as a mission item being reached. Domain events are published only when the transition
is meaningful; arming and connection transitions may be derived from consecutive state
observations rather than mapped one-for-one from a packet.

### Protocol workflow

Request/response packets for missions, parameters, commands, MAVFTP, logs, cameras, gimbals,
and similar protocols belong to one dedicated service or state machine. Their responses are
correlated by system/component and workflow state and are not forced into `VehicleState`.
The general vehicle dispatcher may bridge an existing response to its service, but the
service remains the owner.

### Diagnostic/raw telemetry

Typed packets that have no current product-state use remain available to inspectors,
telemetry logs, and raw-envelope subscribers. They do not trigger warning spam and do not
cause high-rate immutable aggregate churn. This is the default category for newly generated
messages until a product requirement justifies promotion.

### Outbound-only

Requests, commands, setpoints, and overrides that do not update current vehicle state are
owned by outbound APIs. A reply, acknowledgement, or resulting telemetry update is modeled
separately through its workflow or state owner.

## Domain rules

- Observations are immutable facts derived from one or more validated wire messages.
- `VehicleSession` applies observations and exclusively owns aggregate state transitions.
- Handlers translate protocol units and sentinels at the boundary; they contain no UI logic.
- One message type has at most one general domain-handler owner. Cohesive handlers own
  related families instead of creating one class per MAVLink message.
- High-rate messages do not publish expensive global state events for every sample unless a
  consumer genuinely requires that behavior. A latest-sample stream/store is preferred for
  raw IMU and similar data.
- Workflow responses remain in their protocol service and are not added to `VehicleState`
  merely because they are typed.
- UI dependencies are recorded in the catalog, but Core never references UI types.
- Promotion requires a catalog update, observation/state tests, handler tests, and a clear
  stale-data decision. Generation alone never implies domain promotion.

## Ownership validation

`MavLinkPromotionCatalogTests` verify catalog completeness and deterministic generation,
that every registered general handler maps only catalog entries naming that handler, that no
message has multiple single-owner handlers, and that every named observation/model exists.
They also exercise diagnostic raw inspection independently from aggregate dispatch.

## Implemented core telemetry promotion

Task 08 promotes the catalog's normal flight-display families through the existing cohesive
handlers. Primary UI contracts remain stable, while `GPS2_RAW` and battery instances above
zero populate explicit secondary-receiver and secondary-battery state. `EXTENDED_SYS_STATE`
provides landed and VTOL state; `SYS_STATUS` supplies sensor-health and controller-load data;
and `RADIO_STATUS`, ArduPilot `RADIO`, and `SERVO_OUTPUT_RAW` populate radio-link and actuator
telemetry. Handler publication is change-gated, and timestamped slices define stale checks
using the catalog timeouts.

## Implemented navigation and sensor promotion

Task 09 promotes low-rate navigation-quality and sensor data into estimator, vibration,
multi-instance pressure, keyed range, environment, and vehicle-time slices. High-rate IMU,
obstacle-array, optical-flow, and odometry packets remain typed diagnostic/raw traffic by
design: they stay inspectable and loggable without turning every sample into a global
immutable-state allocation. `TIMESYNC` remains a protocol workflow rather than vehicle state.

## Implemented protocol workflow ownership

Task 10 validates dedicated ownership for command, mission, parameter, log-transfer, camera,
gimbal, and peripheral families. Command acknowledgements use a singleton correlation
tracker shared by inbound and outbound paths. Mission responses remain in one transfer state
machine and are correlated by vehicle, mission type, and sequence; they never update
`VehicleState` directly. Packed MAVFTP parameter loading is an optimized transport with the
classic parameter stream as its automatic fallback.

Other selected-dialect workflow and peripheral messages are typed for inspectors and future
services, but remain diagnostic/raw or outbound-only according to the promotion catalog.
The pinned source revision does not define `MISSION_CHANGED`; that notification can only be
added by updating the official dialect revision and regenerating the catalog.
