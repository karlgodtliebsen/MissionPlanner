# MissionPlanner Firmware — Scope and Roadmap

## Purpose



This document records the agreed implementation scope and the planned future expansion of the MissionPlanner firmware subsystem.

The intent is to prevent later development from unintentionally broadening the first implementation, mixing unrelated flashing technologies, or losing sight of deferred capabilities.



The new subsystem will be implemented in a dedicated project:

```text
MissionPlanner.Firmware
```

A corresponding test project should also be created:

```text
MissionPlanner.Firmware.Tests
```

The firmware library must remain independent of the MAUI user interface.

\---

# 1\. Functional distinction

MissionPlanner must treat these as two different operations.

## 1.1 Install application firmware

Normal ArduPilot application firmware installation requires the normal MAVLink session to be disconnected.

The physical flight-controller connection is temporarily owned by a bootloader uploader rather than by the normal MAVLink subsystem.

Typical sequence:

```text
Disconnected MissionPlanner session
    → select or load firmware
    → discover flight controller
    → enter or detect bootloader
    → identify board
    → validate compatibility
    → erase
    → program
    → verify
    → reboot
    → rediscover normal ArduPilot device
```

## 1.2 Update embedded bootloader

Bootloader Update is a connected-vehicle operation.

It uses the existing MAVLink connection to request that the running ArduPilot firmware writes its embedded bootloader image.

Typical sequence:

```text
Connected and disarmed vehicle
    → confirm bootloader update
    → send MAV\_CMD\_FLASH\_BOOTLOADER
    → process COMMAND\_ACK
    → instruct user to reboot the controller
```

This operation does not use the serial bootloader upload protocol and must not be implemented as a variation of normal firmware installation.

\---

# 2\. First implementation scope

The first release is deliberately limited to modern, common ArduPilot flight controllers and Windows desktop use.

## 2.1 Supported platform

* Windows desktop.
* Direct USB or USB-serial flight-controller connections.
* MAUI user interface hosted by the existing MissionPlanner UI project.

The core firmware project must remain platform-neutral even though the first concrete device-monitor implementation targets Windows.

## 2.2 Supported firmware packages

* ArduPilot `.apj` firmware packages.
* PX4-compatible JSON package variants where they use the same bootloader protocol and can be supported safely.
* Custom local `.apj` or compatible `.px4` files.
* Firmware selected from the official ArduPilot firmware manifest.

## 2.3 Supported release channels

* Stable/official.
* Beta.
* Latest/development.
* All compatible options for a detected board.
* Custom local firmware.

Historical firmware may be represented in the domain model but does not need a complete user workflow in the first release unless it falls out naturally from the manifest implementation.

## 2.4 Supported vehicle families

The catalogue should support entries available from the current manifest, including:

* Copter.
* Helicopter.
* Plane.
* Rover.
* Sub.
* Antenna Tracker.
* Blimp when available.

The UI must be data-driven. It must not assume that every vehicle family is present in every manifest response.

## 2.5 Supported bootloader workflow

The first release must support:

* Detecting a controller already running its bootloader.
* Rebooting a disconnected application device into its bootloader through a temporary minimal MAVLink session when practical.
* Manual unplug/replug or reset fallback.
* Flight-controller disappearance and re-enumeration.
* COM-port changes between application and bootloader modes.
* Bootloader synchronization and identification.
* Board-ID compatibility validation.
* Firmware image-size validation.
* Flash erase.
* Chunked programming.
* Checksum verification.
* Bootloader reboot.
* Detection of the returning ArduPilot application device.
* Optional reconnect suggestion to the main MissionPlanner connection subsystem.

## 2.6 Connected firmware page mode

When MissionPlanner has an active vehicle connection:

* Normal firmware installation is unavailable.
* The UI explains that the vehicle must be disconnected before loading application firmware.
* Firmware catalogue tiles and normal upload commands are hidden or disabled.
* Bootloader Update is available only when its prerequisites are satisfied.
* The vehicle must be disarmed before Bootloader Update.
* Command ACK results must be surfaced precisely.

