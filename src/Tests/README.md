# Running the tests

From the repository root, using PowerShell 7 on Windows with the .NET 10 SDK and Node.js installed:

```powershell
.\src\Tests\Run-AllTests.ps1
```

Use `-Configuration Release` for a Release run. The script restores and runs all six .NET test projects sequentially, then the browser JavaScript tests. `MissionPlanner.Test.Support` is a helper library, not a test suite. Logs and TRX reports go into a timestamped folder under `TestResults/all-tests`. Any failed suite makes the script fail, while allowing the remaining suites to finish.

The automated simulator tests create their own loopback UDP vehicle and connection session; no external simulator is needed. Local networking must be available. The UI suite targets Windows.

The following tests remain explicitly skipped because they need external equipment or perform manual maintenance:

- Five serial integration tests require a connected physical vehicle.
- Fourteen SITL tests (`SmokeTestsSitl`) require an external ArduPilot SITL that sends MAVLink to `udp:127.0.0.1:14550`. Set `MP_SITL=1` to run them; they are skipped otherwise.
- Eight DroneBridge tests require configured external devices.
- One firmware hardware theory requires a selected physical controller and operator procedure.
- One legacy resource-renaming test changes source files and is a manual maintenance tool.

No automated command test should be run against real flight hardware. Keep the external tests opt-in and review their setup before enabling them.
