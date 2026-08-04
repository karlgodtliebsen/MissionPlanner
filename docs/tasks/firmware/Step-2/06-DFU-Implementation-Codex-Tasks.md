# Codex Tasks — STM32 USB DFU Firmware Installation

## Scope

Implement the first MissionPlanner DFU workflow for Windows using an installed STM32CubeProgrammer CLI.

This is an advanced initial-install/recovery workflow. It is separate from the existing ArduPilot serial/APJ installer.

## Explicit first-release constraints

Supported:

- Windows desktop.
- STM32 USB DFU/system bootloader.
- Official/local Intel HEX firmware.
- ArduPilot `*_with_bl.hex` initial/recovery packages.
- External STM32CubeProgrammer CLI provider.
- Program + verify.
- Device/driver/tool diagnostics.

Not supported initially:

- Native libusb DFU implementation.
- ST-LINK/SWD.
- Arbitrary binary address programming.
- Option bytes.
- Readout protection changes.
- OTP/security provisioning.
- Secure signed bootloader workflow.
- External flash loaders.
- Bootloader-only flash in normal UI.
- Linux/macOS execution.
- Mobile USB.

---

# Task 0 — Architecture discovery and ADR

Status: Completed on 2026-08-04. [ADR-DFU-STM32CubeProgrammer-CLI](../../../adr/ADR-DFU-STM32CubeProgrammer-CLI.md) records the accepted external-provider boundary, separate DFU/serial contracts, ST licensing and distribution constraint, explicit target limitation, global operation ownership, controlled process execution, and deferred destructive-stage cancellation policy before DFU source implementation begins.

Before editing:

1. Read all firmware docs/tasks.
2. Inspect current operation coordinator and installation abstractions.
3. Inspect platform/service registration conventions.
4. Locate application cache, external launcher and process abstractions.
5. Determine the correct project for Windows-specific implementations.

Create:

```text
docs/adr/ADR-DFU-STM32CubeProgrammer-CLI.md
```

Record:

- External CLI provider decision.
- Why native DFU is deferred.
- Tool licensing/distribution boundary.
- Serial versus DFU separation.
- Board-identification limitations.
- Safety/cancellation policy.

Acceptance:

- No source implementation before ADR is reviewed.

---

# Task 1 — Add DFU domain and service contracts

Status: Completed on 2026-08-04. `MissionPlanner.Firmware.Dfu` now defines immutable lifecycle, driver/tool, device, memory-range, artifact, progress, failure, programming, installation, and controlled-process models plus all eight platform-neutral service boundaries. A distinct global DFU operation kind participates in the existing coordinator lease, with tests proving serial, connected-update, and DFU operations cannot overlap.

Place platform-neutral contracts in `MissionPlanner.Firmware`, organized under a new `Dfu` namespace/folder.

Add models:

```text
DfuOperationState
DfuDriverState
DfuToolStatus
DfuProviderCapabilities
DfuDeviceDescriptor
DfuDeviceInformation
DfuArtifact
DfuArtifactMetadata
DfuMemoryRange
DfuProgrammingRequest
DfuProgrammingResult
DfuProgress
DfuFailure
```

Add interfaces:

```csharp
IDfuToolLocator
IDfuDeviceCatalog
IDfuDeviceMonitor
IDfuProgrammer
IDfuArtifactResolver
IIntelHexInspector
IDfuInstallationService
IDfuProcessRunner
```

Extend the global firmware operation coordinator so serial, connected-bootloader and DFU operations cannot overlap.

Acceptance:

- No UI/platform dependencies in core contracts.
- Immutable records and typed results.
- Operation exclusivity tests.

---

# Task 2 — Implement Intel HEX parser/inspector

Status: Completed on 2026-08-04. The platform-neutral inspector now performs bounded ASCII/HEX parsing, supports record types 00 through 05 required by the workflow, validates checksums, shapes, EOF, address overflow, duplicate and overlapping data, and applies configurable STM32 internal-flash, data-size, source-size, and span limits. It returns SHA-256 provenance and compact sorted ranges with separately stated bootloader/application evidence; malformed artifacts fail before any provider boundary.

