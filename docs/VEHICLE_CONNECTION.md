# Vehicle Connection

How MissionPlanner connects to a vehicle, keeps the link healthy, and cleans up.

This document consolidates the earlier CONNECTION_CLEANUP_SOLUTION, CONNECTION_RESILIENCE,
SINGLE_CONNECTION_SIMPLIFICATION, TELEMETRY_STREAM_SOLUTION,
VEHICLE_CONNECTION_TASK_LIFECYCLE, VEHICLE_CONNECTION_SERVICE_VALIDATION and
VEHICLE_CONNECTION_BEST_PRACTICES documents, updated to match the current code
(`MissionPlanner.Core/Vehicles`, `MissionPlanner.Transport`).

---

## Single-connection model

`VehicleConnectionService` (singleton) supports **one active connection at a time**:

```csharp
public interface IVehicleConnectionService : IAsyncDisposable
{
    bool IsConnected { get; }
    IReadOnlyCollection<VehicleId> ConnectedVehicles { get; }
    Task<VehicleConnectionResult> ConnectSerialAsync(string portName, int baudRate = 57600, CancellationToken ct = default);
    Task<VehicleConnectionResult> ConnectTcpAsync(string host, int port, CancellationToken ct = default);
    Task<VehicleConnectionResult> ConnectUdpAsync(int localPort, string? remoteHost = null, int? remotePort = null, CancellationToken ct = default);
    Task DisconnectAsync(CancellationToken ct = default);
}
```

- Connecting while already connected **automatically disconnects** the previous link first;
  a `SemaphoreSlim` serializes connect/disconnect so there are no races or orphaned connections.
- Registered vehicles (heartbeats) are tracked by `IVehicleRegistry`. Application and presentation
  consumers use the singleton `IActiveVehicleContext` for the selected `VehicleId`, latest immutable
  `VehicleState`, and online state instead of selecting directly from registry order.
- Connection status transitions (online/degraded/offline based on valid packet age) are driven
  by `VehicleConnectionMonitor`.

### Connect flow

```
ConnectSerialAsync / ConnectTcpAsync / ConnectUdpAsync
    → create transport (Polly retry) → MavLinkClient → MavLinkConnection
    → start VehicleMessagePump + connection as stored background tasks
    → wait for heartbeat → vehicle registered in IVehicleRegistry
    → RequestTelemetryStreamsAsync (see below)
```

The message-pump and connection tasks are stored as instance fields and linked to a
service-level `CancellationTokenSource`, so they live for the duration of the connection
(not the method call) and are cancelled/awaited on disconnect and `DisposeAsync`.

### Existing connection access boundary

`IMavLinkClient`, `IMavLinkConnection`, and `IMavLinkConnectionSession` collectively form
the low-level connection implementation. They are created and owned by the connection
infrastructure and must not be independently injected into application UI, view models, or
feature/domain services.

The single application-facing access point to the existing connection is
`IVehicleConnectionSession`:

```text
UI / ViewModel / feature service
    -> IVehicleConnectionSession
        -> IMavLinkConnectionSession / IMavLinkConnection / IMavLinkClient
```

Any UI view model or service that needs the current active MAVLink connection must receive
`IVehicleConnectionSession` through constructor injection and use its `Connection`,
`MessagePump`, `ParameterService`, `ParameterRegistry`, `Transport`, or `Client` property as
appropriate. It must not resolve or inject any of these low-level interfaces directly:

```csharp
// Correct: reuse the application-owned connection session.
public VehicleCommandService(IVehicleConnectionSession vehicleConnectionSession)
{
    this.vehicleConnectionSession = vehicleConnectionSession;
}

// Incorrect in UI, view models, and feature/domain services.
public VehicleCommandService(
    IMavLinkClient client,
    IMavLinkConnection connection,
    IMavLinkConnectionSession connectionSession)
```

This rule prevents parallel clients, connections, message subscriptions, and transport
lifetimes from being created accidentally. Direct use of the low-level interfaces is limited
to the connection infrastructure that constructs and implements
`IVehicleConnectionSession` itself.

