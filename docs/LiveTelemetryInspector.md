# Live Telemetry Inspector

The shell's **Telemetry Inspector / Why?** action opens the Status/Arming panel without
navigating away from Setup or Flight Data. The right-side Avalonia drawer leaves the
uncovered page usable. Its width (360–760), selected vehicle and panel persist for the
application session. Desktop can detach the same content/ViewModel into an Ursa window.
Browser/WASM retains drawer mode and hides Detach; it never needs to create a native window.

## Data ownership and EventHub separation

VehicleSession remains the only owner of VehicleState transitions. Diagnostics retains
references to authoritative immutable snapshots plus connection metadata and evidence.
Disconnected snapshots remain inspectable; a separate diagnostic disconnected flag prevents
late state events from presenting a terminated connection as live.

The existing EventHub implements IVehicleTelemetryEventHub through an isolated asynchronous
delivery path. Domain composition uses exactly:

```csharp
services.AddSingleton<IVehicleTelemetryEventHub, EventHub>();
```

IEventHub retains its existing registration. These are distinct singleton objects.
General application event semantics are unchanged. Each telemetry subscriber has an ordered
256-item queue; overflow discards its oldest pending diagnostic samples and increments
DroppedSamples. Consumers execute away from publishers, exceptions are logged/contained,
and subscription disposal cancels active delivery and discards queued work. This is a
diagnostic delivery boundary, never the transport/parser/command processing path.

VehicleLiveDiagnostics starts in UseDomainServices, before any Inspector opens. It bridges
existing VehicleStateUpdated, VehicleConnected, VehicleDisconnected and assembled
VehicleStatusTextReceived events. Command owners publish normalized request/ACK evidence,
parameter setters publish actual write requests, and calibration/motor owners publish
workflow transitions. Publishers have no Inspector reference.

## Retention and performance

LiveTelemetryInspector configuration options:

| Setting | Default |
|---|---|
| JournalCapacity | 1000 per vehicle |
| RawCapacity | 500 per vehicle |
| ReasonLifetime | 00:00:30 |

Significant-event queues evict oldest entries. Raw observations use separate storage and
never become one journal item per packet. No Serilog scraping or tlog rereading occurs.
Current RC, output and other state changes replace snapshots; only meaningful transitions
enter the journal.

One UI timer refreshes the shared presentation at 10 Hz while open. Closing the Inspector
stops UI refresh work while collection continues. RC rows update only when their values
change. Timeline rows are replaced only when the bounded visible history changes, not on
each state revision. Raw and timeline lists are virtualized and show at most 200 rows.
No per-packet Avalonia dispatch or full VehicleState JSON serialization is used.

Raw observation reuses the protocol connection's bounded MavLinkInspectionTap through
IVehicleConnectionSession (including simulation channels). The adapter owns a 512-entry
lease per connected vehicle and normalizes samples away from receive processing. Unknown
dialect candidates remain visible as unknown/CRC-unverified; they are never treated as
validated domain state. The existing tap excludes signing-key setup traffic.
Overloaded diagnostic observers may drop samples; ordinary MAVLink processing continues.

## Arming explanations

Heartbeat IsArmed is authoritative. COMMAND_ACK Accepted is retained as an acknowledgement,
never proof of physical arming. Arm command request/ACK entries carry a shared transaction
ID and arming diagnostics retain last attempt time/result. Failed Arm: text remains
separate history.

Complete, non-truncated PreArm: messages from the owning component accumulate concrete
reasons (bounded to 32); each expires after ReasonLifetime unless refreshed. Confirmed
ready health or an armed heartbeat clears current text blockers, and reconnection clears
old current reasons. SYS_STATUS enabled/present/health flags supply fresh readiness and
RC/battery health evidence. Onboard logger state supplies confirmed logging blockers.
Missing evidence remains unknown; voltage alone never infers a chemistry-specific failsafe.

