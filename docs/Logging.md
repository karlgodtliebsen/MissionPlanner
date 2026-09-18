# Logging

Telemetry recordings and application diagnostics are separate logical storage areas.
Telemetry uses classic MAVLink tlog records; application diagnostics use Serilog.

## Storage

Desktop storage resolves lazily to `<Documents>/Mission Planner/logs`.
Telemetry files live directly in that directory; application files live in
`application`. If Documents is unavailable or unwritable, local application data
and then the user profile are tried. Failure to find a writable location produces
an explicit diagnostic. Paths use platform-native separators.

`ILogStorage` owns file creation, listing, read, deletion, and export streams.
Identifiers must be single file names. Existing files are never overwritten by
creation. Active writers prevent deletion. ViewModels use these streams and the
existing platform file picker/save services rather than filesystem calls.
`ILogPathProvider` is available only on desktop.

Browser storage is session-only, with a default limit of 32 MiB and 1,024 files.
Quota exhaustion fails the write; it never silently evicts a recording. Export
logs before closing or reloading the browser. Deleting a closed log reclaims its
quota. Browser metadata contains no physical path.

## Implementation sequence

1. Storage contracts and platform implementations: implemented.
2. Classic tlog recorder integration: pending.
3. Structured application logging and runtime level control: pending.
4. Logs root navigation: pending.
5. Telemetry viewer: pending.
6. Application viewer: pending.
7. Integration, health, retention, and final verification: pending.
