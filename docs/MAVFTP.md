# MAVFTP

MAVFTP provides reusable filesystem access over MAVLink inside `MissionPlanner.MavLink`.
`IMavFtpClient` supports session reset, file information, paged directory listing, and streaming
or buffered download for an explicit system/component/transport target. It also supports
streamed upload, deletion, rename, directory creation and remote CRC calculation.

Incoming `FILE_TRANSFER_PROTOCOL` messages use the normal decoder and EventHub pipeline.
Responses correlate by endpoint, system, component, sequence, requested opcode, and session.
Operations serialize per target across all client instances.
Timeouts use bounded same-sequence retries; cancellation retains `OperationCanceledException`. 
Sessions terminate in `finally` and cleanup errors do not hide the
original failure. 
Downloads validate offsets/length, leave caller streams open, and report optional
progress. 
Typed NAKs use `MavFtpRemoteException`.

Burst downloads use bounded windows. A singleton dispatcher decodes each FTP response once and
routes it to a bounded registration by target, opcode, sequence, and session. One burst
registration collects all packets until completion or timeout. Blocks may arrive out of order;
identical duplicates are ignored, while conflicting overlaps and out-of-window data are rejected.
The client computes minimal missing ranges and repairs them with ordinary `ReadFile` requests.
Each reordered window is then written sequentially, supporting non-seekable output without a
whole-file allocation. Unknown-command responses fall back to normal reads. A burst window is collected until complete, explicitly terminated, or idle-timeout; missing ranges are then recovered with ordinary `ReadFile` requests.

`IVehicleFileSystemService` resolves a `VehicleId` through the registry and hides the MAVFTP target
and transport endpoint from the application. The Config/Tuning MAVFTP tab lists and navigates
remote Unix-style paths, resets sessions, downloads a selected file with progress, and cancels an
active operation. Upload and destructive file actions require explicit selection/confirmation;
no file is uploaded merely by selecting it.

## SITL interoperability fix (2026-07-19)

UDP receive creates a new `TransportEndPoint` object for each datagram. MAVFTP response correlation
therefore requires endpoint **value equality**, not object identity. `TransportEndPoint` now compares
transport name, address, and port by value, allowing valid SITL replies to reach the active response
registration.

MAVFTP sequence numbers also follow the protocol conversation: the server responds with request
sequence + 1, and the next client request uses the sequence following the latest server response.
This is especially important after a multi-packet burst, where every reply advances the sequence.
Retries reuse the original request sequence.

Session cleanup uses a separate short timeout and bounded attempt count so cancellation is not held
up by the normal transfer retry policy. Response-queue overflow is surfaced as a protocol failure
instead of being silently treated as packet loss.

Automated SITL tests are not yet available. Manual validation requires connecting to ArduPilot
SITL, listing `/`, downloading and byte-comparing a known multi-packet file twice, and cancelling
one transfer. That historical SITL check did not cover upload or mutations. The 2026-10-24
implementation adds these operations with simulated protocol tests; real interoperability still
needs qualification.

## MAVLink 2 frame-validation fix (2026-07-23)

MAVLink 2 trailing-zero truncation applies to the complete payload, including base fields, and
retains only one byte for a non-empty message. ArduPilot commonly emits MAVFTP ACK/NAK frames with
a 9-byte wire payload even though `FILE_TRANSFER_PROTOCOL` has a 254-byte untruncated payload.
The frame parser now validates MAVLink 2 payloads against the one-byte-to-maximum wire range while
retaining the exact base-field length rule for MAVLink 1. Typed generated decoders zero-fill the
omitted bytes. This prevents valid MAVFTP responses from being discarded before response
registration and correlation.

## Navigation lifecycle resilience (2026-07-23)

The Config/Tuning MAVFTP tab owns a transient filesystem client. Navigating away stops and detaches
its delayed-refresh timer, cancels its lifetime and any active command, and waits for that command
to exit before disposing the client. Page disposal does not reset the vehicle's remote FTP
sessions, because packed-parameter loading can be using the same vehicle concurrently.

MAVFTP coordination is application-wide per target rather than local to one client. The shared
sequence store now provides the operation gate used by the file browser, recreated tab clients,
and packed-parameter clients. Reserving a request advances past both the request and its expected
response before the packet is sent; an observed response then reconciles the next sequence. A
cancelled request therefore cannot leave a recreated client reusing a request sequence that
ArduPilot may still hold in its duplicate cache.

## File management (2026-10-24)

The Config/Tuning tab offers upload, delete, rename, create directory and calculate CRC.
Uploads stream bounded blocks, validate acknowledged offsets, terminate their session and then
compare the remote file CRC with an independently accumulated MAVFTP CRC32 (initial value zero,
no final XOR). Cancellation and failed/corrupt uploads are reported explicitly. Partial files
may remain for inspection; cleanup does not silently delete them.

Paths reject embedded NUL and reserve the wire terminator. The UI checks that a new upload path
is absent before creating it, but MAVFTP cannot guarantee atomic no-overwrite against another
client racing that check. These commands do not provide transactional deployment or rollback.

Protocol tests cover upload boundaries, commands, CRC vectors, corrupt uploads and path limits.
See [CF-05 in the implementation ledger](review-20261023/IMPLEMENTATION.md) for validation and limits.

## Onboard Lua boundary

The Flight Data Scripts tab continues to execute constrained local declarative scripts.
The separate Advanced Scripting Console connects to an already installed Lua REPL applet, or
an explicitly chosen compatible legacy native REPL. Opening it does not install or execute code.

Generic MAVFTP can now upload a user-selected Lua file, but this is not a script deployment
manager: there is no atomic replacement, automated activation, rollback or boot verification.
Review compatibility and the selected target/path before any upload.
