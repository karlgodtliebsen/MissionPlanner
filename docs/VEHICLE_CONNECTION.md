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
- The registered vehicle (heartbeat) is tracked by `IVehicleRegistry`
  (`Vehicles` collection of `VehicleSession`); consumers take
  `vehicleRegistry.Vehicles.FirstOrDefault()?.Id` as "the current vehicle".
- Connection status transitions (stale/degraded/offline based on heartbeat age) are driven
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
- Reconnect-on-link-loss (automatic re-establish) is not implemented — the monitor marks
  the vehicle stale/offline but does not redial.
