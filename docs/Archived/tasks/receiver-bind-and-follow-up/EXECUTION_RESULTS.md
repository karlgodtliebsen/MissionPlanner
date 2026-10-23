# Receiver bind and follow-up execution

Implemented in numbered order, without staging or committing. No hardware commands were sent;
COM12 was not rebooted, bound, erased or flashed.

1. Receiver bind: typed safety-gated MAV_CMD_START_RX_PAIR using the existing ACK tracker.
   Explicit CRSF configuration is Expected capability; unknown configuration stays unavailable.
2. Radio Setup: icon action, protocol/capability explanation, accurate ACK states, bounded
   RC input loss/recovery observation, correlated Inspector events and Lua fallback.
3. Embedded bootloader: shared identity policy now blocks unknown, target-mismatched and
   board-mismatched running firmware, including speedybeef4/134 vs omnibusf4/1002.
   The service rechecks current connected identity. Combined with_bl DFU images disable
   the separate update and explain that the bootloader is already included.
4. Raw capture: complete frame and payload bytes, explicit lengths, CRC verification,
   signature presence/verification, direction, dialect lengths and decoded message identity
   are retained/exported. Details explicitly identify the complete wire payload.

## Raw capture investigation

The reported 3–4-byte payloads alone do not demonstrate corruption. MAVLink 2 removes
trailing zero bytes even from base fields. A timestamp-only ATTITUDE payload of 92E16E is a
valid 3-byte wire payload; omitted bytes represent zeros. CRC validation and complete-frame
evidence distinguish this from an incomplete capture.

Trace inspected: transport read -> parser -> validated frame -> decoder -> connection
inspection tap -> VehicleRawDiagnosticsSource -> bounded raw journal -> JSON export.
The parser already copies complete frames and payloads out of its mutable read buffer.
The decoder receives that same frame and the inspection observation carries its result.
No slicing or lost bytes were found in this path. Some custom decoders decline very short
payloads and the production pipeline retains them through its lossless raw fallback.
DecodedPayloadLength is dialect schema length, not a claim that a typed decoder succeeded.

Capture now takes immutable diagnostic copies and exports the exact full frame as well as
the existing hexadecimal payload. Wire bytes are never padded to make their reported length
match a dialect base length. Parser, recording tap and .tlog serialization are unchanged.

Regression tests cover HEARTBEAT, ATTITUDE, RAW_IMU, RC_CHANNELS, SERVO_OUTPUT_RAW,
GLOBAL_POSITION_INT, STATUSTEXT and COMMAND_ACK in v1/v2 and signed v2, including
read-buffer overwrite, parser reuse, capture-boundary ownership and complete JSON export.
Short valid v2 examples are specifically covered.

See [receiver binding](../../RECEIVER_BIND.md) and
[MAVLink serialization](https://mavlink.io/en/guide/serialization.html#payload_truncation).

## Verification

- `src/Tests/Run-AllTests.ps1`: all six .NET suites passed, 1,376 passed / 30 skipped;
  browser JavaScript: 7 passed.
- `dotnet build src/MissionPlanner.slnx --no-restore -p:UsedAvaloniaProducts= -v:q`:
  passed, 0 errors, 50 existing/unrelated warnings. No CS1591, CS1587 or CS1573 warnings.
- After final cancellation/notification adjustments, receiver Core and UI regressions
  were rerun successfully (14 Core and 6 UI tests).
- `git diff --check`: passed.
- Full test artifacts: `TestResults/all-tests/20260921-035110-944`.
- Physical receiver binding and interactive visual checks were not performed. COM12 remains unchanged.
- Nothing was staged or committed.