## 2.7 Disconnected firmware page mode

When no vehicle connection is active:

* The firmware catalogue is visible.
* Stable, beta and latest/development selection is available.
* All Options is available.
* Custom firmware selection is available.
* Device discovery and flashing are available.
* Bootloader Update through the active MissionPlanner MAVLink connection is unavailable.

\---

# 3\. Project boundaries

## 3.1 MissionPlanner.Firmware responsibilities

`MissionPlanner.Firmware` owns:

* Firmware domain types.
* Firmware operation state machine.
* ArduPilot manifest retrieval abstractions.
* Manifest parsing and catalogue filtering.
* Firmware download abstractions.
* APJ/PX4 package parsing and validation.
* Firmware compatibility checking.
* Bootloader protocol implementation.
* Bootloader discovery orchestration.
* Device-transition matching logic.
* Firmware installation orchestration.
* Bootloader Update use-case abstraction.
* Progress, result and failure models.
* Operation exclusivity.
* Logging contracts.
* Platform-neutral serial and device-monitor interfaces.

## 3.2 UI project responsibilities

The existing MissionPlanner MAUI UI project owns:

* Firmware pages and view models.
* UraniumUI presentation.
* Connected/disconnected page switching.
* Confirmation and warning dialogs.
* File picker integration.
* Progress presentation.
* User interaction prompts.
* Navigation protection during unsafe flash stages.
* Adapters to the existing MissionPlanner connection subsystem.
* Windows-specific UI behavior.

## 3.3 Platform implementation responsibilities

Windows-specific code owns:

* Serial-device enumeration.
* USB VID/PID and product information retrieval.
* Device arrival/removal notifications.
* Polling fallback where notifications are not reliable.
* Stable Windows device identifiers.
* Mapping Windows device data into platform-neutral firmware descriptors.

This implementation may initially reside in the UI host project. It can later be extracted into:

```text
MissionPlanner.Firmware.Platforms.Windows
```

if the dependency graph warrants it.

## 3.4 Prohibited dependencies

`MissionPlanner.Firmware` must not reference:

* .NET MAUI.
* UraniumUI.
* CommunityToolkit MAUI controls.
* WinUI.
* Android, iOS or Mac Catalyst APIs.
* Application pages or view models.
* Global UI state.
* Static application service locators.

\---

# 4\. Architectural rules

## 4.1 Exclusive physical-device ownership

Only one subsystem may own the flight-controller serial device at a time.

```text
Normal MAVLink subsystem
        OR
Firmware bootloader subsystem
```

Normal application firmware installation must not start while the main MissionPlanner MAVLink session is connected.

Stopping only the message pump is not sufficient. The underlying serial stream must be closed and disposed before bootloader discovery or upload begins.

## 4.2 Connection identity

A COM-port name is a transient endpoint, not a stable device identity.

The workflow should match devices using as much information as is available:

* USB serial number.
* Stable operating-system device path.
* VID.
* PID.
* Product name.
* Manufacturer.
* Bootloader board ID.
* Arrival/removal timing.
* Known application-to-bootloader USB transitions.

## 4.3 New MAVLink session after flashing

After flashing and reboot:

* Do not reuse the old parser.
* Do not reuse old channels.
* Do not reuse pending command registrations.
* Do not reuse MAVFTP registrations.
* Do not reuse connection cancellation tokens.
* Do not reuse the old vehicle session.

A returning controller must be treated as a new connection.

## 4.4 Safety before erase

The firmware subsystem must complete all of the following before issuing erase:

* Firmware package parsed successfully.
* Package size bounded and validated.
* Bootloader identified.
* Board ID checked.
* Flash-size compatibility checked.
* External-flash requirements checked where relevant.
* Final user confirmation obtained.
* Exclusive operation lease held.
* Correct physical device selected.

## 4.5 Cancellation

Cancellation is safe before destructive flashing begins.