Create a bounded Intel HEX parser.

Support records needed for STM32 firmware inspection:

- Data record `00`.
- End-of-file `01`.
- Extended segment address `02` if encountered.
- Start segment address `03` if encountered.
- Extended linear address `04`.
- Start linear address `05`.

Validate:

- Line prefix.
- Hex encoding.
- Byte count.
- Record checksum.
- Address calculation overflow.
- Required EOF.
- Overlapping/conflicting data.
- Maximum file size.
- Maximum represented address span.
- Duplicate records.

Return:

- Sorted merged data ranges.
- Total data bytes.
- Lowest/highest address.
- Entry/start address if present.
- SHA-256.
- Warnings.

Do not allocate one huge byte array spanning sparse addresses.

Add policy validation:

- Reject data outside configured STM32 flash address policy.
- Detect whether expected bootloader region and application region are populated.
- Mark package as “appears to contain bootloader” only as evidence, not absolute proof.

Tests:

- Valid `_with_bl.hex` fixture.
- Bad checksum.
- Missing EOF.
- Extended linear address.
- Sparse data.
- Overlap same bytes.
- Overlap conflicting bytes.
- Overflow.
- Oversized input.

Acceptance:

- A malformed HEX file cannot reach CubeProgrammer.

---

# Task 3 — Add Windows DFU USB device catalogue

Implement outside the UI-independent core, in the host or a Windows platform project.

Detect USB devices in DFU mode, including default STM32 identity:

```text
VID 0483
PID DF11
```

Do not require a COM port.

Capture where available:

- PnP instance ID.
- Device path.
- Friendly name.
- Manufacturer.
- VID/PID.
- USB serial number.
- Driver service/provider/version.
- Problem code/status.
- Arrival/removal time.

Use Windows device notifications with a polling fallback.

Map driver state:

```text
NotPresent
PresentReady
PresentWrongDriver
PresentWithProblem
Busy
Unknown
```

Tests use fake PnP snapshots; no hardware in CI.

Acceptance:

- `STM32 BOOTLOADER` can be detected without serial enumeration.
- Wrong-driver state is distinguishable from absence.

---

# Task 4 — Implement STM32CubeProgrammer tool locator

Create a Windows implementation of `IDfuToolLocator`.

Discovery sources:

1. User-configured executable path.
2. Known installation directories.
3. Registry/uninstall entries where reliable.
4. PATH as a final optional source.

Locate:

```text
STM32_Programmer_CLI.exe
```

Validate:

- File exists.
- It is executable.
- Product/file version where available.
- `--help` or version command completes within timeout.
- Minimum supported version policy.

Return statuses:

```text
Available
NotInstalled
PathInvalid
UnsupportedVersion
ExecutionBlocked
```

UI actions:

- Browse for executable.
- Open official STM32CubeProgrammer download page.
- Copy diagnostic.

Do not download or install CubeProgrammer automatically.

---

# Task 5 — Implement safe process runner

Implement `IDfuProcessRunner` using `ProcessStartInfo.ArgumentList`.

Requirements:

- No shell.
- Redirect stdout/stderr.
- Stream output lines asynchronously.
- Timestamp lines.
- Capture exit code.
- Bound startup and total execution.
- Support provider-controlled cancellation.
- Kill process tree only when safe and requested.
- Prevent arbitrary caller-supplied arguments.
- Preserve raw logs for diagnostics.

Add fake runner tests for:

- Successful output.
- Nonzero exit.
- Hung process.
- Cancellation.
- Large output.
- Malformed encoding.
- Executable missing.

---

# Task 6 — Implement CubeProgrammer CLI provider

Create:

```csharp
Stm32CubeProgrammerCliDfuProgrammer
```

Provider responsibilities:

- Report tool/version/capabilities.
- List/inspect USB DFU devices where supported by CLI.
- Connect to a selected USB index/provider device.
- Program a validated HEX file.
- Verify immediately after write.
- Optionally detach/start/reset only when capability and target behavior are known.
- Parse progress and key device information from CLI output.
- Preserve complete raw provider log.

Build commands through a dedicated version-aware command builder.

Typical concepts from vendor CLI:

```text
-c port=usb1
-w <file.hex>
-v
```

Do not hard-code an unverified reset/detach sequence. Use installed-version capability tests and official documentation.

Result must distinguish:

```text
ToolNotFound
NoDfuDevice
ConnectionFailed
FileRejected
EraseFailed
ProgrammingFailed
VerificationFailed
DetachFailed
Succeeded
```

Tests:

- Output fixtures from at least two tool versions where possible.
- Different progress formatting.
- USB index selection.
- Verify failure despite successful write.
- Non-English output fallback: rely on exit code/stable tokens conservatively and preserve raw output.

Acceptance:

- Success is impossible without verified provider result.

---

# Task 7 — Implement DFU artifact resolver

Create resolver paths:

## Official sibling resolver

Given an official APJ manifest entry, derive candidate `_with_bl.hex` only through a safe resolver.

Rules:

1. Source host/path must be recognized as official/configured ArduPilot firmware source.
2. Determine correct vehicle base name.
3. Resolve sibling URI in the same platform/version directory.
4. Verify existence using bounded request.
5. Download with the existing artifact downloader/storage infrastructure where appropriate.
6. Inspect Intel HEX.
7. Record source URI and SHA-256.

## Local file resolver

Allow user-selected:

```text
*.hex
*_with_bl.hex
```

Warn if filename does not indicate bootloader inclusion, but rely on range inspection and explicit confirmation rather than filename alone.

Do not silently substitute a bootloader-only image.

Tests:

- Copter/Plane/Rover/Sub naming.
- Missing sibling.
- Redirect/mirror.
- Nonofficial host rejected for derivation.
- Local custom file.

---

# Task 8 — Add DFU target-safety service

Create:

```csharp
IDfuTargetSafetyService
```

Inputs:

- Explicit selected ArduPilot platform.
- Manifest entry/board ID.
- Firmware HEX ranges.
- DFU MCU/device information.
- Previous application-device identity if available.
- Remembered association if available.

Output:

```text
Allowed
AllowedWithStrongWarning
Blocked
```

Never claim that STM32 chip identity proves the flight-controller PCB.

Block when:

- No platform selected.
- HEX invalid.
- Known incompatible MCU/flash range.
- File is clearly bootloader-only in normal install mode.
- Selected artifact is from another platform by known evidence.

Require typed confirmation phrase or equivalent for low-evidence manual target selection.

Tests cover shared MCU across different board platforms.

---

# Task 9 — Implement DFU installation orchestrator

Create a separate orchestrator:

```csharp
IDfuInstallationService
```

Workflow:

```text
Acquire global firmware lease
→ verify disconnected normal vehicle session
→ locate CubeProgrammer
→ resolve/download HEX
→ inspect HEX
→ wait for/select DFU device
→ inspect device/driver
→ evaluate target safety
→ show final confirmation
→ invoke program + verify
→ detach/reset or request power cycle
→ wait for DFU disappearance
→ wait for application serial device
→ return structured result
```

Cancellation policy:

- Safe before process starts.
- During vendor program/verify, follow provider capability. Do not kill the process merely because a UI token is cancelled unless safe behavior is documented.
- Record cancellation request and stop at safe boundary.

Result separates:

```text
ProgrammingVerified
ApplicationRediscovered
```

A verified flash with no returning application device remains a programming success with a reconnect warning.

---

# Task 10 — Add Advanced/Recovery DFU UI

Add a separate mode/tab/card, not mixed into normal serial Install.

Suggested title:

