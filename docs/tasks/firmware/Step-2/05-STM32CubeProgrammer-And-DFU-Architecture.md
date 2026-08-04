# MissionPlanner DFU Architecture — Lessons from STM32CubeProgrammer

## Purpose

This document analyzes which STM32CubeProgrammer concepts should be adopted by MissionPlanner and defines the recommended first DFU architecture.

## STM32CubeProgrammer capabilities relevant to MissionPlanner

STM32CubeProgrammer provides:

- GUI, CLI and C API variants.
- USB DFU through the STM32 system-memory bootloader.
- ST-LINK/JTAG/SWD and other bootloader interfaces.
- Internal and supported external-memory erase/program/verify.
- Intel HEX, binary, ELF and S-record support.
- Target/device information.
- Progress and timestamped logs.
- Read/upload and memory inspection.
- Option-byte configuration.
- Security/provisioning functions.
- Automation through CLI.
- Windows, Linux and macOS versions.

Official references:

- `https://www.st.com/content/st_com/en/stm32cubeprogrammer.html`
- `https://dev.st.com/stm32cube-docs/prog/latest/en/index.html`
- `https://dev.st.com/stm32cube-docs/prog/latest/en/docs/markup/CubeProg_Command_Lines.html`

## What MissionPlanner should adopt

### Interface-first workflow

Show the communication mode clearly:

```text
ArduPilot Serial Bootloader
STM32 USB DFU
Future ST-LINK/SWD
```

Do not mix these into one ambiguous “port” picker.

### Device refresh and target information

Adopt:

- Explicit Refresh.
- List of detected DFU interfaces.
- Connected state.
- VID/PID.
- STM32 device ID/revision where available.
- Raw provider log.

### File inspection before programming

Adopt:

- Selected filename.
- File type.
- Address ranges.
- Total programmed bytes.
- Whether bootloader is included.
- Selected ArduPilot platform/board target.
- Source/provenance.

### Explicit stages

Adopt a clear sequence:

```text
Detect → Connect → Inspect → Confirm → Erase/Write → Verify → Start/Detach
```

### Verification as a first-class operation

Verification must be visible and mandatory for MissionPlanner success.

### Logs and diagnostic export

Adopt:

- Timestamped stage logs.
- User-selectable normal/verbose level.
- Copy/save diagnostic log.
- Provider version and executable path.
- Exact sanitized arguments.
- Exit code.

## What MissionPlanner should not adopt initially

Do not expose in the first DFU release:

- Arbitrary memory editor.
- Arbitrary read/write addresses.
- Option-byte modification.
- Readout-protection changes.
- OTP programming.
- Trusted-package/security provisioning.
- External loader selection.
- ST-LINK firmware update.
- Production automatic programming mode.
- Generic support for every STM32 interface.

These functions carry substantial brick/security risk and are not required for installing normal ArduPilot `_with_bl.hex` firmware.

## Architectural decision: external CLI first

### Decision

The first DFU implementation should use an installed STM32CubeProgrammer CLI provider.

Do not begin by writing a native USB DFU implementation.

### Rationale

The external provider gives MissionPlanner:

- Vendor-maintained STM32 support.
- Correct device-family flash algorithms.
- Intel HEX handling.
- Erase/program/verify behavior.
- Existing Windows DFU driver support.
- A documented CLI.
- Lower initial risk than a custom libusb protocol stack.

### Licensing/distribution boundary

STM32CubeProgrammer is free but not open source and is distributed under ST licence terms.

Initial MissionPlanner behavior should:

- Detect an existing user installation.
- Allow the user to configure the executable path.
- Link to the official installer.
- Not bundle or redistribute the tool until licence terms have been reviewed explicitly.

### Future decision

A native DFU provider may be added later if:

- Cross-platform integration requires it.
- CLI process control proves insufficient.
- Licensing/distribution constraints make external tooling unacceptable.
- The maintenance/safety cost is accepted.

## DFU is not a serial port

STM32 ROM DFU devices commonly enumerate as USB devices, not COM ports.

The normal serial abstractions cannot represent them adequately.

Create separate concepts:

```csharp
DfuDeviceDescriptor
IDfuDeviceCatalog
IDfuDeviceMonitor
IDfuProgrammer
IDfuToolLocator
IDfuProcessRunner
```

Do not add fake COM-port names to `SerialDeviceDescriptor`.

## Critical target-safety limitation

The STM32 DFU device identity generally identifies the MCU/device family, not the exact flight-controller PCB/platform.

For example, several unrelated flight controllers can use the same STM32 MCU and therefore expose similar DFU identity.

Therefore MissionPlanner must never infer the ArduPilot platform solely from:

- `STM32 BOOTLOADER` name;
- VID `0x0483`;
- PID `0xDF11`;
- STM32 chip/device ID.

The user must explicitly select the ArduPilot hardware target, assisted by:

- manufacturer/board name;
- board documentation;
- previously detected application USB identity;
- previously connected ArduPilot AUTOPILOT_VERSION/HW identity;
- remembered device-to-target association;
- firmware source path/manifest metadata.

Before write, show a strong confirmation containing:

```text
Selected ArduPilot platform
Vehicle family
Firmware version/channel
File name
File type
Address ranges
STM32 DFU device identity
Warning that DFU cannot prove the PCB target
```

## Firmware artifact strategy

### Normal serial install

Use `.apj`/`.px4`.

### Initial/recovery DFU install

