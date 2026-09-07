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

Commands run from repository root, with `-p:UsedAvaloniaProducts=` to avoid optional product
license checks during automated builds:

- `dotnet build src/Platforms/MissionPlanner.Desktop/MissionPlanner.Desktop.csproj --no-restore -p:UsedAvaloniaProducts= -v quiet`: passed.
- `dotnet build src/Platforms/MissionPlanner.Browser/MissionPlanner.Browser.csproj --no-restore -p:UsedAvaloniaProducts= -v quiet`: passed.
- `dotnet build src/MissionPlanner.slnx --no-restore -p:UsedAvaloniaProducts= -v quiet`: passed.
- `src/Tests/Run-AllTests.ps1`: passed, 846 .NET tests and 6 JavaScript tests; 29 existing skips unchanged. Results: `TestResults/all-tests/20260907-023009-517`. The initial sandbox attempt could not read user NuGet settings; rerunning with that access passed.

The Firmware panel test fixture needed its newly introduced `IDomainFactory` dependency
registered; no production firmware behavior was changed. No hardware commands were sent.
Interactive screen-reader, theme, and resize verification remains for the final ADV-14 audit.