```text
Initial Install / DFU Recovery
```

UI steps:

1. Explain when DFU is needed.
2. Select exact ArduPilot hardware target.
3. Select official `_with_bl.hex` or local HEX.
4. Show STM32CubeProgrammer status/version.
5. Show DFU device/driver status.
6. Provide “How to enter DFU” and Device Manager guidance.
7. Inspect and display HEX ranges.
8. Display final target evidence and warning.
9. Program and verify.
10. Show reset/power-cycle/reconnect instructions.

Do not show:

- Option bytes.
- Arbitrary addresses.
- Memory editor.
- Readout protection.

During programming:

- Block duplicate actions/navigation.
- Show raw/summary logs.
- Show explicit verification stage.

---

# Task 11 — Add diagnostics

Diagnostic report includes:

```text
Operation ID
MissionPlanner version
OS
CubeProgrammer path/version
Provider capabilities
DFU PnP identity
VID/PID
Driver provider/service/version/problem code
STM32 device ID/revision
Selected ArduPilot platform/board ID
Vehicle family/version/channel
HEX filename/source/SHA-256
HEX address ranges/bytes
Sanitized CLI arguments
Exit code
Stage timings
Verification result
Raw provider log attachment/path
Application rediscovery result
```

Never log the entire firmware binary.

---

# Task 12 — Automated test suite

Add tests for:

- Domain validation.
- Operation exclusivity.
- Intel HEX parsing.
- Tool location.
- Command construction.
- Process runner.
- CLI output parsing.
- Driver/device states.
- Artifact resolution.
- Target safety.
- Full orchestrator success.
- Tool missing.
- No device.
- Wrong driver.
- Invalid HEX.
- Ambiguous board.
- User rejection.
- Program failure.
- Verify failure.
- DFU device remains present.
- Application not rediscovered.
- Cancellation at safe and unsafe stages.

No test may invoke a real CubeProgrammer executable in CI.

Use a separate optional integration category for an installed CLI with no connected hardware.

---

# Task 13 — Manual hardware protocol

Create:

```text
docs/tasks/firmware/DFU Hardware Test.md
```

Minimum hardware coverage:

- One STM32F4 flight controller.
- One STM32H7 flight controller.
- One board initially running non-ArduPilot firmware where practical.
- DFU entry by BOOT button/pads.
- Correct STM32 driver.
- Wrong-driver diagnostic.
- Official `_with_bl.hex`.
- Local custom `_with_bl.hex`.
- Verify and reboot.
- Returning COM device.
- Deliberate wrong-platform selection blocked before program.

Record exact board name, MCU, target platform, firmware URL/hash and evidence.

---

# Task 14 — Documentation/licensing

Update:

```text
docs/FIRMWARE.md
docs/tasks/firmware/MissionPlanner.Firmware.ScopeAndRoadmap.md
FEATURES.md
ai.md
```

Document:

- External tool dependency.
- Official install link.
- Licence/distribution decision.
- DFU versus serial workflow.
- `_with_bl.hex` rationale.
- Target identity limitation.
- Supported platforms.
- Driver troubleshooting.
- Recovery.

---

# Final acceptance criteria

1. Existing serial/APJ workflow remains green.
2. DFU is a separate operation kind and UI workflow.
3. CubeProgrammer installation is detected rather than bundled.
4. Windows DFU devices are detected without COM ports.
5. Valid Intel HEX is inspected before provider execution.
6. Exact ArduPilot platform must be selected explicitly.
7. Default official artifact is matching `_with_bl.hex` when available.
8. Program must be followed by verification.
9. Provider output and exit code are preserved.
10. No option-byte/security/arbitrary-memory controls are exposed.
11. Wrong platform is blocked before provider execution where evidence exists.
12. Shared MCU identity is never treated as proof of board platform.
13. F4 and H7 manual tests are documented.
14. CI uses fakes only.
15. A future native DFU provider can be added without changing UI/orchestrator contracts.
