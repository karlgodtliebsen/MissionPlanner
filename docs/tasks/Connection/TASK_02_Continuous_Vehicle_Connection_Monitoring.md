# TASK — Continuous Vehicle Connection Monitoring and Disconnect Handling

## Goal

Continuously monitor the health of an active vehicle connection and react correctly when communication is degraded, lost, physically disconnected, or restored.

The implementation must distinguish:

```text
transport open
```

from:

```text
vehicle is actually communicating
```

A serial port/socket being open does not prove that the vehicle is alive.

## Reference behavior in original MissionPlanner

Inspect the original MissionPlanner implementation, primarily:

```text
MainV2.cs
ExtLibs/ArduPilot/CurrentState.cs
ExtLibs/ArduPilot/Mavlink/MAVLinkInterface.cs
```

Classic MissionPlanner tracks `lastvalidpacket`.

Notable behavior:

- after approximately 1 second without a valid packet it progressively attenuates displayed link quality;
- after more than 3 seconds without data it can issue a "WARNING No Data ..." warning;
- it suppresses that warning during the initial connection period and throttles repetitions;
- `CurrentState` forces link quality to zero after a longer no-data interval (10 seconds in the classic implementation);
- `MAVLinkInterface` updates `lastvalidpacket` whenever a valid MAVLink packet is received;
- the explicit serial reboot path knows that direct USB may disappear during reboot and attempts to reopen it.

Use this behavior as functional guidance, not as an architecture to copy.

Do **not** port the old `MainV2` background loop or `Thread.Sleep` logic directly.

Use asynchronous, cancellable Next Gen services.

## Existing Next Gen functionality to inspect/reuse

Before implementing, inspect the current:

- transport abstractions for Serial / UDP / TCP;
- `MavLinkConnection` receive loops/channels;
- vehicle registry/session;
- `VehicleState.Connection`;
- heartbeat handling;
- connection/disconnection domain events;
- command ACK waiting;
- parameter streaming;
- mission services;
- MAVFTP;
- telemetry recorder;
- root connection UI / connect-disconnect commands.

Do not add a second parallel concept of vehicle connection state if one already exists.

Extend the existing model cleanly.

## Required connection model

The monitor should be driven by both:

### 1. Transport state

Examples:

- serial port closed/unplugged;
- read loop ended;
- socket failure;
- transport exception;
- explicit user disconnect.

### 2. MAVLink liveness

Track the timestamp of the last **valid MAVLink frame** received for the active vehicle.

Heartbeat timestamp should continue to be tracked separately, but the liveness timeout should not declare a connection dead merely because one expected message type was temporarily absent while other valid vehicle traffic is arriving.

Use the existing frame/parser boundary to update liveness.

## Suggested state machine

Reuse existing connection-state types if possible. If the current model needs extension, support semantics equivalent to:

```text
Connecting
Online
Degraded / Stale
Disconnected
```

Suggested default thresholds based on useful classic behavior:

```text
Online -> Degraded:
    no valid vehicle packet for 3 seconds

Degraded -> Disconnected:
    no valid vehicle packet for 10 seconds

Any state -> Disconnected:
    underlying transport closes/faults definitively

Degraded -> Online:
    valid vehicle traffic resumes before disconnect timeout
```

Thresholds must be configuration/options, not magic values buried in a ViewModel.

Use a monotonic/time-provider abstraction (`TimeProvider` or project equivalent) so the logic is testable.

Do not run one tight polling loop per UI element.

A single connection-health service/timer is sufficient.

## Startup grace period

Classic MissionPlanner avoids noisy data-loss warnings immediately after connecting.

Implement equivalent notification behavior:

- state monitoring begins immediately;
- user-facing "No Data" warning is suppressed during an initial grace period, suggested default 30 seconds;
- a definite transport failure still transitions to `Disconnected` immediately;
- do not suppress real disconnect state merely to suppress a warning.

The grace period applies to notification noise, not domain truth.

## Armed vehicle warning behavior

If communication becomes degraded while the vehicle is armed:

- raise a high-visibility connection-loss warning;
- show elapsed time since last valid data;
- throttle repeated audible/toast warnings;
- do not create a new notification every timer tick.

Suggested repeat throttle:

```text
5 seconds
```

When communication resumes, clear active warning state and log recovery.

## Disconnect reaction

A transition to `Disconnected` must be handled once and published through the normal domain/application event pipeline.

Expected reactions include:

### Vehicle/domain state

- set connection state to `Disconnected`;
- preserve last known telemetry for inspection, but mark it stale/disconnected;
- do not present old telemetry as current;
- update heartbeat/last-packet timestamps consistently.

### UI

- update Connect/Disconnect controls;
- show clear connection-lost status;
- disable commands requiring an active vehicle;
- leave navigation usable;
- avoid modal-dialog storms.

### In-flight operations

Cancel/fail operations tied to that connection, including where applicable:

- pending command/ACK waiters;
- parameter download/read/write operations;
- mission transfers;
- MAVFTP requests/transfers;
- calibration workflows;
- other request/response registrations.

Prefer a per-connection cancellation token/lifetime object so every subsystem does not invent its own disconnect polling.

