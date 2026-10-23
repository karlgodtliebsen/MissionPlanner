# Telemetry / arming execution results

Completed the five tasks in README order, retaining the previous uncommitted servo-output work.

1. **Radio switch configuration:** Radio Setup projects FLTMODE_CH, loaded RCx_OPTION
   assignments and ARMING_RUDDER. A selectable channel shows a bounded low/high
   movement trace. Explicit confirmation assigns only a free channel's option 153
   using the existing parameter metadata/edit/readback pipeline. Flight-mode, primary
   pilot and occupied channels, missing parameters, armed state and connection changes
   prevent assignment.
2. **Servo output propagation:** retained the prior typed zero-trimming fix and
   freshness/reset behavior; added real shared-catalog live/replay equality coverage.
3. **Common telemetry:** extended existing AHRS2, VFR_HUD, GPS_RAW_INT and POWER_STATUS
   decoders to reconstruct trimmed payloads, including partial GPS extension scalars.
   Primary attitude/global-position observations retain precedence for one second.
   Existing domain observations, immutable state slices and shared replay decoders remain
   authoritative; no parallel state or recorder format was added.
4. **Log metadata:** catalog/open/export rebuild timing, size, packet count and identity
   from the same indexed snapshot. Heartbeat supplies identity, AUTOPILOT_VERSION
   supplies version/build, and startup text supplies firmware fallback/recognized targets.
   AUTOPILOT_VERSION now also accepts trimmed base fields. Historical files are rebuilt
   on reopening rather than relying on a creation-time size or new persistent cache.
5. **Arming evidence:** added request/readiness stages, request source, ACK and historical
   PreArm context. Configured RC/stick gestures are explicitly inferred; heartbeat alone
   confirms armed. Fresh healthy checks plus live RC and no recent request produce
   configuration guidance instead of an invented rejection/blocker. Existing current
   reason expiry and recovery clearing remain; reconnect clears request context.

## Validation

- Core test project: **121 passed**, covering radio assignment, servo output propagation,
  shared decoder conformance, live/replay equality, firmware identity, recording/metadata,
  arming requests and diagnostic freshness. No failures or skipped tests in this selection.
- Avalonia UI tests: **8 passed** (full log clipboard export/cancellation and receiver binding).
  Export tests deliberately supply a stale zero-byte catalog row and compare Recording
  size/duration/count with the freshly generated Index.
- Shared Avalonia app compiled as part of UI tests.
- Browser/WASM build: **succeeded, zero warnings and zero errors**.
- No CS1591/CS1587 documentation warnings in the affected build/test logs.
- `git diff --check`: passed.

Commands used (from the repository root):

```powershell
dotnet test src/Tests/MissionPlanner.Core.Tests/MissionPlanner.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~RadioArmingConfigurationTests|FullyQualifiedName~ServoOutputPropagationTests|FullyQualifiedName~CommonTelemetryTrimTests|FullyQualifiedName~Telemetry|FullyQualifiedName~Arming|FullyQualifiedName~VehicleLiveDiagnostics|FullyQualifiedName~MavLinkConformanceFixtureTests|FullyQualifiedName~MavLinkDecoderCatalogTests|FullyQualifiedName~VehicleFirmwareIdentityTests|FullyQualifiedName~CoreVehicleStateTelemetryTests"
dotnet test src/Tests/MissionPlanner.AvaloniaUI.Tests/MissionPlanner.AvaloniaUI.Tests.csproj --no-restore --filter "FullyQualifiedName~TelemetryLogClipboardTests|FullyQualifiedName~ReceiverBindViewModelTests"
dotnet build src/Platforms/MissionPlanner.Browser/MissionPlanner.Browser.csproj --no-restore
```

## Limits and review notes

No physical controller/RC transmitter was operated and no parameter writes were sent
to hardware. RC source attribution remains an inference; raw PWM thresholds do not
confirm transmitter intent, stick hold duration or physical motor movement. A physical
bench check and interactive desktop/browser review remain appropriate. Log refresh now
scans indexed recordings to derive metadata, so very large libraries may refresh more
slowly. Unrelated existing CompassSetupView changes were preserved. Nothing was committed.