### Consumer connection lifetime

`ActiveVehicleContext` follows `VehicleConnected`, `VehicleStateUpdated`, `VehicleDisconnected`,
and registry-reset events. `VehicleStateUpdated` refreshes its latest immutable snapshot, but
heartbeat and telemetry updates do not raise the context's compatibility `Changed` event.
`Changed` is raised when the active vehicle changes or crosses the online boundary.
`ConnectionCancellationToken` changes only on vehicle replacement or definitive disconnect/reconnect;
degradation and recovery preserve the connection lifetime. This gives UI workflows a common cancellation
boundary without turning high-rate domain state into presentation lifecycle notifications.
Views and services that need live telemetry subscribe to `VehicleStateUpdated` directly for their
active lifetime; they do not use `IActiveVehicleContext.Changed` as a telemetry stream.

Flight Data uses Avalonia `TabControl` with `TabItemViewBase<TViewModel>`. The view base
connects loaded/unloaded state to ViewModel activation and deactivation. Each ViewModel owns
its subscriptions and background work for its activation lifetime and replaces cancelled
tokens before reactivation.
`ApplicationStateService` also derives its vehicle identity and connection flag from the active vehicle
context so application chrome and pages share one source.

---

## Transport resilience

All three transports (`SerialMavLinkTransport`, `TcpMavLinkTransport`,
`UdpMavLinkTransport` in `MissionPlanner.Transport`) wrap connection establishment in the
same Polly pipeline:

| Setting | Value |
|---|---|
| Max retry attempts | 3 |
| Base delay | 500 ms |
| Backoff | Exponential with jitter |

This absorbs transient failures: serial ports not yet released or still enumerating, TCP
handshake timeouts, UDP port binding conflicts. Retries are logged as warnings.

Serial close additionally discards the in/out buffers and waits ~100 ms so Windows fully
releases the port before a subsequent open (a chronic source of
`UnauthorizedAccessException: Access to the path 'COM10' is denied`).

---

## Telemetry streams

ArduPilot only broadcasts HEARTBEAT by default — everything else must be requested.
After the first heartbeat, `VehicleConnectionService.RequestTelemetryStreamsAsync` uses
`IMavLinkCommandService.RequestDataStreamAsync` (REQUEST_DATA_STREAM) to enable:

| Stream | Rate | Carries |
|---|---|---|
| EXTRA1 | 10 Hz | ATTITUDE (roll/pitch/yaw) |
| POSITION | 5 Hz | GLOBAL_POSITION_INT |
| EXTENDED_STATUS | 2 Hz | SYS_STATUS (battery), GPS status |
| RAW_SENSORS | 5 Hz | GPS_RAW_INT, IMU |

If telemetry stops arriving but heartbeats continue, suspect the stream requests (some
firmware configurations honor `SR_*` parameters instead).

---

## Shutdown / cleanup

- `IVehicleConnectionService` and its consumers are **singletons**, so the DI container
  owns their disposal.
- `App` hooks `Window.Destroying` and disposes the connection service, releasing the
  serial port when the app window closes. Without this, the COM port stays locked and the
  next launch fails with "access denied".

---

## Practical guidance (connecting, tests)

Message order is not guaranteed: attitude often arrives before the first heartbeat, and
heartbeat is only 1 Hz. Don't assume — wait for registration:

```csharp
var result = await connectionService.ConnectSerialAsync("COM10", 115200, ct);

// Wait (with timeout) until the vehicle appears in the registry
var deadline = DateTimeOffset.UtcNow + TimeSpan.FromSeconds(5);
VehicleId? vehicleId = null;
while (vehicleId is null && DateTimeOffset.UtcNow < deadline)
{
    vehicleId = vehicleRegistry.Vehicles.FirstOrDefault()?.Id;
    if (vehicleId is null) await Task.Delay(100, ct);
}
```

