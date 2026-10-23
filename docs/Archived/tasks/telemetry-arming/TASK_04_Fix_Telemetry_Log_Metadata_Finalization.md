# TASK 04 — Fix Telemetry Log Metadata Finalization

## Goal

Make telemetry log metadata accurately reflect the recording after capture/indexing.

## Confirmed inconsistency

The latest full telemetry logger export reports metadata similar to:

```text
File size: 0
Vehicle: Unknown
Firmware: Unknown
Duration: ~28 seconds
```

while the indexed recording reports approximately:

```text
Length: 109576 bytes
PacketCount: 2586
Duration: ~35 seconds
Vehicle telemetry is clearly present
Firmware startup text and AUTOPILOT_VERSION are present
```

This is a NextGen metadata refresh/finalization defect.

## Required investigation

Trace metadata through:

```text
recording creation
-> live capture
-> file close/flush
-> index build
-> metadata extraction
-> logs list/details
-> full diagnostic export
```

Determine whether metadata is captured too early and never refreshed after the file is finalized.

## Required metadata behavior

After a recording is closed/indexed, populate:

```text
actual file size
start time
end time
duration
packet count
vehicle/system id
vehicle type
firmware family
firmware version
board/target when available
```

Use `AUTOPILOT_VERSION` and/or startup `STATUSTEXT` already present in the recording.

## Source precedence

Suggested:

```text
AUTOPILOT_VERSION -> firmware version/build identity
startup STATUSTEXT -> target/platform string
HEARTBEAT -> vehicle type/system/component
file/index -> duration/size/count
```

Do not leave `Unknown` when sufficient evidence exists in the recording.

## Duration consistency

Define one authoritative duration:

```text
IndexedLastTimestamp - IndexedFirstTimestamp
```

or another explicit documented rule.

Ensure file metadata and index metadata do not disagree silently.

## File size

Never cache size `0` from the instant the recording file is created and retain it permanently.

Refresh size after flush/close.

## Tests

Cover:

- file begins at size 0, grows, closes -> final size correct;
- duration derived after index;
- vehicle identity extracted from heartbeat;
- firmware extracted from AUTOPILOT_VERSION;
- target extracted from startup STATUSTEXT;
- missing metadata remains Unknown only when truly unavailable;
- reopened historical log shows same metadata.

## Acceptance criteria

- Logs page and exported diagnostics show correct file size.
- Duration is consistent.
- Vehicle/Firmware are populated when data exists.
- Metadata remains correct after application restart.
