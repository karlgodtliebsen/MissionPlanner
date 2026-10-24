# Running the tests

From the repository root, using PowerShell 7 on Windows with the .NET 10 SDK and Node.js installed:

```powershell
.\src\Tests\Run-AllTests.ps1
```

Use `-Configuration Release` for a Release run. The script restores and runs all seven .NET test projects sequentially, then the browser JavaScript tests. `MissionPlanner.Test.Support` is a helper library, not a test suite. Logs and TRX reports go into a timestamped folder under `TestResults/all-tests`. Any failed suite makes the script fail, while allowing the remaining suites to finish. Each suite has a ten-minute total execution timeout.

The root `global.json` selects Microsoft.Testing.Platform for `dotnet test` on .NET 10. The current xUnit packages use MTP v2, which no longer supports the legacy VSTest target with this SDK. This configuration does not pin or change the installed SDK version. See [Microsoft's migration guide](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-with-dotnet-test#migrate-to-mtp-mode-of-dotnet-test).

For individual suites and focused runs, use the MTP command-line options:

```powershell
dotnet test --project src/Tests/MissionPlanner.Analysis.Tests/MissionPlanner.Analysis.Tests.csproj --configuration Release
dotnet test --project src/Tests/MissionPlanner.Core.Tests/MissionPlanner.Core.Tests.csproj --filter-class '*BenchTelemetryReplayTests' '*SimulationPlaybackDiagnosticsTests'
dotnet test --project src/Tests/MissionPlanner.Core.Tests/MissionPlanner.Core.Tests.csproj --filter-trait 'TestTier=Unit' 'TestTier=FakeRuntime'
```

Use `--solution src/MissionPlanner.slnx` for a solution run. Old command examples in historical validation records use positional project paths and VSTest `--filter` expressions; translate them to `--project` and xUnit's `--filter-class`, `--filter-method` or `--filter-trait` options. The local runner uses xUnit's built-in `--report-xunit-trx` reporting; `--logger trx` and `--blame-hang-timeout` are VSTest options. See [xUnit's MTP options](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform).

The automated simulator tests create their own loopback UDP vehicle and connection session; no external simulator is needed. Local networking must be available. The UI suite targets Windows.

The following tests remain explicitly skipped because they need external equipment or perform manual maintenance:

- Five serial integration tests require a connected physical vehicle.
- Fourteen SITL tests (`SmokeTestsSitl`) require an external ArduPilot SITL that sends MAVLink to `udp:127.0.0.1:14550`. Set `MP_SITL=1` to run them; they are skipped otherwise.
- Eight DroneBridge tests require configured external devices.
- One firmware hardware theory requires a selected physical controller and operator procedure.
- One legacy resource-renaming test changes source files and is a manual maintenance tool.

No automated command test should be run against real flight hardware. Keep the external tests opt-in and review their setup before enabling them.

## Temporarily skipped UI regressions

On 2026-10-24, the following **17 failing cases across 14 test methods** were explicitly skipped at the user's request after migrating CI to Microsoft.Testing.Platform. The initial run exposed 14 failures; a follow-up run exposed three more cases with the same headless thread ownership error. They remain unresolved regressions; skipping them does not mean their assertions pass. Each `Skip` reason contains the tracking ID below. Test bodies and assertions are retained, and unrelated cases in the same theories remain enabled.

| ID | Observed failure | Required before removing the skip |
|---|---|---|
| UI-SKIP-01 | Mandatory Hardware tab header `Frame Type` differs from help article title `Frame`. | Align the help catalog and intended navigation labels, then pass the complete section-help theory. |
| UI-SKIP-02 | The offline topbar fixture reaches `NotificationHelper.SetupManagers` without a view model. | Correct the fixture or the design-preview lifecycle and verify the topbar layout assertions. |
| UI-SKIP-03 | Headless rendering accesses Avalonia objects owned by a different thread. | Resolve dispatcher/session ownership and isolation, then verify these cases both individually and in the full UI suite. |
| UI-SKIP-04 | `HeadlessPlatformTypeface` is cast to `SkiaTypeface` during rendering. | Make the test rendering/font backends consistent and isolated, then verify these cases both individually and in the full UI suite. |

| Test method | Skipped cases | ID |
|---|---|---|
| [SectionHelpTests.EveryVisibleSectionHasOneCompleteHelpArticle](MissionPlanner.AvaloniaUI.Tests/SectionHelpTests.cs) | Mandatory Hardware only (1); Optional Hardware and Configuration remain enabled. | UI-SKIP-01 |
| [DiagnosticMenuAndFirmwareLayoutTests.TopbarRetainsLayoutWithDistinctMenuIcons](MissionPlanner.AvaloniaUI.Tests/DiagnosticMenuAndFirmwareLayoutTests.cs) | 1 | UI-SKIP-02 |
| [DiagnosticMenuAndFirmwareLayoutTests.FirmwareConfigurationScrollsInShortWindow](MissionPlanner.AvaloniaUI.Tests/DiagnosticMenuAndFirmwareLayoutTests.cs) | 1 | UI-SKIP-03 |
| [DiagnosticPanelViewTests.DestinationsMountAndRetainSelectionAtNarrowWidth](MissionPlanner.AvaloniaUI.Tests/DiagnosticPanelViewTests.cs) | 1 | UI-SKIP-03 |
| [ParameterProfilesViewModelTests.ProfileDialogRenders](MissionPlanner.AvaloniaUI.Tests/ParameterProfilesViewModelTests.cs) | 1 | UI-SKIP-03 |
| [PaletteThemeRenderingTests.PalettesResolveAndRenderIndependentlyAcrossWindows](MissionPlanner.AvaloniaUI.Tests/PaletteThemeRenderingTests.cs) | 1 | UI-SKIP-03 |
| [ParameterEditorToolbarTests.EditorWidthTogglePreservesViewAndModel](MissionPlanner.AvaloniaUI.Tests/ParameterEditorToolbarTests.cs) | 1 | UI-SKIP-03 |
| [ParameterEditorToolbarTests.SelectedTabControlsVisibleActionsAndQuickEditorCommands](MissionPlanner.AvaloniaUI.Tests/ParameterEditorToolbarTests.cs) | 1 | UI-SKIP-03 |
| [QuickEditorLayoutTests.EditorSectionsDoNotOverlapAtDifferentWindowSizes](MissionPlanner.AvaloniaUI.Tests/QuickEditorLayoutTests.cs) | 800x550, 1200x850, 1920x1030 (3) | UI-SKIP-03 |
| [MapOverlayThemeTests.ProductionMapIconsStayBlack](MissionPlanner.AvaloniaUI.Tests/MapOverlayThemeTests.cs) | FlightData and FlightPlanner (2); GeoFence remains enabled. | UI-SKIP-03 |
| [FftAnalysisViewTests.CompiledViewRendersSourceControlsAndBothPlotModes](MissionPlanner.AvaloniaUI.Tests/FftAnalysisViewTests.cs) | 1 | UI-SKIP-03 |
| [FirmwareDocumentTests.FormattedNumericAndLiveBindingsReachTheDocument](MissionPlanner.AvaloniaUI.Tests/FirmwareDocumentTests.cs) | 1 | UI-SKIP-04 |
| [CompassInformationDocumentTests.RenderSelectCopyAndTheme](MissionPlanner.AvaloniaUI.Tests/CompassInformationDocumentTests.cs) | 1 | UI-SKIP-04 |
| [ServoOutputViewTests.ConfigurationRendersLoadedServoRows](MissionPlanner.AvaloniaUI.Tests/ServoOutputViewTests.cs) | 1 | UI-SKIP-04 |

Verification on Windows with the Release configuration after applying these skips: **525 passed, 17 skipped, 0 failed** (542 cases total).

To restore coverage, remove the relevant `Skip` attributes after repairing the cause, run the full UI suite below, and update this inventory. Keep the workflow's failure handling enabled so any other failing tests still fail CI.

```powershell
dotnet test --project src/Tests/MissionPlanner.AvaloniaUI.Tests/MissionPlanner.AvaloniaUI.Tests.csproj --configuration Release
```
