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

### MAVLink Inspector (ADV-02)

The inspector acquires a bounded observer lease from the existing connection's pipeline.
Receive decoding remains single-pass; the tap retains the same decoded message and original
frame bytes. Outbound observations are recorded after successful transport submission and
read framing metadata only. Unknown-dialect candidates are exposed by the existing parser
as CRC-unverified diagnostics and remain rejected from normal processing. Signature bytes
are retained; the inspector explicitly does not claim authentication verification.

Each of at most eight observers owns a bounded queue (default 512 observations). Full queues
drop the newest observation and increment a visible counter; they never block telemetry.
The aggregator retains the first 512 distinct direction/system/component/message keys until
Clear and counts omitted new keys. Each key keeps only its latest frame and five one-second
rate buckets. Rates divide the last five buckets by five seconds, including the current
partial second. Decoded details are limited to 128 fields and 8192 characters per message.

The page refreshes four times per second and uses a virtualized table, a separate detail
ViewModel, text/numeric/direction filters, message-ID or descending-count sort, clear,
clipboard copy and bounded JSON export through the existing file service. Freeze captures
an immutable display snapshot while collection continues; exports capture current collection
state. Closing releases the observer immediately and clears retained raw data. Reopening
starts new statistics. Connection termination requires reopening after reconnect.

Tests exercise direction/system/component/message separation, rate expiry, filters, unknown
and signed byte preservation, a 100,000-frame stream, overflow and independent queues,
normal decoder/event delivery alongside inspection, freeze/copy/clear, and ten page cycles.

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

ADV-02 verification: Desktop, Browser, and full solution builds passed with
`--no-restore -p:UsedAvaloniaProducts= -v quiet`. `src/Tests/Run-AllTests.ps1` passed
877 .NET tests and 7 JavaScript tests, with 29 existing skips.
Results: `TestResults/all-tests/20260907-030648-480`. No hardware was used.

## ADV-03 Proximity Viewer

Core normalizes existing decoded DISTANCE_SENSOR and OBSTACLE_DISTANCE observations to meters
and clockwise degrees from vehicle forward. Horizontal ArduPilot rotations use orientation × 45°;
global obstacle arrays require a fresh vehicle heading. Vertical and unsupported frames remain in
diagnostics without being projected onto the radar. See the [MAVLink field definitions](https://mavlink.io/en/messages/common.html)
and [ArduPilot proximity orientation handling](https://github.com/ArduPilot/ardupilot/blob/master/libraries/AP_Proximity/AP_Proximity_MAV.cpp).

Unknown, out-of-range, too-close and stale measurements remain distinct. OBSTACLE_DISTANCE zero
is a usable reading only when its minimum allows zero; DISTANCE_SENSOR zero is treated as unknown.
The nearest valid horizontal reading excludes stale/unsupported samples. The default freshness is
two seconds, configurable through ProximityOptions. Arrays have no protocol sensor instance ID;
their identity is system/component/sensor-type, while distance sensors also retain their sensor ID.

The page owns a 256-observation queue on the existing connection tap and retains at most 64 sources
with 72 points each. It neither re-decodes messages nor changes telemetry rates. UI snapshots update
at four Hz; a single responsive drawing control displays distance rings and points, with a separate
virtualized diagnostics view. Radar, diagnostics and parent coordinator have dedicated viewmodels.
Closing the page cancels its reader/timer and clears data without disposing the shared connection.

ADV-03 verification: Desktop, Browser and full solution builds passed using the same commands above.
Core tests passed 565 tests and UI tests passed 47, including angle/units/sentinel fixtures, source
limits, fake-clock staleness, a 1,000-sample coalescing test and ten navigation cycles. Full-suite
results are recorded below. No hardware or interactive rendering checks were performed.

Full ADV-03 suite: 886 .NET tests and 7 JavaScript tests passed; 29 existing skips unchanged.
Results: `TestResults/all-tests/20260907-032437-861`.
