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

Burst is attempted first and unsupported servers fall back to regular reads. 
The current vertical slice handles one burst response then recovers later ranges with `ReadFile`; 
multi-packet burst gap reordering and SITL validation remain follow-up. 
Upload, mutations, and packed parameters are not implemented. 

The recommended next task is full burst recovery plus ArduPilot SITL tests.
