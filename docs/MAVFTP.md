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
whole-file allocation. Unknown-command and repeated burst timeouts fall back once to normal reads.

`IVehicleFileSystemService` resolves a `VehicleId` through the registry and hides the MAVFTP target
and transport endpoint from the application. The Config/Tuning MAVFTP tab lists and navigates
remote Unix-style paths, resets sessions, downloads a selected file with progress, and cancels an
active operation.

Automated SITL tests are not yet available. Manual validation requires connecting to ArduPilot
SITL, listing `/`, downloading and byte-comparing a known multi-packet file twice, and cancelling
one transfer. Upload, mutations, capability-state decoding, and packed parameters remain excluded.
