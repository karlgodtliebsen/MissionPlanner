# ADR: Use an installed STM32CubeProgrammer CLI for initial DFU support

- Status: Accepted
- Date: 2026-08-04

## Context

Mission Planner needs a Windows-first recovery and initial-install workflow for flight controllers in the STM32 ROM USB DFU bootloader. These devices normally enumerate as USB devices, not COM ports, so the existing ArduPilot serial bootloader abstractions cannot represent or program them safely.

The vendor-supported STM32CubeProgrammer supplies device-family flash algorithms, Intel HEX programming, verification, Windows DFU driver support, and a documented CLI. A native libusb implementation would require Mission Planner to own USB transport, STM32-family behavior, driver compatibility, programming safety, and cross-platform validation before the first useful recovery workflow.

STM32 DFU identity identifies the MCU family, not the exact flight-controller PCB. The common `0483:DF11` identity and `STM32 BOOTLOADER` label therefore cannot prove which ArduPilot target is correct.

## Decision

The initial DFU provider will control an existing user-installed `STM32_Programmer_CLI.exe`. Mission Planner will detect and validate the tool but will not bundle, redistribute, download, or silently install it.

Platform-neutral immutable contracts, Intel HEX inspection, safety policy, and orchestration belong in `MissionPlanner.Firmware` under a distinct `Dfu` namespace. Windows USB discovery, tool location, and process execution belong in the application host/platform services. DFU will not be modeled as a fake serial port and will not implement the serial bootloader client interface.

The global firmware operation coordinator remains the shared exclusivity boundary. Serial installation, connected bootloader update, and DFU may use separate protocol services, but only one firmware operation may hold destructive ownership at a time.

## Controlled provider boundary

The process provider must:

- validate the configured or discovered executable path;
- execute the program directly with `UseShellExecute = false`;
- construct arguments with `ProcessStartInfo.ArgumentList` rather than a shell command string;
- never invoke `cmd.exe` or PowerShell;
- redirect bounded stdout and stderr and capture the exit code;
- expose the executable version and provider capabilities;
- log sanitized structured arguments without firmware bytes or secrets;
- accept only arguments produced by typed Mission Planner requests;
- treat program plus verify as the minimum successful operation.

STMicroelectronics licenses and distributes STM32CubeProgrammer separately. Linking to the official installer does not grant Mission Planner redistribution rights. Any future bundling requires a separate licence review and architecture decision.

## Artifact and target policy

The normal serial workflow continues to use `.apj` or `.px4`. Initial installation and DFU recovery use a matching Intel HEX artifact, normally `*_with_bl.hex` so the application and ArduPilot bootloader are installed together.

Before the provider runs, Mission Planner will parse the HEX file with bounded input and address-span limits, validate records and checksums, reject conflicting overlaps and unsafe address ranges, and display the represented ranges and hash. Malformed or policy-invalid HEX must never reach CubeProgrammer.

The operator must explicitly select and confirm the exact ArduPilot platform. The final prompt must show the selected platform, vehicle/release provenance, file name and type, address ranges, detected STM32 identity, and a warning that DFU cannot prove the PCB target. VID/PID, product name, or STM32 device ID alone must never auto-select a board target.

The first release does not expose arbitrary binary addresses, bootloader-only programming in the normal UI, option bytes, readout protection, OTP/security provisioning, external loaders, ST-LINK/SWD, or secure provisioning workflows.

## Safety and cancellation

Detection, tool location, artifact resolution, download, HEX inspection, device inspection, and final confirmation are cancellable immediately.

Once erase/write begins, ordinary UI cancellation is recorded but must not abruptly kill CubeProgrammer unless the installed provider version has a documented safe-stop boundary. The workflow continues through verification or a provider-defined safe boundary, preserves bounded diagnostic output, and explains that power must remain connected. Navigation and concurrent firmware operations remain blocked while destructive ownership is active.

Successful programming and failure to rediscover the application are separate results. Mission Planner may request detach/start only when the provider reports that capability; otherwise it instructs the operator to remove the DFU boot condition and reset or power-cycle.

## Consequences

- The first implementation is Windows-only and requires a separate ST tool installation.
- Native cross-platform DFU is deferred, reducing initial protocol and driver risk.
- Automated tests can fake every tool, USB, artifact, parser, process, and orchestration boundary without hardware.
- Serial and DFU diagnostics remain distinct while sharing global operation exclusivity.
- Exact-board selection remains an operator responsibility because STM32 DFU evidence is insufficient to identify the flight-controller PCB.

## Revisit conditions

A native DFU provider may be proposed in a new ADR if cross-platform requirements, CLI limitations, maintenance needs, or licensing/distribution constraints justify owning the USB protocol and its safety validation.