The summary is ARMED, DISARMED / READY, DISARMED / NOT READY, ARMING UNKNOWN, or CONNECTION LOST.
A disconnected or stale sample is labelled accordingly rather than represented as live.

## Panels

- **Status:** readiness/reasons, last arm attempt/failure, connection state, endpoint,
  transport, independent packet/heartbeat ages, reported link-drop evidence, mode,
  system status and firmware/hardware identity.
- **RC:** reported channel values, known RCMAP/AUX/mode assignments, configured min/trim/max,
  sample age and RSSI. Unassigned/unknown channels are dimmed. Advertised channels need
  not all move. Missing calibration stays unknown; 1000–2000 is only a bar display fallback.
- **Power:** measured voltage/current, remaining percent, consumed mAh/Wh, power rails and
  concrete battery failure evidence. No chemistry thresholds are imposed.
- **Outputs:** observed bank/output values, available SERVOx_FUNCTION mapping and selected
  motor protocol, plus recent correlated motor request/ACK/workflow evidence.
  Hardware protocol support/reboot state and physical movement are not inferred.
  ESC RPM remains unavailable when no normalized ESC telemetry exists.
- **Sensors:** detected/enabled/healthy flags, optional disabled sensor distinction, GPS,
  EKF, vibration, range-sensor presence and onboard logging evidence.
- **Raw:** newest-first timestamp, source IDs, message ID/name, verification summary and
  expandable hexadecimal payload. Name/ID and source filters plus independent follow/pause
  affect only presentation.

## Freeze, markers and context

Freeze holds displayed values and history; it does not pause domain ingestion, journaling,
tlog recording, Serilog or command processing. The frozen timestamp and continuing-collection
message remain visible. Resume immediately refreshes current state and events. Changing
vehicle leaves freeze so one vehicle's retained display cannot be confused with another.

Add Marker accepts optional bounded text and appends a Marker event. Clear Events clears
only that vehicle's diagnostic journal. Neither changes telemetry files, application logs,
Raw storage, current vehicle state or command ownership.

While already open, Setup selection suggests RC for Radio, Outputs for Motor/Servo,
Sensors for Accelerometer/Compass and Power for Battery. Flight Data suggests Status.
Suggestions never force the drawer open; a user's manual panel choice suppresses later
suggestions until the Inspector closes. Why? explicitly selects Status.

## Clipboard snapshot

Copy captures current collected diagnostics for the selected vehicle as indented JSON.
It includes capture time, connection context, the full vehicle state, arming evidence,
RC channels, output diagnostics, available parameters, and all retained journal and raw
samples (including payload hex). Events and raw samples are newest first. The export
ignores panel filters and display freeze; its scope explicitly identifies this behavior.
Retention limits are included: this is bounded diagnostic history, not an entire tlog.
Missing observations remain null, and non-finite numeric values are represented as strings.
JSON formatting runs on demand outside the ingestion lock.

## Verification

Automated tests cover separate hub singletons, slow/failing subscribers, bounded queues,
disposal, isolated vehicle state, journal eviction/concurrent access, reason expiry,
accepted ACK versus armed heartbeat, RC mapping and invalid/missing inputs, low-voltage
power evidence, unknown Raw samples/filters, output versus physical-motion distinction,
optional disabled sensors, browser-safe hosting, session selection, freeze/resume, markers,
context override and disconnected presentation.

Interactive desktop detach/resize, browser layout and physical flight-controller checks
must be verified separately; unit tests do not claim hardware validation.

Validation on 2026-09-20: the full solution builds with zero errors. Run-AllTests.ps1
passed all six .NET suites and seven JavaScript tests. After final integration review,
Core passed 739 tests (six skipped) and UI passed 153 tests. Across the suites this is
1,264 passing .NET tests, 30 skipped .NET tests and seven passing JavaScript tests.
No CS1591, CS1587 or CS1573 warnings remain in the final build log. Changes are intentionally
uncommitted for review.