Cancellation is normally supported during:

* Catalogue retrieval.
* Firmware download.
* Package parsing.
* Device discovery.
* Waiting for user action.
* Preflight checks.

After erase or programming begins:

* Do not abruptly dispose the serial connection solely because a caller token was cancelled.
* Record cancellation as requested.
* Stop only at a protocol-defined safe boundary.
* Never report cancellation as successful completion.
* Clearly warn that power must not be removed.

## 4.6 Verification

Programming is not success.

Success requires:

* Complete firmware transfer.
* Successful protocol completion.
* Checksum or equivalent verification.
* Reboot command or documented bootloader exit.

Failure to rediscover the application device after a verified flash should be reported as:

```text
Firmware installation succeeded, but the returning application device was not detected.
```

It must not be reported as a flash failure.

## 4.7 Force operations

Board-ID mismatch, unsupported secure modes and flash-size mismatch must be blocked by default.

A future force mode must not be a casual boolean on the standard installation API.

It must require:

* A separately named advanced operation.
* Explicit application configuration.
* Strong warnings.
* Display of detected and selected board identities.
* Typed user acknowledgement.
* Recovery guidance.

\---

# 5\. First-release feature groups

## 5.1 Firmware catalogue

Implement:

* Official ArduPilot manifest retrieval.
* Gzip JSON support.
* In-memory cache.
* Persistent cache abstraction.
* Stale-cache fallback.
* Force refresh.
* Stable/beta/latest filters.
* Vehicle-type filters.
* Board-ID filters.
* USB VID/PID filters.
* All Options query.
* Unknown-field tolerance.
* Deterministic deduplication.

## 5.2 Firmware package reader

Implement:

* APJ/PX4 JSON parsing.
* Magic validation.
* Board-ID extraction.
* Base64 decoding.
* Zlib decompression.
* Image-size validation.
* Maximum-size limits.
* Internal and external image metadata.
* Firmware checksum behavior compatible with the upstream uploader.
* Protection against oversized allocation and decompression bombs.

## 5.3 Bootloader protocol

Implement the modern ArduPilot/PX4 serial bootloader protocol required for:

* Synchronization.
* Bootloader revision.
* Board ID.
* Board revision.
* Flash-size information.
* Chip information where supported.
* Erase.
* Internal flash programming.
* External flash programming where supported.
* Checksum retrieval.
* Verification.
* Reboot.

All reads must be bounded by explicit timeouts.

## 5.4 Device discovery

Implement:

* Initial serial-device snapshot.
* Device arrival/removal monitoring.
* Polling fallback.
* Candidate prioritization.
* Non-destructive bootloader probing.
* Immediate disposal of rejected candidates.
* Overall discovery timeout.
* Per-device open and synchronization timeouts.
* Manual reconnect prompt support.

## 5.5 Firmware operation orchestrator

Implement the complete workflow:

```text
Acquire exclusive operation
    → verify MissionPlanner is disconnected
    → resolve/download firmware
    → parse and validate package
    → select or discover physical device
    → enter or discover bootloader
    → identify bootloader
    → validate compatibility
    → request final confirmation
    → erase
    → program
    → verify
    → reboot
    → wait for application device
    → return structured result
```

## 5.6 Bootloader Update

Implement as a separate connected use case:

* Require active connection.
* Require disarmed vehicle.
* Require explicit confirmation.
* Use the existing MAVLink command service.
* Send the documented bootloader-flash command and parameters.
* Map all ACK outcomes.
* Explain unsupported-board results.
* Instruct the user to reboot after success.

\---

# 6\. User-interface states

The firmware page must be driven by a presentation mode model.

```text
Connected
Disconnected
OperationInProgress
UnsupportedPlatform
```

## 6.1 Connected

Show:

* Explanation that normal firmware cannot be loaded while connected via MAVLink.
* Instruction to disconnect.
* Bootloader Update action when supported.
* Current vehicle and connection summary where useful.

Hide or disable:

