# MissionPlanner Next Gen — Logging Work Package

Execute these tasks one at a time, in order.

## Architectural decisions

- Keep telemetry logging and application logging as separate concerns.
- Give both a dedicated root navigation page named `Logs`, with `Telemetry` and `Application` subviews.
- Do not add another tab to the already crowded Flight Data tab set.
- Preserve classic Mission Planner `.tlog` compatibility.
- Desktop uses a platform path/storage abstraction; Browser/WASM must not assume an arbitrary OS filesystem.
- Serilog remains the application logging framework.
- Add a bounded structured in-memory Serilog sink for the live Application Log viewer.
- Preserve appsettings-based Serilog configuration and environment-specific levels.
- Add runtime log-level control through a `LoggingLevelSwitch` or equivalent.
- Reuse and complete `TelemetryLogsTabView` / `TelemetryLogsTabViewModel`.

## Desktop path policy

Logical root:

`<MyDocuments>/Mission Planner/logs`

Recommended layout:

```text
Mission Planner/
└── logs/
    ├── *.tlog
    ├── *.tlog.meta.json
    └── application/
        └── MissionPlanner.NextGen.Application-YYYYMMDD.log
```

If `MyDocuments` is unavailable/unwritable, resolve a writable platform-specific local application-data fallback. No ViewModel may construct paths directly.

## Browser/WASM policy

Browser/WASM has no arbitrary desktop path. Use the same storage contracts with a browser-safe backend. Minimum behavior:

- live application log viewer works from the in-memory sink;
- telemetry recording works to a browser-safe store or bounded session store;
- explicit export/download is available;
- `.tlog` import is available;
- no `File.*`, `Directory.*`, or hard-coded Windows paths in ViewModels.

## Classic `.tlog` format

Classic Mission Planner writes repeated records as:

```text
[8-byte timestamp][raw MAVLink frame]
```

Timestamp:
- unsigned 64-bit integer;
- UTC Unix epoch microseconds;
- big-endian.

Reference: `ArduPilot/MissionPlanner`, `ExtLibs/ArduPilot/Mavlink/MAVLinkInterface.cs`.

## Task order

1. `TASK_01_Logging_Architecture_And_Platform_Storage.md`
2. `TASK_02_Classic_Tlog_Compatible_Telemetry_Recorder.md`
3. `TASK_03_Serilog_InMemory_Sink_Path_Override_And_Level_Control.md`
4. `TASK_04_Logs_Root_Page_And_Navigation.md`
5. `TASK_05_Complete_Telemetry_Logs_Viewer.md`
6. `TASK_06_Application_Log_Viewer.md`
7. `TASK_07_Integration_Tests_Retention_And_Documentation.md`
