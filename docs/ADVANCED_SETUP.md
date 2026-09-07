# Advanced Setup

Implementation follows `tasks/Setup-Advanced/MissionPlanner-Setup-Advanced-Codex-Tasks.md`
in order. Baseline: `04135bc5b` on `feature/setup-advanced`, also the local `main` head.

## Foundation (ADV-00)

The Core catalogue contains thirteen stable feature IDs, descriptions, risk/help text,
prerequisites, and routes. `AdvancedAvailabilityService` evaluates immutable platform and
session evidence without Avalonia, native APIs, permission prompts, or transport creation.
Platform prerequisites are checked before connection requirements so a disconnected browser
still explains its missing output bridge.

Windows capability reporting is in `MissionPlanner.Library.Windows`; browser reporting is
in `MissionPlanner.Library.Browser`. Browser vehicle connectivity does not imply an audited
output bridge. Browser keys are explicitly session-only. Location remains permission-required
until an adapter supplies permission evidence. No permission is requested merely by opening
the hub.

The hub uses the Install Firmware `SectionCard`, heading, warning, and Ursa ContentPage
styles. Its scroll viewport receives finite space from a star-sized Grid row. Text wraps,
reasons are selectable, and launch buttons have feature-specific accessibility names.
Each card owns a small ViewModel; the parent subscribes to its `Action<AdvancedFeatureId>`
event only while active. Connection, active-vehicle, parameter, and platform changes refresh
availability. Generation checks reject queued updates from an old activation.

`AdvancedToolRegistry` validates unique IDs and non-null factories. The existing application
page factory handles its stable routes. Subsequent tasks register concrete pages; until then
all inventory entries remain visible and unimplemented destinations are explicitly disabled.
This is the foundation stage, not completion of the thirteen tools.

Tool implementations must use the existing view lifecycle and create an
`AdvancedToolLifetime` per activation. Register owned subscriptions, sinks, pending tasks,
and secret-clearing callbacks there. Closing cancels first, runs cleanup in reverse order,
and attempts every cleanup even after a failure. Never register shared connection services
for disposal. Tests cover ten complete lifetime cycles and cleanup failure.

## Verification record

### Warning Manager (ADV-01)

The Warning Manager route now opens a working page with singleton list, editor and live
status child ViewModels. Parent `ActivateAsync`/`DeactivateAsync` attach and detach their
`Action<T>` events. Saves validate the entire next rule set and only update the saved list
after persistence succeeds. Preview does not modify rules or evaluation state. Editing,
duplication, deletion, enable/disable and acknowledgement are supported.

Rules use numeric fields from the existing `ITelemetryFieldCatalog`, with explicit native
units and a three-second freshness limit. Text/Boolean/enum fields are not numeric sources.
Ranges include both endpoints. Hysteresis expands the hold region; outside-range hysteresis
must be less than half the range width. Missing, non-finite, future-dated, stale, disconnected,
and disabled samples have defined states. Acknowledgement suppresses repeats for one rule
until it clears. Switching vehicles resets evaluation and acknowledgement state.

Visual evaluation runs at four updates per second while the page is open. Closing cancels
the timer and pending persistence and detaches all child events. Speech/system notifications
are deliberately not enabled and the page states this explicitly. Rules never issue vehicle
commands. The live panel retains cleared/unavailable states instead of inventing values.

Windows stores `advanced-warnings.json` under the existing per-user `MissionPlanner Next Gen`
application-data directory using temporary-file replacement. Browser uses the independent
origin-local `MissionPlanner.Advanced.WarningRules` local-storage key. Both preserve one
recovery copy of malformed documents. The shared versioned repository skips individually
invalid/duplicate records while retaining valid rules, limits documents to 256 Ki characters
and 256 rules, and reports storage failures. Import/export reuse `IFileOpenService` and
`IFileSaveService`; imports validate before merging by stable rule ID and reject invalid files
without replacing saved rules. `BrowserPlanningFileService` supplies those existing interfaces
through the browser's single-view storage provider, without requiring a desktop Window.

ADV-01 tests cover all operators, timing, hysteresis, acknowledgement isolation, missing/stale
inputs, invalid rules, serialization/corrupt recovery, preview isolation, ten navigation cycles,
and cancellation during a pending load. Browser JavaScript tests verify separate storage keys,
limits, recovery, and quota failure propagation.

Commands run from repository root, with `-p:UsedAvaloniaProducts=` to avoid optional product
license checks during automated builds:

- `dotnet build src/Platforms/MissionPlanner.Desktop/MissionPlanner.Desktop.csproj --no-restore -p:UsedAvaloniaProducts= -v quiet`: passed.
- `dotnet build src/Platforms/MissionPlanner.Browser/MissionPlanner.Browser.csproj --no-restore -p:UsedAvaloniaProducts= -v quiet`: passed.
- `dotnet build src/MissionPlanner.slnx --no-restore -p:UsedAvaloniaProducts= -v quiet`: passed.
- `src/Tests/Run-AllTests.ps1`: passed, 846 .NET tests and 6 JavaScript tests; 29 existing skips unchanged. Results: `TestResults/all-tests/20260907-023009-517`. The initial sandbox attempt could not read user NuGet settings; rerunning with that access passed.

The Firmware panel test fixture needed its newly introduced `IDomainFactory` dependency
registered; no production firmware behavior was changed. No hardware commands were sent.
Interactive screen-reader, theme, and resize verification remains for the final ADV-14 audit.


ADV-01 verification: Desktop, Browser, and `src/MissionPlanner.slnx` builds passed
with `--no-restore -p:UsedAvaloniaProducts= -v quiet`. The full `src/Tests/Run-AllTests.ps1`
run passed 870 .NET tests and 7 JavaScript tests, with the same 29 skips.
Results: `TestResults/all-tests/20260907-024555-616`. Existing unrelated compiler warnings
remain; no new CS1591/CS1587 warnings were reported. Hardware and interactive UI testing
were not performed.

The import/export follow-up passed all 45 UI tests (one additional file-service integration
test), Browser build and full solution build. These checks follow the 870-test full run above.