* Firmware catalogue.
* Custom firmware upload.
* All Options.
* Normal application firmware installation.

## 6.2 Disconnected

Show:

* Firmware catalogue.
* Release-channel selection.
* All Options.
* Custom firmware.
* Device status.
* Selected package metadata.
* Installation command.

## 6.3 Operation in progress

Show:

* Current operation stage.
* Progress percentage when meaningful.
* Technical status.
* Device identity.
* Selected firmware identity.
* Power-disconnection warning.

Disable:

* Duplicate start commands.
* Page navigation during unsafe stages.
* Release-channel changes.
* File selection.
* Application shutdown where practical.

## 6.4 Unsupported platform

Show a clear explanation that direct firmware installation is currently supported only on Windows desktop.

Connected Bootloader Update may remain available on another platform later if the existing MAVLink connection supports it, but this is not part of the first release.

\---

# 7\. Deferred features

The following capabilities are explicitly outside the first implementation.

They must remain visible in the roadmap and must not be partially mixed into the modern APJ workflow.

## 7.1 Install Firmware Legacy

Future support may include:

* AVR/Arduino boards.
* Intel HEX parsing.
* STK500/STK500v2 upload.
* Avrdude integration assessment.
* Retired-board warnings.
* VRBrain-specific logic.
* Legacy serial baud-rate behavior.
* Separate legacy UI route.

## 7.2 DFU and bootloader recovery

Future support may include:

* STM32 DFU device detection.
* `.hex` firmware.
* `\_with\_bl.hex` packages.
* Recovery of boards without a working serial bootloader.
* Bootloader installation through DFU.
* External-tool dependency assessment.
* Platform-specific driver guidance.

## 7.3 Secure firmware

Requires separate research and design:

* Secure bootloader variants.
* Signed firmware packages.
* Signing key management.
* Public-key installation.
* Key storage and revocation.
* Supported board matrix.
* Recovery implications.
* User warnings.
* Audit logging.
* Prevention of accidental insecure downgrade.

Secure flashing must not be implemented as a simple checkbox on normal firmware installation.

## 7.4 Force Bootloader and force flashing

Requires analysis of original Mission Planner behavior and current ArduPilot semantics.

Potential concerns include:

* Forcing bootloader mode.
* Bypassing board matching.
* Bootloader replacement.
* Recovery hardware requirements.
* Brick risk.
* Secure-board restrictions.

## 7.5 Historical firmware

Future work:

* Firmware history index.
* Commit/hash selection.
* Compatibility with historical manifests.
* Explicit downgrade warnings.
* Parameter backup and restore guidance.
* Bootloader compatibility checks before downgrade.

## 7.6 Additional transports

Future support may include:

* UART through telemetry adapters.
* Network-based firmware installation.
* BlueOS vehicle firmware workflows.
* DroneCAN node firmware.
* SD-card `.abin` update.
* Companion-computer update.
* Remote update with loss-of-link protections.

## 7.7 Additional platforms

Future support may include:

* Linux desktop.
* Mac Catalyst.
* Android USB host.
* iOS where platform restrictions permit.
* Platform-specific device monitoring and permissions.

## 7.8 Firmware signing and provenance

Future enhancements:

* Manifest signature validation.
* Firmware artifact hash verification.
* Download mirror trust policy.
* Provenance display.
* Cached-artifact integrity revalidation.
* Reproducible build metadata.

## 7.9 Parameter preservation workflow

Future UX may include:

* Automatic parameter backup before flashing.
* Firmware-version comparison.
* Reset-to-default recommendation.
* Parameter migration report.
* Restore with compatibility filtering.
* Changed/default parameter summary.

This must not be coupled into the initial bootloader transport implementation.

\---

# 8\. Testing roadmap

## 8.1 Automated tests required for first release

