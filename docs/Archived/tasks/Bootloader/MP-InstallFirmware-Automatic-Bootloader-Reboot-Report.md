# Automatic ArduPilot bootloader entry — implementation report

Verified on 2026-09-06. Changes retain `feature/install-firmware-work-through`, which contains the current remote `main` commit (`ae2d5a9cc`) plus the two existing firmware-work commits. The remote main hash was verified without changing the checkout.

## Revised flow

1. Select a serial device and load/validate the APJ package.
2. Check the selected device for an existing ArduPilot bootloader (`CheckingForBootloader`). The initial probe has a configurable two-second deadline (`Firmware:BootloaderInitialProbeTimeout`).
3. If not identified, obtain temporary serial access and request MAVLink reboot (`RequestingBootloaderReboot`). An ordinary vehicle connection is not required. Existing connection-conflict checks and exclusive OS serial ownership remain in force.
4. Release temporary access and watch for the matching bootloader (`WaitingForBootloader`). Missing ACK, USB disappearance, or a recoverable write failure does not decide the outcome: successful bootloader protocol identification does.
5. If automatic detection expires, present `ManualBootloaderReconnect` (`ManualBootloaderReconnectRequired`), then run bounded discovery again. Rejecting the prompt or cancelling stops the operation.
6. Identify the bootloader, validate APJ compatibility, close the installation confirmation, then erase/program/verify/reboot. Existing mismatch handling and destructive-stage cancellation safeguards remain unchanged.

STM32 DFU is unchanged and remains a separate workflow. Browser direct installation remains disabled by the existing page capability evaluation; no desktop serial API was added to a shared ViewModel.

## Temporary MAVLink access and device identity

The existing `TemporaryMavLinkBootloaderGateway` uses `IFirmwareSerialPortFactory`, the existing MAVLink frame parser/decoder, and `IMavLinkCommandEncoder`. It identifies an ArduPilot autopilot heartbeat and sends `MAV_CMD_PREFLIGHT_REBOOT_SHUTDOWN` with parameter 1 set to 3, then disposes its port without waiting for an ACK. This matches [ArduPilot's reboot-to-bootloader semantics](https://www.ardupilot.ardupilot.org/sub/docs/common-install-sdcard.html). No vehicle registration, global session, or second MAVLink implementation is created.

Discovery now filters candidates for a selected device instead of only ranking it ahead of unrelated devices. Matching prioritizes USB serial number across changed PID, OS instance, and COM port; otherwise it uses OS identity, with same-port matching when stable evidence is unavailable. Conflicting known serial identities are rejected even on the previously selected COM port. Arrival notifications and the existing bounded polling policy are both retained.

The initial probe previously bypassed devices labeled as application devices. Its cancellation path could skip disposal during `IdentifyAsync`. Discovery now releases its client/port in `finally` unless ownership is transferred to a successfully discovered bootloader, allowing the initial probe to run before temporary MAVLink access.

## Files changed

Paths below are relative to the repository root.

| Files | Purpose |
| --- | --- |
| `src/Core/MissionPlanner.Core/Firmware/TemporaryMavLinkBootloaderGateway.cs` | Bounded reboot write, ArduPilot heartbeat selection, structured diagnostics. |
| `src/Core/MissionPlanner.Firmware/Configuration/FirmwareOptions.cs`, `FirmwareConfigurator.cs` | Configurable, validated initial probe deadline. |
| `src/Core/MissionPlanner.Firmware/Devices/SystemFirmwareSerialPortFactory.cs` | Release a port if cancellation arrives during native open. |
| `src/Core/MissionPlanner.Firmware/Discovery/BootloaderDiscoveryService.cs` | Cancellation-safe disposal, selected physical-device filtering, discovery diagnostics. |
| `src/Core/MissionPlanner.Firmware/Entry/AlreadyInBootloaderEntryStrategy.cs`, `TemporaryMavLinkRebootEntryStrategy.cs`, `ManualReconnectBootloaderEntryStrategy.cs`, `BootloaderEntryService.cs`, `BootloaderEntryContext.cs` | Initial probe, temporary reboot, discovery before fallback, ordered stage reporting and connection ownership guard. |
| `src/Core/MissionPlanner.Firmware/Installation/FirmwareInstallationService.cs` | Forward entry stages into the existing operation coordinator. |
| `src/Core/MissionPlanner.Firmware/Model/FirmwareOperationState.cs`, `src/Core/MissionPlanner.Firmware/Operations/FirmwareOperationCoordinator.cs` | Extend the existing state machine for automatic entry and fallback. |
| `src/UI/MissionPlanner.App/Views/InitSetup/InstallFirmware/FirmwareInteractionService.cs`, `InstallFirmwareViewModel.cs` | Centralized fallback wording and readable stage messages. |
| `src/Tests/MissionPlanner.Firmware.Tests/BootloaderEntryStrategyTests.cs`, `BootloaderDiscoveryServiceTests.cs` | Real strategy ordering, fallback, changed USB/COM identity, unrelated bootloader rejection, timeout/cancellation disposal, state transitions. |
| `src/Tests/MissionPlanner.Core.Tests/TemporaryMavLinkBootloaderGatewayTests.cs` | Missing ACK, silent port timeout/cancellation, write-time USB disappearance, resource cleanup. |
| This report | Implementation and verification record. |

## Verification

All commands run from the repository root, using the existing restored dependencies.

| Command | Result |
| --- | --- |
| `dotnet test src/Tests/MissionPlanner.Firmware.Tests/MissionPlanner.Firmware.Tests.csproj --no-restore -v quiet` | 211 passed, 1 existing hardware test skipped. Includes existing APJ mismatch/no-erase tests. |
| `dotnet test src/Tests/MissionPlanner.Core.Tests/MissionPlanner.Core.Tests.csproj --no-restore --filter FullyQualifiedName~TemporaryMavLinkBootloaderGatewayTests -v quiet` | 8 passed. |
| `dotnet test src/Tests/MissionPlanner.AvaloniaUI.Tests/MissionPlanner.AvaloniaUI.Tests.csproj --no-restore -p:UsedAvaloniaProducts= -v quiet` | 34 passed, including firmware dialog sequencing. |
| `dotnet build src/Platforms/MissionPlanner.Desktop/MissionPlanner.Desktop.csproj --no-restore -p:UsedAvaloniaProducts= -v quiet` | Succeeded. |
| `dotnet build src/Platforms/MissionPlanner.Browser/MissionPlanner.Browser.csproj --no-restore -p:UsedAvaloniaProducts= -v quiet` | Succeeded; existing unrelated compiler warnings remain. |
| `git diff --check` | Passed. |

## Remaining hardware validation

No controller was rebooted or flashed during this implementation. A final COM11/custom-APJ hardware test remains necessary. Devices that change both their USB serial identity and OS identity across reboot cannot be safely correlated to a different COM port using the current descriptors; discovery intentionally does not substitute an unrelated controller. Bare serial endpoints without stable metadata can only be matched by their selected port. Application UART baud rates other than the configured firmware baud rate, and firmware that does not expose an ArduPilot heartbeat, can require manual fallback. Native OS serial-open/dispose behavior remains driver dependent.