For parameter work, give the autopilot a moment after the first heartbeat (≈1 s) before
issuing PARAM_REQUEST_LIST, and use the streaming service (see PARAMETERS.md) rather than
hand-rolled request loops — it owns the subscribe-first/retry logic.

Test hygiene (serial hardware tests):

- Always `await connectionService.DisconnectAsync()` in teardown (`IAsyncLifetime.DisposeAsync`),
  then `await Task.Delay(200)` before the next test opens the port.
- Wrap every operation in a timeout (`CancellationTokenSource(TimeSpan...)`).
- Waiting for 2–3 heartbeats before asserting gives a stable link on slow radios.

---

## Status / known gaps

- Serial/USB is implemented and tested against hardware.
- TCP and UDP connect paths exist but still need real-world testing (see FEATURES.md).
- Reconnect-on-link-loss (automatic re-establish) is not implemented — the monitor performs
  owned disconnect cleanup on sustained loss, but does not redial.

## Arming readiness and retained feedback

The immutable vehicle state exposes `Arming`, owned by `VehicleSession`. Heartbeat
armed state takes precedence. Pre-arm readiness requires the SYS_STATUS pre-arm check
bit to be present, enabled, and healthy; absent/disabled checks remain unknown rather
than reporting Ready to Arm. Assembled autopilot STATUSTEXT messages retain the latest
meaningful `PreArm:` blocker and `Arm:` rejection independently of message history.
Readiness or arming clears the pre-arm blocker. The last arming rejection remains
visible until session reset/disconnect. Offline transitions clear both reasons.

The Flight Data HUD shows the arming/readiness label and both retained reasons below
the flight instruments. No parameter or command is sent by this read-only feature.

## Background stable firmware checks

After a successful connection, VehicleFirmwareUpdateService queues a bounded, cancellable
check without awaiting it in connection establishment. AUTOPILOT_VERSION supplies the
installed numeric version and firmware family. Only ArduPilot official builds are eligible;
beta, development, release-candidate, unknown and non-ArduPilot identities are skipped.

The existing IFirmwareCatalogService supplies stable releases and its cache/TTL. No second
HTTP client or parser is used. Exact target evidence comes from a complete current-connection
ArduPilot board STATUSTEXT banner (platform followed by hexadecimal hardware identifiers).
The name must equal a catalog platform, with matching vehicle family and MAV build variant.
A board ID or USB hint alone never selects an update. Missing, ambiguous, stale or truncated
board evidence produces no recommendation. Numeric comparison prevents downgrade prompts.

A non-modal shell notice shows the vehicle, target, installed version and available stable
version. View Upgrade passes the exact artifact to Install Firmware; Later dismisses it;
Release Notes opens the official ArduPilot repository notes through the platform link service.
No package is downloaded, prepared or flashed by the check. The installation workflow retains
its normal identity/compatibility, operation ownership and confirmation requirements.

Suppression is per hardware identity (or vehicle ID fallback), platform, build variant,
installed version and available version for the application session. A newer release can
notify again. Disconnect cancels work; a 45-second ceiling bounds catalog/identity waits.
Failures are diagnostic warnings, never connection failures or modal error prompts.
Controllers that do not send an exact board banner require manual catalog selection.

Verification: numeric comparison, shared-board variants, non-ArduPilot skip, missing target,
catalog failure, cancellation, repeated-update suppression and newer-release notification
have automated tests. Installer tests verify exact Stable target/version/variant selection
without firmware preparation. The original src-v.1.38 reference tree is absent in this checkout.

## Continuous connection health

VehicleConnectionMonitor is a singleton application service, with one cancellable low-frequency
timer for the active connection. It is started by the connection owner, independent of view
activation. Each watch is keyed by VehicleId and connection generation; cleanup verifies the
generation so an old watch cannot disconnect a replacement connection.

