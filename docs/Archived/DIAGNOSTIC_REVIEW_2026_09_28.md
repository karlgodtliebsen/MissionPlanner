# Diagnostic follow-up review — 2026-09-28

Branch: `feature/improve-preflight-contradiction`.

The supplied task list substantially overlaps the existing implementation. This
follow-up preserves all Readiness, Messages and Inspector markup and the topbar
IconDropDownButton itself. No flight-controller commands, parameter writes, hardware
connections or recording operations were performed.

## Verification against the task list

| Task | Existing implementation and follow-up |
| --- | --- |
| 1 — Readable results | Typed ReadinessCheckItem templates already show statuses, units, explanations, evidence and navigation. Passing checks collapse and mixed results render at 360/700 pixels. Fixed retained links losing their connection-session context. |
| 2 — Battery assessment | Existing shared BatteryReadiness/PreflightAssessmentService already evaluates voltage independently of percentage, configured thresholds, freshness, disabled monitoring, instance-specific blockers and positive recovery. Existing decoder and lifecycle tests cover these rules. Extended reconnect isolation to connections where no preceding disconnect notification was observed. |
| 3 — Shared Inspector host | Existing shell host retains width for the application session and clamps it to available width. Views remain mounted across destination changes; inspection does not retarget operational commands. Existing pinning, freeze, filtering and export behavior retained. Fixed frozen samples crossing a reconnect. |
| 4 — Review | Offline production-markup screenshots captured; see below. No further layout migration in this change. |
| 5 — Readiness/Messages | Already present. Fixed Readiness count using the active vehicle while the entry point opened a pinned vehicle. Both menu counts now match the diagnostic target; the Flight Data summary remains active-vehicle scoped. Reading messages still does not resolve blocker evidence. |
| 6 — Flight Data/Logs | Dedicated panels, compact readiness summary and existing Logs route are already integrated. No additional tabs removed, no second log browser, and no logging behavior changed. |
| 7 — Validation/docs | Focused Core and UI tests exercised; documentation updated for session boundaries and count scope. Platform checks remain explicitly unverified. |

## Root causes and changed files

- `Views/Common/TopBarView.axaml`: three duplicated InformationOutline menu icons
  replaced by ClipboardCheckOutline (Readiness), MessageAlertOutline (Messages),
  and FileDocumentOutline (Logs). Inspector retains InformationOutline. The existing
  Ursa IconDropDownButton, BorderlessButton theme, Material icon library, 35×35 button,
  30×30 icons, spacing, commands and interaction styling are unchanged.
- `Diagnostics/VehicleDiagnosticModels.cs` and `VehicleLiveDiagnostics.cs` in Core:
  connection start was internal only. Expose it on the diagnostic snapshot and clear
  current state for every new connection, even when disconnect delivery was absent.
- `Views/Diagnostics/LiveTelemetryInspectorViewModel.Navigation.cs`: readiness row
  commands captured only vehicle/key. Capture connection start too, guard evidence
  and configuration navigation, recreate rows after reconnect, and separate inspected
  blocker counts from the active Flight Data summary.
- `Views/Diagnostics/LiveTelemetryInspectorViewModel.cs`: track displayed connection
  start and clear frozen presentation when it changes; invalidate count cache on
  selection changes.
- Tests: ReadinessNavigationTests, BatteryEvidenceLifecycleTests,
  DiagnosticPanelViewTests and DiagnosticMenuAndFirmwareLayoutTests cover these
  cases and generate the review images.
- `READINESS_AND_DIAGNOSTIC_NAVIGATION.md`: updated current dropdown and session semantics.

## Validation

Affected application and test projects build through `dotnet test --no-restore`.
Existing compiler warnings remain. Core diagnostic/battery filters pass **33 tests**;
UI readiness/navigation/menu/messages filters pass **18 tests**. No hardware was used.
Full logs are in `.tmp/diagnostics-review-core.log` and `.tmp/diagnostics-review-ui.log`.

The Core filter includes BatteryReadiness, BatteryWire, BatteryEvidenceLifecycle,
PreflightAssessment, VehicleLiveDiagnostics, VehicleDiagnosticPanels,
VehicleAdvancedDiagnostic and VehicleArmingDiagnostic.
The UI filter includes Readiness, DiagnosticPanelViewTests,
DiagnosticMenuAndFirmwareLayoutTests, LiveTelemetryInspectorTests, Messages and
DiagnosticConfigurationTests.

Set `MISSIONPLANNER_VISUAL_TEST_OUTPUT` to an output directory when running the UI
tests to reproduce the screenshots. These use synthetic offline data, not telemetry
from a connected controller:

- [Entire topbar, design preview](../.tmp/diagnostics-review/topbar-offline.png)
- [Desktop panel](../.tmp/diagnostics-review/panel-Desktop.png)
- [Narrow Inspector](../.tmp/diagnostics-review/panel-Inspector.png)
- [Expanded connection details](../.tmp/diagnostics-review/panel-Expanded-details.png)
- [Pinned vehicle different from active](../.tmp/diagnostics-review/panel-Pinned-other-vehicle.png)
- [Readiness, dark/narrow](../.tmp/diagnostics-review/readiness-Dark-360.png)
- [Readiness, light/desktop](../.tmp/diagnostics-review/readiness-Light-700.png)

## Review boundaries and remaining limitations

- The topbar image is a design preview with no connected-vehicle view model. It
  verifies the existing button layout, not runtime connection text or DPI behavior.
  The menu test separately verifies distinct icons and keyboard activation/dismissal.
- Light/dark readiness and headless layouts were exercised; OS scaling, all supported
  themes, browser behavior and native detached-window interactions were not tested.
- Expanded technical details can crowd the remaining Inspector content in the
  headless capture. Panel markup was intentionally left untouched for visual review.
- Explicit pinning is displayed in the panel. The compact menu labels themselves
  still do not name the pinned vehicle or show selected-panel indication. Changing
  those labels/states would require a separate topbar presentation change.
- Readiness opens as a list; direct scrolling/highlighting of an individual check
  is not implemented. Evidence navigation selects the relevant diagnostic section.
- Width persists during the application session, not across application restarts.
- An old-session link is rejected with feedback; it does not open a historical-session
  viewer. Historical journal/raw data remain available through existing diagnostics.
- No additional views were migrated. Existing detached-window support was preserved
  because the user's newer requests explicitly require it.

This is the requested review checkpoint, not a claim that every visual acceptance
criterion in the supplied task list has been completed.
