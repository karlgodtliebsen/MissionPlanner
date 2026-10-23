# Arming page implementation report

Date: 2026-09-25
Task package: `docs/tasks/arming-page` (Tasks 01–07).

## Delivered

- Semantic Arming configuration service over the existing parameter registry/metadata.
- Unknown/Disabled/All/Custom checks, metadata-backed custom bits, stick modes,
  location and vehicle-specific requirements, defaults and reboot evidence.
- Explicit current/pending review and verified Apply/Discard. All-check disabling
  includes an explicit warning in confirmation. Partial failures retain pending edits.
- RC assignment move/clear using Radio's shared conflict rules and function constant.
  Multiple assignments stay visible and block simple reassignment. Radio retains its
  existing controls and now uses the shared vehicle operation gate for assignment.
- Information/Configuration page with persistent diagnostic summary, status/advanced
  Markdown, semantic editors, contextual actions and Full Parameters navigation.
- Normal typed GCS Arm/Disarm, policy evaluation, explicit Arm confirmation,
  hazardous Disarm confirmation, replay prohibition and connection-scoped cancellation.
- Page activation/deactivation ownership, one-second cached semantic refresh, no
  independent parameter download, preserved unchanged documents, and invalidated
  pending state at vehicle/connection/identity boundaries.

## Files

Core:
- `src/Core/MissionPlanner.Core/Setup/Arming/ArmingSetupState.cs`
- `src/Core/MissionPlanner.Core/Setup/Arming/IArmingConfigurationService.cs`
- `src/Core/MissionPlanner.Core/Setup/Arming/ArmingConfigurationService.cs`
- `src/Core/MissionPlanner.Core/Setup/Arming/ArmingConfigurationService.Apply.cs`
- `src/Core/MissionPlanner.Core/Setup/MandatoryHardware/RadioArmingConfiguration.cs`
- `src/Core/MissionPlanner.Core/Configuration/DomainConfigurator.cs`

Application:
- `src/UI/MissionPlanner.App/Presentation/Documents/ArmingSetupDocumentFactory.cs`
- `src/UI/MissionPlanner.App/Views/InitSetup/Arming/ArmingPage.axaml`
- `src/UI/MissionPlanner.App/Views/InitSetup/Arming/ArmingPage.axaml.cs`
- `src/UI/MissionPlanner.App/Views/InitSetup/Arming/ArmingViewModel.cs`
- `src/UI/MissionPlanner.App/Views/InitSetup/Arming/ArmingViewModel.Operations.cs`
- `src/UI/MissionPlanner.App/Views/InitSetup/Arming/ArmingSettingViewModel.cs`
- `src/UI/MissionPlanner.App/Configuration/ApplicationConfigurator.cs`

Tests:
- `src/Tests/MissionPlanner.Core.Tests/ArmingConfigurationTests.cs`
- `src/Tests/MissionPlanner.AvaloniaUI.Tests/ArmingDocumentTests.cs`
- `src/Tests/MissionPlanner.AvaloniaUI.Tests/ArmingViewModelTests.cs`
- `src/Tests/MissionPlanner.AvaloniaUI.Tests/ArmingPageViewTests.cs`
- `src/Tests/MissionPlanner.AvaloniaUI.Tests/DocumentRenderingCollection.cs`
- `src/Tests/MissionPlanner.AvaloniaUI.Tests/CompassInformationDocumentTests.cs`

Documentation: `docs/ARMING.md`, `docs/SetupUxPattern.md`, this report.

## Architecture decisions

Core produces configuration evidence and verified write plans; the App document
factory formats it. The existing singleton live diagnostic owns readiness/request
history. The existing typed command service owns gating, transmission and ACK
correlation. No replacement diagnostic cache, protocol sender, arbitrary-parameter
form engine or force-arm/bypass UI was introduced.

Only completed parameter loads export assignments. Missing capabilities/defaults
remain unknown. Reconnect invalidates local pending values rather than silently
carrying a reviewed plan into a new session. The operator must stage/review again.
Multiple RC assignments require explicit cleanup in Full Parameters.

Shared Markdown rendering keeps native selection/copy; it does not restore the
buttons the user removed. The older Compass renderer test was corrected to assert
that behavior. Actual rendering tests are serialized because Avalonia's platform
registry is process-global; concurrent platform initialization exposed registry races.

## Automated validation

Executed from the repository with `dotnet` and absolute project paths:

- Core tests: `dotnet test src/Tests/MissionPlanner.Core.Tests/MissionPlanner.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~Arming|FullyQualifiedName~Compass|FullyQualifiedName~VehicleCommandPolicy"` — 82 passed.
- UI tests: `dotnet test src/Tests/MissionPlanner.AvaloniaUI.Tests/MissionPlanner.AvaloniaUI.Tests.csproj --no-restore --filter "FullyQualifiedName~Arming|FullyQualifiedName~Compass"` — 73 passed.
- Desktop: separate `OutputPath=artifacts/arming-validation/` to avoid modifying the running application's DLLs — passed, 0 errors; 22 existing unrelated warnings.
- Browser/WASM: `dotnet build src/Platforms/MissionPlanner.Browser/MissionPlanner.Browser.csproj --no-restore` — passed, 0 warnings/errors.
- `git diff --check` — passed. No new CS1591/CS1587 warnings in touched code.

Coverage includes special All semantics; metadata custom masks; stick modes;
missing metadata/parameters; Copter/Plane/Rover metadata projections; defaults/reboot;
RC move/clear/conflicts/multiple assignments/unsupported option; armed or stale Apply;
connection loss during writes; failed readback; reconnect; document escaping and
copyable assignments; historical/current separation; pending/default/Discard/Apply;
parameter completion; unchanged document identity; activation/deactivation; replay;
confirmation cancellation; accepted/denied/timeout command results; hazardous disarm;
a vehicle switch during confirmation; custom check selection; RC movement; observed
armed heartbeat transition; retained pending edits after failed Apply; and deactivation
during an unfinished initial load.

Real compiled-page tests exercise Information/Configuration at widths 700 and 1100,
light/dark themes and render scales 1 and 2. Shared renderer regression covers native
selection/copy, long documents, forbidden external content and theme changes.

## SITL result

Not run. No ArduCopter, Plane or Rover SITL session was operated during this work.
Cross-family automated metadata tests are not SITL verification.

## Physical hardware result

Not run. No physical controller was commanded, no motors were armed, and no hardware
parameter writes were performed. The props-removed acceptance procedure is in
`docs/ARMING.md` and the original Task 07 checklist.

## Limitations and remaining acceptance

- Execute the documented SITL and physical-bench checklists before treating hardware
  behavior as validated, including simultaneous cross-page readback observation.
- RC movement is sampled once per second; brief transitions can be missed. Hold each
  endpoint long enough for a refresh during bench testing.
- Multiple RC assignments deliberately require Full Parameters cleanup.
- No automatic navigation deep link selects a particular Radio/Safety/Compass tab;
  current blockers name their owning subsystem. Full Parameters navigation is wired.
- Automated headless layout/build checks do not establish browser clipboard behavior
  or physical high-DPI display quality; those remain manual acceptance items.
