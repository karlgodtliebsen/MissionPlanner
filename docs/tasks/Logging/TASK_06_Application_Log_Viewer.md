# TASK 06 — Structured Serilog Application Log Viewer

## Goal

Build a viewer for current and historical application logs.

## Location

Host as:

```text
Logs -> Application
```

## Live source

Use the structured in-memory Serilog sink. Do not parse the active text file for live updates.

Preserve:
- timestamp;
- level;
- SourceContext;
- template;
- rendered message;
- exception;
- structured properties.

## Historical source

Read rolled Serilog files through the logging storage/path services.

Requirements:
- enumerate historical files;
- safely read active file if sharing permits;
- tolerate partial final lines;
- expose metadata.

Do not silently change persisted Serilog format to JSON in this task.

## UI

Virtualized list/grid:

```text
Timestamp
Level
Source
Message
```

Details pane/dialog:
- full message;
- template if available;
- exception type/message/stack;
- properties;
- thread id/correlation/vehicle id if present.

## Filtering

Support:
- minimum level;
- exact level;
- SourceContext;
- text search;
- time range;
- exception-only;
- current session / historical file.

Useful quick filters:
- Warnings+
- Errors+
- MAVLink
- Transport
- Parameters
- Firmware

Prefer SourceContext/properties over message-text heuristics.

## Live behavior

Support:
- Pause;
- Resume;
- Follow tail;
- Clear view only;
- Copy;
- Export current/selected view where supported.

Batch/coalesce UI updates under high event rate.

## Runtime level

Expose Task 03 controller:

```text
Minimum logging level:
[Information | Debug | Verbose]
```

Show a clear indication when Verbose is active. Runtime override need not persist unless existing settings behavior explicitly supports it.

## File commands

- Refresh;
- Open;
- Export;
- Delete old log;
- Open folder on desktop.

Never delete the active file. Hide unsupported commands on Browser.

## Tests

Cover:
- live subscription;
- filtering;
- pause/resume;
- high-rate batching;
- exception detail;
- runtime level;
- historical enumeration;
- active-file deletion protection;
- Browser no File sink;
- malformed final line.

## Acceptance criteria

- Current-session structured logs visible live.
- Viewer remains responsive at Verbose.
- Historical rolled files open.
- Filters are useful.
- Runtime verbosity changes without restart.
- Browser live viewer works even without file logging.
