# Test verification — 2026-09-18

Task 2: ensure the repository tests compile and run.

## Repairs

- Updated the parameter notification threading fixture to supply the clipboard service required by FullParametersListTabViewModel. This unblocked compilation.
- Configured the firmware UI fixture's Current snapshot from its mocked vehicle ID/state. The previously missing snapshot caused 46 activation failures; all 132 UI tests now pass.
- Fixed manifest MAV-type normalization: QUADROTOR and other recognized MAV types map to the existing family-based filters. Missing, null, or blank MAV types use the declared vehicle family; explicit unknown values remain Unknown. This also preserves legacy mirror deduplication.
- Added 17 parser regression cases covering supported aliases, whitespace, missing values, and explicit unknown types. The existing fresh-cache filtering and deduplication tests remain unchanged and pass.

## Commands and results

Run from the repository root with PowerShell 7:

~~~powershell
& ./src/Tests/Run-AllTests.ps1
dotnet build src/MissionPlanner.slnx --no-restore -p:UsedAvaloniaProducts= --verbosity quiet
~~~

The runner completed successfully, executing all six .NET test projects and the browser JavaScript tests sequentially.

| Suite | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Avalonia UI | 132 | 0 | 0 |
| Browser bridge | 1 | 0 | 0 |
| Core | 671 | 0 | 6 |
| Firmware | 309 | 0 | 1 |
| Simulator smoke | 8 | 0 | 22 |
| General tests | 54 | 0 | 1 |
| **.NET total** | **1,175** | **0** | **30** |
| Browser JavaScript | 7 | 0 | 0 |

The 30 existing skips require physical hardware, an external SITL/DroneBridge setup, or an explicitly local resource-renaming run. No tests were disabled or marked skipped by this work.

Final solution build: succeeded, 0 errors and 6 existing nullable warnings. No new public-API documentation warnings were reported. This was an incremental build; warning totals are not a clean-build baseline.

Local logs and TRX files:

- Test run: TestResults/all-tests/20260918-130103-809/
- Runner log: artifacts/task2-all-tests-final.log
- Solution build: artifacts/task2-solution-build.log

The [button icon audit](AXAML_BUTTON_ICON_AUDIT.md) documents Task 1. Its full-solution compilation blocker is resolved by the fixture repair above. Interactive visual inspection and physical hardware testing were not performed.
