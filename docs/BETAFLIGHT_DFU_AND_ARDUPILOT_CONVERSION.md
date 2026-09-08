# Betaflight identity and DFU conversion

## Task progress

| Task | Status |
| --- | --- |
| 01 MSP protocol | Implemented; firmware tests pass |
| 02 Identity | Pending |
| 03 Discovery integration | Pending |
| 04 ROM DFU reboot | Pending |
| 05 Physical correlation | Pending |
| 06 Conversion | Pending |
| 07 UI | Pending |
| 08 Hardware acceptance and regression | Pending |
| 09 Betaflight firmware installation | Explicitly deferred |

## Protocol boundary

`Betaflight/Protocol` in MissionPlanner.Firmware contains bounded v1/native-v2 framing and
`IBetaflightMspClient`. It does not change MAVLink parsing. Requests use the existing
`IFirmwareSerialPort` stream under exclusive caller ownership. `MspPortConnector` wraps the
existing platform factory with typed busy/unsupported/cancellation/timeout outcomes and
disposes a port that opens after the deadline. Callers must await requests sequentially and
dispose the connection. Timed-out requests close the stream to abort uncancellable native reads;
another request requires a new connection. A completed write is retained as evidence separately
from a successful response, for the later reboot/disconnect race.

Frames are bounded to 254 bytes for v1 and 1024 for native v2. Jumbo and v2-over-v1 encapsulation
are unsupported. Noise and checksum failures resynchronize without an unbounded buffer. Deadline
and caller cancellation are separate typed outcomes. Native v2 coverage is limited to framing;
no unverified extended identity request is currently sent.

Command IDs were checked on 2026-09-09 against the upstream
[v1 header](https://github.com/betaflight/betaflight/blob/master/src/main/msp/msp_protocol.h) and
[v2 header](https://github.com/betaflight/betaflight/blob/master/src/main/msp/msp_protocol_v2_betaflight.h).
The task notes' proposed `MSP2_MCU_INFO = 0x300C` does not appear in current upstream headers
or `msp.c`; it is deliberately not defined or sent. The identity task will use versioned
BOARD_INFO evidence where supported. MCU identity will never imply ArduPilot board compatibility.

## Validation

Task 01: `dotnet test src/Tests/MissionPlanner.Firmware.Tests/MissionPlanner.Firmware.Tests.csproj --no-restore -v quiet`.
Hand-authored vectors cover v1 request/reply/error, fragmented and multiple frames, noise,
corruption/resynchronization, empty payloads, bounds, and native v2. Fake streams and an explicitly
advanced clock cover request timeout/cancellation/disconnect. No physical device was opened.

Physical acceptance remains pending. No board mappings, conversion success, or Pavo 20
compatibility have been established by this work.
