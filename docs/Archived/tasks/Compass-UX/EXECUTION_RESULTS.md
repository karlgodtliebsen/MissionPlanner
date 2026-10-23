# Compass UX execution results

## Status

Tasks 01–06 are implemented. Task 07 is **gated on real-hardware validation**,
which has not been performed in this session. Its instruction is:

> After Compass is complete and validated on real hardware, extract only the proven reusable pieces needed by future Setup pages.

The reusable pattern and Arming migration guidance are documented in
[SetupUxPattern.md](../../SetupUxPattern.md). No speculative generic Setup framework
or full Arming page was introduced.

## Implemented behavior

1. Extended the existing `ICompassConfigurationService` with semantic read,
   evaluate and apply contracts. Nullable configuration preserves missing values;
   capability/default/source details are separate from ordinary setting names.
2. Reused the parameter registry, metadata parser, context-created parameter edit
   sessions, readback confirmation and vehicle operation gate. Disabling Compass
   explicitly reviews navigation-use and first-yaw-source dependencies. Unknown or
   compass-dependent alternate yaw sets block disabling until explicitly reviewed.
3. Rebuilt the page as Status, Configuration, Calibration / Actions, and collapsed
   Advanced diagnostics. Friendly controls retain secondary raw names/values and
   existing calibration start/progress/accept/cancel/reset behavior.
4. Added per-setting changed markers, a pending footer, metadata-only default resets,
   Discard, confirmed Apply, partial-failure reporting and explicit safe reboot.
   Current-value/connection/firmware conflicts prevent stale reviews from applying.
5. Separated configuration validity, device detection, fresh magnetometer health
   and current arming evidence. Historical compass PreArm messages do not become
   current blockers. IDs use the existing bus/address decode without guessed models.
6. Added regression coverage and documented the pattern. Forced-external sensor mode
   remains an enum value and is preserved across unrelated edits. Unsupported enum
   capabilities/defaults remain unavailable; no guessed defaults are written.

## Verification

- Core selection: **29 passed**, zero failures/skips. Includes semantic models,
  missing/unsupported capability, dependency order, actual write/readback integration,
  enable + orientation, partial failure, readback mismatch, reboot aggregation,
  connection/armed guards, historical/current arming evidence, forced-external mode,
  metadata defaults, existing Compass diagnostics and metadata parser regression tests.
- Avalonia selection: **21 passed**, zero failures/skips. Includes Compass local edits,
  defaults/discard, success/failure, reconnect conflicts, loading/unsupported state,
  calibration command availability, and existing Frame/parameter progress/threading tests.
- Desktop and Browser/WASM builds: succeeded with zero warnings/errors in final platform
  build logs. Existing unrelated warnings remain when recompiling some dependency/test
  projects. No new Compass warnings or CS1591/CS1587 documentation warnings remain.
- `git diff --check`: passed.

Commands:

```powershell
dotnet test src/Tests/MissionPlanner.Core.Tests/MissionPlanner.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~Compass|FullyQualifiedName~ParameterMetadataXmlParserTests"
dotnet test src/Tests/MissionPlanner.AvaloniaUI.Tests/MissionPlanner.AvaloniaUI.Tests.csproj --no-restore --filter "FullyQualifiedName~CompassSetupViewModelTests|FullyQualifiedName~FrameSetupViewModelTests|FullyQualifiedName~ParameterProgressDialogTests|FullyQualifiedName~ParameterNotificationThreadingTests"
dotnet build src/Platforms/MissionPlanner.Desktop/MissionPlanner.Desktop.csproj --no-restore
dotnet build src/Platforms/MissionPlanner.Browser/MissionPlanner.Browser.csproj --no-restore
```

## Remaining validation

No hardware parameters were written, calibration was not run on a controller, and no
physical vehicle reboot was requested. Interactive visual review and a hardware bench
check remain unverified. Before Task 07 extraction, verify enabled/disabled configurations,
actual compass inventory/rotation choices, calibration acceptance/cancellation, dependent
yaw changes, readback and the explicit reboot/reconnect flow on supported firmware.

Changes are uncommitted. Existing unrelated work was not reverted.
