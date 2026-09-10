# Runtime identification corrections — 2026-09-11

The existing implementation covered much of tasks 01–05, but discovery ran MSP before MAVLink, one snapshot-wide deadline could starve later endpoints, and the workflow disabled Install until manual bootloader entry. These were functional gaps even though the earlier regression tests passed.

## Task 01 — identity model

Changed `FirmwareRuntimeProbeResult.cs` and `FirmwareWorkflowContext.cs` to carry typed probe outcomes, application-mode presentation, and runtime verification independently of board confidence. USB names remain hints. Runtime evidence never populates bootloader board identity.

Tests: runtime identity invariants and installation-plan verification checks. No new protocol or transport stack was introduced.

Icon-button definitions/resources/styles were not modified.

## Task 02 — safe runtime probing

Changed `IArduPilotRuntimeVerifier.cs`, `TemporaryMavLinkBootloaderGateway.cs`, `FirmwareDeviceIdentityService.cs`, and `BetaflightOptions.cs`.

Discovery now uses existing-session evidence first, passive MAVLink second, and sequential MSP fallback third. Busy ports are not taken over. Results distinguish non-ArduPilot MAVLink, timeout, busy, and transport failures. Each device receives a bounded 12-second default budget, including fallback; a timeout on one endpoint does not prevent the next from being identified. Presence-bound caching and explicit invalidation remain supported.

Tests cover ordering, successful ArduPilot without MSP, Betaflight fallback, earlier-port timeout, late invalidated results, busy/open errors, cancellation, and disposal of an outstanding native read.

Icon-button definitions/resources/styles were not modified.

## Task 03 — Install Firmware integration

Changed `InstallFirmwareViewModel.Workflow.cs` to pass runtime verification into the plan and show operating mode, runtime evidence, verification, and diagnostic code through the existing summary binding. Existing discovery/selection lifecycle and board compatibility remain in their owning services.

Tests cover verified ArduPilot with unresolved board, application-mode text, USB-only hints, and existing panel/selection regressions.

Icon-button definitions/resources/styles were not modified.

## Task 04 — automatic bootloader handoff

Changed `FirmwareWorkflowResolver.cs`. A validated APJ and verified ArduPilot application can start Install with board compatibility pending. This enables the already-implemented installation service to enter/discover the bootloader, read its board identity, check strict compatibility, request installation confirmation, and only then erase/write. Runtime alone does not mark the artifact compatible. Unverified runtime, armed state, same-port telemetry ownership, invalid packages, and wrong formats remain blocked.

Existing bootloader-entry, board mismatch, missing identity, discovery, cancellation, DFU, and programming-order tests remain authoritative. No physical reboot or firmware installation was performed during this correction.

Icon-button definitions/resources/styles were not modified.

## Task 05 — verification and hardware evidence

New tests: `FirmwareRuntimeDiscoveryTests.cs`, `FirmwareRuntimeHardwareTests.cs`.

Updated tests: `FirmwareProbeSerializationTests.cs`, `BetaflightDiscoveryTests.cs`, `FirmwareRuntimeIdentityTests.cs`, `FirmwareInstallationPlanTests.cs`, `TemporaryMavLinkBootloaderGatewayTests.cs`, and `FirmwarePlanViewModelTests.cs`.

The earlier ordering test incorrectly required MSP first. It now requires MAVLink first. The snapshot-timeout test now verifies that discovery continues after one endpoint times out. Compatibility assertions were retained.

Commands run from the repository (absolute project paths were used by the tools):

```powershell
dotnet test src/Tests/MissionPlanner.Firmware.Tests/MissionPlanner.Firmware.Tests.csproj --no-restore -v minimal
dotnet test src/Tests/MissionPlanner.Core.Tests/MissionPlanner.Core.Tests.csproj --no-restore --filter "FullyQualifiedName~TemporaryMavLinkBootloaderGatewayTests|FullyQualifiedName~FirmwareRuntimeHardwareTests" --environment MP_FIRMWARE_RUNTIME_PORT=COM10 -v minimal
dotnet test src/Tests/MissionPlanner.AvaloniaUI.Tests/MissionPlanner.AvaloniaUI.Tests.csproj --no-restore -v minimal
dotnet build src/MissionPlanner.slnx --no-restore -v minimal
dotnet test src/Tests/MissionPlanner.AvaloniaUI.Tests/MissionPlanner.AvaloniaUI.Tests.csproj --no-build --filter FullyQualifiedName~FirmwarePlanViewModelTests -v minimal
```

Results: firmware 292 passed, 1 manual flash test skipped; gateway plus opt-in COM10 hardware 17 passed; Avalonia UI 104 passed. Builds reported existing unrelated nullable/member warnings, with no CS1591/CS1587 warnings in affected files.

Final solution build succeeded (0 errors, 8 existing test-project warnings). After adding the final automatic-Install UI assertions, all 16 firmware-plan UI tests passed. `git diff --check` passed. The entire solution test suite was not run; verification covered the affected firmware suite, gateway tests, hardware probe, and Avalonia UI suite.

COM10 initially opened but transmitted no bytes. The operator confirmed normal MissionPlanner connection also failed and unplugged/reconnected the controller. Thereafter two consecutive production passive probes and the identity-discovery integration passed: ArduPilot, verified by MAVLink, application mode, exact board unresolved. The serial handle was released between attempts. No MAVLink commands, MSP requests, reboot, erase, or programming were needed by the successful hardware test. Experimental serial-readiness and read-only request changes were removed.

The hardware test is opt-in via `MP_FIRMWARE_RUNTIME_PORT`; ordinary test runs skip it. Physical flashing, bootloader handoff, and real Betaflight/DFU hardware remain unverified in this session; automated regressions cover those paths.

The ongoing external edit to `InstallFirmwarePage.axaml` was observed and left untouched. This correction changed no view markup.

Icon-button definitions/resources/styles were not modified.
