# TASK 07 — Logging Integration, Retention and Documentation

## Goal

Finish the subsystem as an integrated feature and prevent regressions.

## Desktop end-to-end validation

Verify:
1. app starts;
2. Serilog file path resolves through platform service;
3. vehicle connects;
4. `.tlog` starts;
5. telemetry is received;
6. disconnect flushes;
7. Logs -> Telemetry lists it;
8. viewer opens/replays it;
9. Logs -> Application shows live structured events;
10. historical app log is visible after restart.

## Browser/WASM validation

Verify:
1. app starts without File sink;
2. Application viewer gets structured in-memory events;
3. telemetry records to browser-safe/session store;
4. `.tlog` exports;
5. exported `.tlog` imports again;
6. no desktop-path exceptions.

## Retention

Application logs: remain appsettings/Serilog driven.

Telemetry logs: **do not auto-delete by default**. Telemetry is user data. Any future retention policy must be opt-in and visible.

## Logging health summary

Expose a lightweight status model:

```text
Telemetry Recording
  Recording / Stopped / Error
  Current log name
  Bytes written
  Started at

Application Logging
  Current minimum level
  File logging enabled/disabled
  Current file if desktop
  In-memory buffer count/capacity
```

May be shown on Logs root page.

## Enrichment

Where centrally available, add structured properties such as:

```text
VehicleId
SystemId
ComponentId
Transport
ConnectionId
MavLinkMessageId
MavLinkCommand
```

Use scopes/enrichers in central paths. Do not rewrite every log statement.

## Documentation

Create/update `docs/Logging.md` covering:
- telemetry vs application logging;
- `.tlog` binary format;
- recording point;
- desktop paths;
- Browser behavior;
- sidecar metadata;
- Serilog config;
- environment-specific verbosity;
- runtime level switch;
- retention;
- import/export;
- distinction between:
  - GCS telemetry `.tlog`;
  - Next Gen Serilog application log;
  - vehicle onboard DataFlash/file logging.

## Automated integration tests

Prove:
- recorder output readable by Next Gen reader;
- classic `.tlog` fixture readable;
- generated binary prefix matches classic format;
- in-memory sink + viewer integration;
- desktop path override;
- Browser DI composition;
- Browser Logs page needs no desktop-only service.

Build normal supported targets, including Browser/WASM when part of CI.

## Acceptance criteria

- Logging is documented.
- Desktop/Browser behavior explicit and tested.
- Telemetry logs are not auto-deleted by default.
- Application rolling retention stays appsettings-driven.
- Three logging concepts are clearly distinguished.
- Relevant builds/tests pass.
