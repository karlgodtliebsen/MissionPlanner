# TASK 03 — Serilog In-Memory Sink, File Path Override and Runtime Level Control

## Goal

Keep appsettings-driven Serilog configuration, add a structured bounded in-memory sink, resolve the File sink path through platform services, and add runtime verbosity control.

## A. Structured in-memory sink

Implement a sink with a model similar to:

```csharp
public sealed record ApplicationLogEntry(
    DateTimeOffset Timestamp,
    LogEventLevel Level,
    string MessageTemplate,
    string RenderedMessage,
    string? SourceContext,
    Exception? Exception,
    IReadOnlyDictionary<string, object?> Properties);
```

Requirements:
- thread-safe;
- bounded capacity;
- configurable capacity;
- oldest entries discarded when full;
- snapshot API;
- event/observable/change notification;
- viewer cannot block Serilog;
- no UI dependency.

## B. Preserve appsettings setup

Keep `ReadFrom.Configuration(...)`.

Before Serilog is built, resolve the configured File sink path through `ILogPathProvider.ApplicationLogDirectory`.

Preferred convention:

```json
{
  "Name": "File",
  "Args": {
    "path": "{ApplicationLogPath}"
  }
}
```

A small tested resolver replaces only that placeholder. Preserve all other File sink settings from appsettings.

If changing the config shape is undesirable, isolate existing sink-discovery logic in one tested component.

Do not use reflection into a constructed logger.

### Browser

Browser/WASM must not configure `Serilog.Sinks.File`. Use:
- structured in-memory sink;
- browser console sink if desired;
- browser export/storage only through platform abstraction.

## C. Runtime level control

Keep environment-specific defaults, but introduce a `LoggingLevelSwitch` exposed through:

```csharp
public interface IApplicationLogLevelController
{
    LogEventLevel MinimumLevel { get; }
    void SetMinimumLevel(LogEventLevel level);
}
```

The configured environment determines startup level. Viewer can temporarily change it without restart.

## D. High-frequency categories

Current Verbose output includes very high-rate entries:
- serial read sizes;
- every MAVLink decode;
- no-subscriber event publishes;
- full HUD state snapshots.

Do not remove diagnostics. Ensure high-volume areas have useful `SourceContext` categories and add sensible category overrides for normal operation. Development can still enable full Verbose.

Avoid a whole-application logging rewrite.

## E. Rolling and retention

Keep rolling interval, retained count, file-size policy, output template, etc. in appsettings. Active file must be readable by the historical viewer.

## Tests

Cover:
- bounded behavior;
- event ordering;
- exceptions and properties;
- subscriber isolation;
- path placeholder replacement;
- Browser has no File sink;
- runtime level change;
- existing rolling configuration preserved.

## Acceptance criteria

- Appsettings remains authoritative.
- Desktop File sink uses platform-resolved path.
- Browser has no desktop File sink dependency.
- Live structured log entries available through DI.
- Sink is bounded.
- Runtime level can change without restart.
- Development can still use Verbose.