Results should clearly mean "connection lost", not merely generic timeout where possible.

### Telemetry logging

If telemetry recording is active:

- flush and close current `.tlog`;
- mark sidecar/session metadata appropriately;
- never leave writer active against a dead connection.

### Transport

On definitive transport failure:

- dispose/close safely;
- ensure read/write loops terminate;
- do not double-dispose or publish duplicate disconnect events.

## Recovery before hard disconnect

If traffic resumes while state is only `Degraded`:

```text
Degraded -> Online
```

without destroying and recreating the whole vehicle session.

Publish/log recovery if useful.

## Reconnection policy

Do not implement blind infinite auto-reconnect as part of this task unless an existing Next Gen policy already requires it.

Keep the design compatible with explicit reconnect policies.

In particular, preserve/support the intentional reboot case:

```text
FC reboot
-> USB serial disappears temporarily
-> same device returns
-> controlled reconnect may be attempted
```

That behavior should be initiated by the reboot workflow/reconnection policy, not by a generic monitor repeatedly opening every serial port.

A normal cable-unplug disconnect should leave the UI in a truthful disconnected state.

## UDP-specific behavior

UDP often has no meaningful physical "port closed" event.

Therefore UDP must rely primarily on MAVLink liveness timeout.

Receiving no data for the hard timeout must eventually transition the vehicle to `Disconnected` even though the local UDP socket remains open.

If traffic resumes after the session was fully disconnected, follow existing discovery/reconnection policy; do not silently resurrect disposed application operations unless explicitly supported.

## Multi-vehicle readiness

Even if the UI currently focuses on one vehicle, do not make the monitor inherently global to `SysId 1`.

Health state must be associated with:

```text
VehicleId / connection/session
```

## Suggested service responsibility

Use an application/domain service similar to:

```csharp
public interface IVehicleConnectionMonitor
{
    void Track(VehicleConnectionSession session);
    void ReportValidPacket(VehicleId vehicleId, DateTimeOffset receivedAt);
    void ReportTransportFault(VehicleConnectionId connectionId, Exception? exception = null);
    void Stop(VehicleConnectionId connectionId);
}
```

This is illustrative only; fit the existing architecture rather than forcing these names.

Prefer event-driven packet updates plus a low-frequency health timer over scanning UI state.

## Disconnect reasons

Where useful, distinguish reasons:

```csharp
public enum VehicleDisconnectReason
{
    UserRequested,
    TransportClosed,
    TransportFault,
    CommunicationTimeout,
    ApplicationShutdown,
    Reboot
}
```

Reuse an existing equivalent if present.

## Logging

Add structured logs for state transitions only, not every monitor tick:

```text
Connecting -> Online
Online -> Degraded
Degraded -> Online
Degraded -> Disconnected
Transport fault
User disconnect
Reconnect after reboot
```

Include structured properties where available:

```text
VehicleId
ConnectionId
Transport
Port/Endpoint
LastPacketAge
LastHeartbeatAge
DisconnectReason
IsArmed
```

Avoid Verbose spam every second.

## Tests

Use fake transports and a fake/controllable clock.

Cover at least:

### Healthy connection

- regular valid packets keep state `Online`;
- heartbeat and other packets update timestamps correctly.

### Degraded and recovery

```text
Online
+ 3s no packets
-> Degraded

packet arrives before 10s
-> Online
```

Ensure no disconnect cleanup runs during temporary degradation.

### Hard timeout

```text
Online
+ >10s no packets
-> Disconnected
```

Verify disconnect event emitted exactly once.

### Physical transport loss

- serial/read-loop fault causes immediate disconnected transition;
- duplicate errors do not publish repeated disconnect events.

### UDP

- open UDP socket with no MAVLink traffic eventually disconnects by liveness timeout.

### Armed warning

- degraded armed vehicle produces high-priority warning;
- repeated warning is throttled;
- recovery clears warning state.

### In-flight operations

Verify connection loss cancels/fails representative operations:

- command ACK waiter;
- parameter operation;
- MAVFTP registration/operation.

Do not wait for long generic timeouts after the connection is already known lost.

### Telemetry recorder

- disconnect flushes/stops telemetry log;
- only one stop occurs.

### Startup grace

- user-facing no-data warning suppressed during grace period;
- definite transport disconnect is not suppressed.

### Explicit user disconnect

- correct reason;
- no misleading "unexpected connection lost" warning.

## Acceptance criteria

- Active vehicle liveness is continuously monitored independently of the UI.
- An open transport is not treated as proof of a healthy vehicle connection.
- Last valid MAVLink traffic drives liveness.
- Temporary data loss produces a degraded state and can recover cleanly.
- Sustained data loss or transport failure produces exactly one disconnected transition.
- UI connection state updates automatically.
- Commands and long-running vehicle operations stop promptly on disconnect.
- Telemetry recording closes cleanly.
- Armed connection loss is clearly surfaced without notification spam.
- Serial, UDP and TCP semantics are handled correctly.
- Implementation is async/cancellable and does not port the old `MainV2` thread loop.
- Tests use a controllable clock and complete without real-time multi-second waits.
