# MAVFTP

MAVFTP provides reusable filesystem access over MAVLink inside `MissionPlanner.MavLink`.
`IMavFtpClient` supports session reset, file information, paged directory listing, and streaming
or buffered download for an explicit system/component/transport target.

Incoming `FILE_TRANSFER_PROTOCOL` messages use the normal decoder and EventHub pipeline.
Responses correlate by endpoint, system, component, sequence, requested opcode, and session.
Operations serialize per target. 
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
active operation.

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
one transfer. Upload, mutations, capability-state decoding, and packed parameters remain excluded.

## MAVLink 2 frame-validation fix (2026-07-23)

MAVLink 2 trailing-zero truncation applies to the complete payload, including base fields, and
retains only one byte for a non-empty message. ArduPilot commonly emits MAVFTP ACK/NAK frames with
a 9-byte wire payload even though `FILE_TRANSFER_PROTOCOL` has a 254-byte untruncated payload.
The frame parser now validates MAVLink 2 payloads against the one-byte-to-maximum wire range while
retaining the exact base-field length rule for MAVLink 1. Typed generated decoders zero-fill the
omitted bytes. This prevents valid MAVFTP responses from being discarded before response
registration and correlation.