MavLinkConnectionActivity records accepted frames before decoding, with fixed per-system
counters/timestamps rather than per-frame event allocations. Invalid CRC/signatures and unknown
unverified parser candidates do not count as liveness. Any valid traffic from the vehicle's
system on that connection counts; heartbeat timestamps remain separate. TimeProvider's
monotonic timestamps drive age and warning throttling, while UTC receipt times serve the UI.
Transport read-loop completion bypasses decode backlog and cancels connection-owned operations.

The ConnectionHealth configuration section supports:

| Option | Default |
|---|---|
| DegradedAfter | 00:00:03 |
| DisconnectedAfter | 00:00:10 |
| NotificationGracePeriod | 00:00:30 |
| WarningRepeatInterval | 00:00:05 |
| PollInterval | 00:00:00.250 |

Online becomes Degraded after three seconds without valid packets and recovers when traffic
resumes. Ten seconds of silence becomes Offline (the existing model's disconnected state).
Serial EOF, TCP closure, transport failures and pipeline termination disconnect immediately;
UDP silence disconnects even when its local socket is still open. Notifications have startup
grace; domain state does not. Armed loss shows elapsed time in a persistent warning and uses
throttled Error notifications after grace. Recovery clears the warning. Only transitions are
logged at Information level, with vehicle/connection/transport, packet age, heartbeat age,
armed state and disconnect reason.

Hard disconnect goes through the existing serialized owner and VehicleDisconnected event.
Reasons include UserRequested, ApplicationShutdown, CommunicationTimeout and TransportFault.
The protocol lifetime cancels ACK waiters, parameter streams, mission transfers and MAVFTP
requests. Commands and parameter results identify connection loss; MAVFTP cancellation also
carries that message. Calibration/edit workflows receive the existing active-vehicle lifetime.
Temporary degradation does not replace that lifetime or dispose the session.

Cleanup stops protocol loops, flushes/closes the telemetry recorder and removes live registry
ownership. The active snapshot retains last telemetry with Offline status for inspection.
Delayed online events cannot revive it; a new successful connection is required. Connect/
Disconnect ownership and navigation remain available. No generic auto-reconnect or serial-port
scanning was added; intentional reboot/reconnection remains owned by existing firmware workflows.

Validation uses fake transports and ManualTimeProvider: packet versus heartbeat freshness,
other-system isolation, degradation/recovery, hard timeout, duplicate faults, notification
grace/throttle, explicit stop, active-context cancellation and late-event rejection. Real
ACK, parameter and MAVFTP waiters cancel promptly; parser/recorder tests prove monitor-triggered
flush and one stop for serial/TCP/UDP test endpoints. Hardware unplug/replug and real vehicle
firmware-server checks were not performed. The classic source tree is absent in this checkout.

Verification on 2026-09-19: the full solution builds with zero errors. Run-AllTests.ps1
passes 1,250 .NET tests (30 skipped) and seven browser JavaScript tests. The final
Core rerun after cleanup review passes 729 tests (six skipped). Physical hardware
validation remains outstanding.

## Reconnecting after identifier changes

Naming captures an immutable `VehicleReconnectTarget` before Apply. It retains the
connection generation and exact serial port/baud, TCP host/port, or UDP local/remote
settings. `IVehicleConnectionService.ReconnectAsync` closes only that generation and
shares the normal connection lock with monitor-driven teardown. It waits two seconds
for the endpoint to settle, then makes up to three attempts with two seconds between
failures and a 30-second overall deadline. Each attempt waits for the normal vehicle
heartbeat. Exclusive connect paths refuse to replace an unrelated active connection,
including one opened during the delay. No port scanning is performed.

Recovery has a separate cancellation lifetime from the disconnected vehicle. Naming
shows the existing cancellable progress overlay through teardown, retries, and fresh
identifier readback. Closing the overlay or leaving the page cancels recovery; all
completion paths dispose the overlay. Readback has a separate 12-second limit and
must confirm the requested identifiers before the UI reports them saved. Recovery
never repeats parameter writes. A manual Reconnect button supports another attempt
after failure/cancellation or a later disconnect. This is a Naming workflow, not a
global reconnect policy for ordinary connection loss.
