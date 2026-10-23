# TASK 05 — Complete TelemetryLogsTabView / TelemetryLogsTabViewModel

## Goal

Make the existing telemetry log view fully functional using the new storage, reader and recorder services.

## Start point

Inspect and reuse:
- `TelemetryLogsTabView`
- `TelemetryLogsTabViewModel`

Preserve useful existing commands/layout.

## Log list

Show at least:
- display/file name;
- start timestamp;
- duration when known;
- size;
- vehicle id/name when metadata exists;
- firmware when metadata exists.

Newest first by default.

## Commands

Support:
- Refresh;
- Open/View;
- Replay;
- Import `.tlog`;
- Export/Save As;
- Delete;
- Open containing folder on desktop where supported;
- clear selection.

Browser:
- platform/browser import;
- export/download;
- no unsupported Open Folder command.

## Packet timeline

Virtualized table with useful columns:

```text
Time
Delta
SysId
CompId
Message Id
Message Name
Summary
```

Support:
- search;
- message name/id filter;
- SysId/CompId filter;
- severity/status filter where applicable;
- jump to timestamp;
- follow replay position.

Use the same MAVLink decoder registry as live operation. Unknown messages remain visible as raw messages.

## Replay

Connect to existing replay infrastructure:
- Play;
- Pause;
- Stop;
- seek;
- speed multiplier;
- current time;
- duration.

Replay must never transmit commands to a real connected vehicle.

## Diagnostics

Where practical, correlate common sequences such as:

```text
COMMAND_LONG
STATUSTEXT
COMMAND_ACK
```

Do not hide raw messages. If full transaction correlation is too large, keep a clean extension point for it.

## Performance

- asynchronous load;
- cancellation;
- virtualized rows;
- bounded decoded-page cache/indexing as needed;
- progress for long operations;
- do not eagerly materialize huge logs into heavy row ViewModels.

## Sidecar

Use `.tlog.meta.json` if available, but never require it.

## Tests

Cover:
- list/open;
- classic `.tlog`;
- Next Gen `.tlog`;
- unknown MAVLink message;
- search/filter;
- import/export;
- missing sidecar;
- truncated final record;
- cancellation;
- large synthetic log remains bounded/responsive.

## Acceptance criteria

- Viewer works without classic Mission Planner.
- Opens classic and Next Gen `.tlog`.
- Replays logs.
- Browser import/export works.
- Large logs remain responsive.