Prefer the matching:

```text
<vehicle>_with_bl.hex
```

This installs application firmware and the ArduPilot bootloader in one DFU operation where the board build provides it.

### Bootloader-only image

Treat bootloader-only `.hex`/`.bin` as a separate advanced recovery action. Do not offer it in the normal DFU install flow.

### Artifact resolution

The official manifest primarily references APJ artifacts. A DFU artifact resolver may derive a sibling `_with_bl.hex` URI only when:

1. The selected entry is from a recognized official ArduPilot firmware directory.
2. The vehicle-specific base filename is known.
3. A bounded HEAD/GET proves the file exists.
4. The user sees the resolved URI and filename.
5. Download and Intel HEX inspection succeed.

Never replace `.apj` with `_with_bl.hex` through an unchecked string assumption.

Support local custom `_with_bl.hex` selection.

## Proposed domain model

```csharp
public enum FirmwareProgrammingTransport
{
    ArduPilotSerialBootloader,
    Stm32Dfu,
    StLink // future
}

public enum DfuOperationState
{
    Idle,
    LocatingTool,
    WaitingForDfuDevice,
    InspectingDevice,
    ResolvingArtifact,
    DownloadingArtifact,
    InspectingHex,
    AwaitingConfirmation,
    Programming,
    Verifying,
    Detaching,
    WaitingForApplication,
    Completed,
    Cancelled,
    Failed
}

public sealed record DfuDeviceDescriptor(
    string ProviderId,
    ushort VendorId,
    ushort ProductId,
    string? ProductName,
    string? SerialNumber,
    string? DevicePath,
    string? McuDeviceId,
    string? Revision,
    DfuDriverState DriverState);

public interface IDfuProgrammer
{
    Task<DfuProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<DfuDeviceDescriptor>> ListDevicesAsync(CancellationToken cancellationToken);
    Task<DfuDeviceInformation> InspectAsync(DfuDeviceDescriptor device, CancellationToken cancellationToken);
    Task<DfuProgrammingResult> ProgramAndVerifyAsync(
        DfuProgrammingRequest request,
        IProgress<DfuProgress>? progress,
        CancellationToken cancellationToken);
}
```

## Provider architecture

```text
DFU ViewModel
  → IDfuInstallationService
      → IDfuToolLocator
      → IDfuDeviceCatalog
      → IDfuArtifactResolver
      → IIntelHexInspector
      → IDfuProgrammer
          → Stm32CubeProgrammerCliDfuProgrammer
              → IDfuProcessRunner
                  → STM32_Programmer_CLI.exe
```

## Process execution safety

The provider must:

- Use `ProcessStartInfo.ArgumentList`; never build one shell command string.
- Set `UseShellExecute = false`.
- Redirect stdout/stderr.
- Never invoke `cmd.exe` or PowerShell to run the programmer.
- Validate executable path.
- Allow only controlled arguments.
- Quote paths through `ArgumentList` rather than manual quoting.
- Capture exit code and output.
- Kill the process tree on safe cancellation where the vendor operation allows it.
- Avoid cancellation during active erase/write unless CubeProgrammer documents safe stop behavior.
- Redact sensitive paths where appropriate in shared diagnostics.

## CLI command construction

Use version-aware command construction based on official CLI documentation and the installed tool’s help output.

Typical concepts include:

```text
-c port=usb1
-w <firmware.hex>
-v
```

Do not hard-code undocumented assumptions about reset/detach/start. Model them as provider capabilities and validate against the installed CLI version.

The default STM32 DFU USB identity is commonly:

```text
VID 0x0483
PID 0xDF11
```

but the provider must support custom VID/PID and must not treat default IDs as proof of the board platform.

## Intel HEX inspection

Even if CubeProgrammer performs the actual programming, MissionPlanner should parse enough Intel HEX to:

- Validate record checksums.
- Reject malformed files.
- Determine absolute address ranges.
- Detect overlapping/conflicting records.
- Calculate total programmed bytes.
- Identify whether data covers expected bootloader/application regions.
- Display ranges before confirmation.
- Prevent obviously dangerous out-of-range artifacts.

Do not attempt to reproduce all STM32 memory maps initially. Use a conservative board/MCU policy and provider inspection results.

## Operation separation

The existing firmware operation coordinator can be extended to prevent simultaneous serial and DFU operations, but do not force both protocols through one client interface.

Recommended operation kinds:

```text
InstallApplicationFirmwareSerial
InstallApplicationAndBootloaderDfu
InstallBootloaderOnlyDfu (future/advanced)
UpdateEmbeddedBootloaderMavLink
```

## Recovery behavior

After programming/verifying:

- Ask provider to detach/start only when supported.
- Otherwise instruct user to disconnect BOOT/DFU condition and power-cycle/reset.
- Monitor disappearance of DFU device.
- Monitor appearance of normal application serial device.
- Report “programming succeeded, application not rediscovered” separately.

## Acceptance for architecture phase

1. ADR documents external CLI decision.
2. DFU domain types do not depend on MAUI/WinUI.
3. Serial and DFU workflows remain distinct.
4. Exact ArduPilot platform selection is mandatory.
5. `_with_bl.hex` is the default initial/recovery artifact type.
6. CubeProgrammer is detected, not bundled.
7. All process arguments are controlled and logged safely.
8. No option-byte/security/memory-editor functionality is exposed.
9. Provider is fully fakeable for automated tests.