* Manifest parsing.
* Gzip and cache behavior.
* Catalogue filtering.
* APJ parsing.
* Corrupt package handling.
* Size limits.
* Known checksum vectors.
* State-machine transitions.
* Operation exclusivity.
* Device arrival/removal sequences.
* COM-port changes.
* Bootloader synchronization.
* Bootloader identify.
* Erase.
* Program.
* Verify.
* Reboot.
* Fragmented serial responses.
* Timeout behavior.
* Board mismatch.
* Insufficient flash.
* Wrong checksum.
* Connection-conflict rejection.
* Connected Bootloader Update ACK mapping.
* UI mode resolution.
* Command enablement.
* Duplicate-operation rejection.

## 8.2 Manual hardware tests required for first release

At minimum:

* One supported F4 controller.
* One supported H7 controller.
* Existing bootloader detected.
* Reboot from application to bootloader.
* COM-port changes during reboot.
* Successful stable firmware upload.
* Custom APJ upload.
* Wrong-board APJ rejection.
* Unplug/replug fallback.
* Repeated upload.
* Verification failure simulation where practical.
* Returning application-device detection.
* Connected Bootloader Update on a supported controller.

## 8.3 CI constraints

* CI must not require physical hardware.
* Hardware tests must be explicitly categorized as manual.
* Protocol tests must run against scripted or in-memory transports.
* Network tests should use fake HTTP handlers or committed fixtures.

\---

# 9\. Documentation requirements

Maintain:

```text
docs/Firmware.md
```

It should document:

* Architecture.
* Connected versus disconnected behavior.
* Project boundaries.
* Manifest handling.
* APJ package structure.
* Device identity.
* Bootloader protocol.
* Safety model.
* Cancellation rules.
* Recovery guidance.
* Supported features.
* Deferred features.
* Troubleshooting.
* Upstream code attribution and licensing.

Update as implementation progresses:

* `FEATURES.md`.
* `ai.md`.
* Relevant design documentation.
* Manual hardware-test records.

\---

# 10\. Licensing and attribution

Mission Planner and ArduPilot upstream code are GPLv3.

When porting or adapting:

* Mission Planner firmware utilities.
* PX4 uploader implementations.
* ArduPilot `uploader.py`.
* Protocol constants.
* Checksum algorithms.
* Firmware-format parsing.

Preserve applicable copyright notices and licence attribution.

Do not copy code into the new project without recording its origin.

\---

# 11\. Definition of done for the first release

The first firmware release is complete when:

1. `MissionPlanner.Firmware` builds as a UI-independent `net10.0` class library.
2. `MissionPlanner.Firmware.Tests` provides comprehensive automated coverage.
3. Connected mode blocks normal firmware installation.
4. Connected mode supports Bootloader Update through existing MAVLink command infrastructure.
5. Disconnected mode loads and caches the official firmware catalogue.
6. Stable, beta, latest, All Options and custom APJ selection work.
7. APJ packages are validated safely.
8. Windows device discovery handles disappearance, arrival and COM-port changes.
9. The bootloader is identified before erase.
10. Board-ID and flash-size compatibility checks are mandatory.
11. Flash erase, program, verify and reboot work with the supported bootloader protocol.
12. Verification is required before success.
13. Duplicate firmware operations are prevented.
14. Serial resources are disposed on every path.
15. Cancellation cannot abruptly interrupt unsafe flash stages.
16. Application-device rediscovery is reported separately from flash success.
17. Existing MissionPlanner MAVLink, parameter, MAVFTP and UI behavior remains unaffected.
18. At least one F4 and one H7 hardware smoke test are documented successfully.
19. Unsupported legacy, DFU, secure and additional-platform features remain clearly documented as future work.

\---

# 12\. Guiding principle

The firmware feature is a controlled transfer of exclusive communication ownership:

```text
MissionPlanner MAVLink subsystem
        ↓ release physical device
MissionPlanner firmware bootloader subsystem
        ↓ flash and reboot
MissionPlanner connection subsystem
        ↓ create a completely new MAVLink session
```

The first release should favor correctness, board safety, bounded operations, diagnostic clarity and recoverability over breadth of hardware support.

