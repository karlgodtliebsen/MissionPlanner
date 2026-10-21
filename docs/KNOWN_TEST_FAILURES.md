# Known test failures

Last verified: 2026-10-22 (Debug, Windows, .NET 10.0.402).

A failure is considered known only when both the test name and the failure signature match an
entry below. New failures, or the same test failing for a different reason, must be investigated.

## Resolved since the 2026-07-22 list

The twelve `MissionPlanner.Core.Tests` failures recorded on 2026-07-22 (serial-hardware tests,
fixed UDP port contention, missing `IHeartbeatVehicleHandler` registration, and the exact
floating-point HUD assertion) no longer fail. `MissionPlanner.Core.Tests` currently reports
no failures; its skipped tests are the opt-in external-equipment tests listed in
[../src/Tests/README.md](../src/Tests/README.md).

Seventeen `AvaloniaUI.Tests` failures caused by stale file paths, the renamed firmware tabs, the
unregistered `OpenParameterEditorsViewModel` in test hosts, and two rendering classes missing from
the `Document rendering` collection were fixed on 2026-10-22.

## Headless UI tests that pass alone but fail in a full run

Two tests pass when run on their own and fail when the whole `MissionPlanner.AvaloniaUI.Tests`
project runs. Avalonia's platform registry is process-global, and test classes configure it
differently (`UseSkia()` with real drawing versus headless drawing), so the first class to start
decides what the others get. Run them with `--filter` to verify a change that touches them.

| Test | Signature |
|---|---|
| `PaletteThemeRenderingTests.PalettesResolveAndRenderIndependentlyAcrossWindows` | `InvalidCastException: Unable to cast object of type 'HeadlessGlyphRunStub' to type 'Avalonia.Skia.GlyphRunImpl'` |
| `FftAnalysisViewTests.CompiledViewRendersSourceControlsAndBothPlotModes` | `The calling thread cannot access this object because a different thread owns it` |

The durable correction is to build one shared `AppBuilder` for every rendering test class (all of
them are already in the `Document rendering` collection), or to move the rendering tests that need
Skia into their own test project.

## Hardware and external-tool tests

Tests that need a physical vehicle, an external SITL, or DroneBridge devices are skipped with an
explicit reason rather than failing. See [../src/Tests/README.md](../src/Tests/README.md).
