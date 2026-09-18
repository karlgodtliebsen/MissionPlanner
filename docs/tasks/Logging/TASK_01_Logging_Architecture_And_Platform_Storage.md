# TASK 01 — Logging Architecture and Platform Storage

## Goal

Introduce the common logging/storage architecture needed by telemetry logging and Serilog application logging, with desktop and Browser/WASM support.

## Existing context

MissionPlanner Next Gen already has telemetry functionality, `TelemetryLogsTabView`, `TelemetryLogsTabViewModel`, Serilog logging, desktop targets and Browser/WASM support.

Do not put filesystem logic into ViewModels.

## Required design

Create contracts equivalent in responsibility to:

```csharp
public interface ILogStorage
{
    Task<IReadOnlyList<LogStorageItem>> ListAsync(LogStorageArea area, CancellationToken cancellationToken = default);
    Task<Stream> CreateAsync(LogStorageArea area, string fileName, CancellationToken cancellationToken = default);
    Task<Stream> OpenReadAsync(LogStorageArea area, string id, CancellationToken cancellationToken = default);
    Task DeleteAsync(LogStorageArea area, string id, CancellationToken cancellationToken = default);
    Task<LogExportResult> ExportAsync(LogStorageArea area, string id, CancellationToken cancellationToken = default);
}

public enum LogStorageArea
{
    Telemetry,
    Application
}
```

Also add a desktop path service equivalent to:

```csharp
public interface ILogPathProvider
{
    string LogsRoot { get; }
    string TelemetryDirectory { get; }
    string ApplicationLogDirectory { get; }
}
```

Adapt names to existing solution conventions.

## Desktop policy

Primary root:

```text
<Environment.SpecialFolder.MyDocuments>/Mission Planner/logs
```

Telemetry files remain directly under `logs`. Application logs use `<root>/application`.

Fallback if MyDocuments is unavailable:
1. platform-appropriate local application data;
2. user-profile fallback;
3. clear diagnostic failure if no writable location exists.

Normalize separators and create directories lazily.

## Browser/WASM

Implement browser-safe `ILogStorage`.

Requirements:
- no arbitrary desktop path assumptions;
- no `File.*` / `Directory.*` from shared ViewModels;
- import/export through platform abstraction;
- live application logs always available from memory;
- use existing browser persistence if present; otherwise bounded session storage plus explicit export is acceptable initially.

No browser-specific types in Core/domain code.

## DI

Register platform implementation:
- Desktop -> `DesktopLogStorage` + `DesktopLogPathProvider`
- Browser -> `BrowserLogStorage`

## Models

Add immutable platform-neutral models for:
- stored log id;
- display name;
- logical area;
- size;
- created/modified timestamp;
- optional physical path on desktop.

## Tests

Cover:
- Windows-style path calculation;
- Linux-style path calculation;
- fallback when MyDocuments is unavailable;
- directory creation;
- path traversal prevention;
- list/create/open/delete;
- Browser implementation requiring no physical path.

Use temp directories/fakes.

## Acceptance criteria

- No logging ViewModel directly uses `File`/`Directory` or constructs Windows paths.
- Desktop resolves telemetry root to `Mission Planner/logs`.
- Application logs resolve to `Mission Planner/logs/application`.
- Browser builds without desktop filesystem assumptions.
- Services are registered through DI.
- Existing telemetry viewer remains.
