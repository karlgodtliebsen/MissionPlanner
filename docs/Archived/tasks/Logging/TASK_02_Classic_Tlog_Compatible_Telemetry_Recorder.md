# TASK 02 — Classic Mission Planner Compatible Telemetry Recorder

## Goal

Record live vehicle telemetry in classic Mission Planner `.tlog` format and make recording lifecycle-safe across Serial, UDP and TCP.

## Binary format

Each record is:

```text
[8-byte timestamp][raw MAVLink frame]
```

Timestamp:
- `UInt64`;
- UTC Unix epoch microseconds;
- big-endian.

Implement endian handling explicitly.

## Recording point

Record the validated raw incoming MAVLink frame **before domain decoding**.

Do not reconstruct packets from decoded messages.

Reasons:
- future decoders can decode historical logs;
- unknown messages remain intact;
- historical bytes are independent of current domain handlers.

For strict compatibility, `.tlog` contains received telemetry frames only. Do not add direction bytes or a Next Gen header.

## Services

Introduce or complete responsibilities equivalent to:

```csharp
public interface ITelemetryRecorder
{
    bool IsRecording { get; }
    Task StartAsync(VehicleId vehicleId, TelemetryRecordingContext context, CancellationToken cancellationToken = default);
    ValueTask RecordReceivedFrameAsync(ReadOnlyMemory<byte> frame, DateTimeOffset receivedAt, CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}
```

and a corresponding `ITelemetryLogReader`.

Reuse existing solution types where appropriate.

## File naming

```text
yyyy-MM-dd HH-mm-ss.tlog
```

Collision-safe suffixes:
`-1`, `-2`, etc.

Use `ILogStorage`.

## Optional metadata sidecar

Create optional:

```text
yyyy-MM-dd HH-mm-ss.tlog.meta.json
```

Suggested metadata:
- vehicle id/name;
- board/platform;
- firmware family/version;
- transport;
- application version;
- UTC start/end;
- clean/unclean close.

The sidecar must never be required to read the `.tlog`.

## Lifecycle

- Start recording automatically on live vehicle connection.
- Stop/flush on disconnect.
- Stop/flush on application shutdown.
- Recording failure must not crash the vehicle connection.
- Avoid zero-byte abandoned logs where practical.
- Recorder lifetime is independent of UI lifetime.

## Buffering

Use buffered, serialized writes with bounded buffering/channel semantics. No unbounded memory growth. Flush gracefully on stop.

## Tests

Add binary-format tests:
- exact big-endian timestamp bytes;
- raw MAVLink frame unchanged;
- multiple records;
- MAVLink v1/v2;
- signed MAVLink v2 unchanged;
- stop/cancellation flush;
- collision naming;
- unknown message ids round-trip.

Use a small classic `.tlog` fixture if suitable.

## Acceptance criteria

- `.tlog` starts automatically for live connections.
- Classic Mission Planner can read it.
- Next Gen reader/replay can read it.
- Recording happens before domain decoding.
- Disconnect/shutdown closes cleanly.
- Browser target records through its storage adapter.
- No custom `.tlog` header.
