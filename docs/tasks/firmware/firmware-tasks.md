## Feature

Implement the MissionPlanner firmware subsystem using a this class-library project:

```text
MissionPlanner.Firmware
```

Create:

```text
MissionPlanner.Firmware.Tests
```

The feature has two operating modes:

```text
Connected:
    Show connection warning.
    Disable normal firmware installation.
    Allow Bootloader Update through MAVLink.

Disconnected:
    Show firmware catalogue.
    Detect compatible USB/serial devices.
    Download, validate and upload firmware.
```

The subsystem must be designed for eventual cross-platform support, but the first functional bootloader upload implementation targets Windows.

---

# Task 0 — Repository and architecture discovery

Before changing source:

1. Read:

   * `ai.md`;
   * `docs/DesignConcepts.md`;
   * `FEATURES.md`;
   * existing transport and connection documentation;
   * existing project and test conventions.

2. Locate:

   * the solution file;
   * `MavLinkConnection`;
   * the vehicle connection manager;
   * serial transport interfaces and implementations;
   * command/ACK handling;
   * current Setup navigation and view-model composition;
   * current HTTP/file-cache abstractions;
   * existing DI registration style;
   * current platform-specific device discovery.

3. Determine the dependency graph between:

   * Core;
   * MAVLink;
   * Transport;
   * UI;
   * tests.

4. Produce a short implementation note before coding:

   * proposed project references;
   * proposed namespaces;
   * identified reusable existing abstractions;
   * any circular-dependency risk.

5. Run and record the baseline build and tests.

Do not create duplicate serial, HTTP, logging, clock, or dispatcher abstractions when equivalent interfaces already exist.

### Acceptance

* Baseline build succeeds.
* Codex has identified the correct existing extension points.
* No source changes beyond an optional architecture note.

---

# Task 1 — Create the projects

Create the class-library project:

```text
MissionPlanner.Firmware
```

Recommended properties:

```xml
<TargetFramework>net10.0</TargetFramework>
<Nullable>enable</Nullable>
<ImplicitUsings>enable</ImplicitUsings>
<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
```

Create:

```text
MissionPlanner.Firmware.Tests
```

Use the same test framework and assertion library as the existing solution.

## Dependency constraints

`MissionPlanner.Firmware` may reference:

* BCL packages;
* Microsoft logging/options/DI abstractions;
* a lower-level MissionPlanner Core or MAVLink project when unavoidable.

It must not reference:

* the MAUI UI project;
* UraniumUI;
* CommunityToolkit UI;
* WinUI;
* Android, iOS, or Mac Catalyst APIs.

The UI project may reference `MissionPlanner.Firmware`.

Prefer defining a narrow gateway inside `MissionPlanner.Firmware` and implementing it in the host rather than referencing the complete connection subsystem.

## DI registration

Add:

```csharp
public static class FirmwareServiceCollectionExtensions
{
    public static IServiceCollection AddMissionPlannerFirmware(
        this IServiceCollection services,
        Action<FirmwareOptions>? configure = null);
}
```

Add options validation at startup where appropriate.

### Acceptance

* Both projects are added to the solution.
* All configured targets build.
* Empty tests run successfully.
* No UI dependency exists in `MissionPlanner.Firmware`.

---

# Task 2 — Define the firmware domain model

Create immutable records and enums.

## Enums

```csharp
FirmwareReleaseChannel
{
    Stable,
    Beta,
    Latest,
    Historical,
    Custom
}

FirmwareVehicleType
{
    Copter,
    Helicopter,
    Plane,
    Rover,
    Sub,
    AntennaTracker,
    Blimp,
    Unknown
}

FirmwareImageFormat
{
    Apj,
    Px4,
    IntelHex,
    Abin,
    Unknown
}

FirmwareOperationKind
{
    InstallApplicationFirmware,
    UpdateEmbeddedBootloader
}

FirmwareOperationState
{
    Idle,
    LoadingCatalog,
    SelectingFirmware,
    Downloading,
    ValidatingPackage,
    WaitingForDevice,
    EnteringBootloader,
    IdentifyingBootloader,
    CheckingCompatibility,
    Erasing,
    Programming,
    Verifying,
    Rebooting,
    WaitingForApplication,
    Completed,
    Cancelled,
    Failed
}
```

## Value objects and records

Create types equivalent to:

```csharp
FirmwareVersion
FirmwareArtifact
FirmwareBoardTarget
FirmwareManifestEntry
UsbIdentifier
SerialDeviceDescriptor
ApjFirmwarePackage
BootloaderIdentity
FirmwareCompatibilityResult
FirmwareProgress
FirmwareOperationResult
FirmwareOperationFailure
```

`FirmwareProgress` should contain:

```csharp
FirmwareOperationState State
double? Percentage
string Message
long? CompletedBytes
long? TotalBytes
```

Do not put localized UI strings into the domain model. Prefer a stable message code plus optional technical detail.

## Typed exceptions

Add narrowly scoped exceptions:

```csharp
FirmwareManifestException
FirmwareDownloadException
FirmwarePackageException
FirmwareCompatibilityException
FirmwareDeviceNotFoundException
FirmwareBootloaderException
FirmwareVerificationException
FirmwareBusyException
FirmwareConnectionConflictException
```

### Acceptance

* Domain types contain no UI concepts.
* Domain types are immutable where practical.
* Equality and validation tests exist.
* Invalid board IDs, sizes, URLs, and release data are rejected.

---

# Task 3 — Implement the operation state machine

Create a state-machine component that owns legal firmware-operation transitions.

Example:

```text
Idle
  → Downloading
  → ValidatingPackage
  → WaitingForDevice
  → IdentifyingBootloader
  → CheckingCompatibility
  → Erasing
  → Programming
  → Verifying
  → Rebooting
  → Completed
```

Requirements:

* Illegal transitions throw a typed exception.
* Only one firmware operation may be active globally.
* Every state change publishes progress.
* State transitions are logged.
* The active operation has a unique operation ID.
* Terminal states are immutable.
* Cancellation rules depend on state.

## Cancellation policy

Allow normal cancellation during:

* catalogue loading;
* download;
* device discovery;
* preflight checks;
* waiting for user action.

Once flash erase/programming has started, cancellation must not abruptly close the serial connection. Either:

* disable cancellation until a safe protocol boundary; or
* record cancellation requested and stop only where the uploader protocol permits safe termination.

Do not claim that cancellation is safe after flash erase has begun.

### Acceptance

* All valid paths have tests.
* Illegal transitions have tests.
* Concurrent-operation attempts fail deterministically.
* Progress ordering is deterministic.

---

# Task 4 — Implement firmware manifest support

ArduPilot publishes a JSON firmware manifest containing fields such as board ID, MAV type, vehicle type, release type, version, USB identifiers, platform, bootloader strings, Git SHA and download URL. ([ArduPilot.org][5])

Create:

```csharp
public interface IFirmwareCatalogService
{
    Task<FirmwareCatalog> GetCatalogAsync(
        FirmwareCatalogRequest request,
        CancellationToken cancellationToken = default);
}
```

Supporting interfaces:

```csharp
IFirmwareManifestClient
IFirmwareManifestParser
IFirmwareCatalogCache
```

## Requirements

1. Support gzip-compressed JSON manifests.
2. Make the manifest URI configurable.
3. Use the official ArduPilot source as the default.
4. Parse unknown fields without failing.
5. Preserve raw release metadata for diagnostics.
6. Normalize:

   * release type;
   * MAV type;
   * vehicle type;
   * USB VID/PID;
   * board ID;
   * platform;
   * bootloader strings.
7. Support queries:

   * latest stable by vehicle type;
   * beta by vehicle type;
   * latest/development by vehicle type;
   * all firmware for a board;
   * firmware matching USB VID/PID;
   * firmware matching bootloader board ID;
   * all options.
8. Deduplicate mirrored or equivalent entries.
9. Prefer semantic versions where available.
10. Keep “latest build” distinct from stable and beta.

## Cache

Implement:

* in-memory cache;
* persistent local cache through an abstraction;
* cache timestamp;
* optional ETag/Last-Modified handling;
* stale-cache fallback when network retrieval fails;
* explicit force-refresh.

Do not place file-system paths directly into core catalogue logic.

### Tests

Use committed manifest fixtures covering:

* current ChibiOS board;
* Cube variants;
* multiple vehicle types;
* stable/beta/latest;
* duplicate entries;
* absent optional fields;
* malformed USB IDs;
* unknown future fields;
* corrupt gzip;
* corrupt JSON.

### Acceptance

* Catalogue can load without network in tests.
* Filters return deterministic results.
* A stale valid cache is available if HTTP fails.
* No synchronous `.Result` or `.Wait()` is used.

---

# Task 5 — Implement APJ package parsing

Create:

```csharp
public interface IFirmwarePackageReader
{
    Task<ApjFirmwarePackage> ReadAsync(
        Stream stream,
        CancellationToken cancellationToken = default);
}
```

Implement APJ and PX4 JSON package parsing.

## Validate

* JSON is structurally valid.
* Magic value is supported.
* Board ID exists and is valid.
* Internal image exists.
* Base64 data is valid.
* Compressed data is valid.
* Decompressed size matches declared size.
* Image does not exceed configured safety limits.
* External-flash image is handled when present.
* Declared maximum flash size is valid.
* Integer overflow and decompression-bomb cases are rejected.

## Preserve metadata

Include:

* board ID;
* board revision constraints, if present;
* description;
* summary/platform;
* image size;
* image maximum size;
* external image size;
* build version;
* Git identity;
* original package metadata.

## Checksum

Study the official ArduPilot `uploader.py` and original Mission Planner `px4uploader` implementation and port the exact firmware-padding and checksum behavior.

Do not invent a generic CRC implementation and assume it is protocol-compatible.

Add attribution and licence notices for any ported implementation.

### Tests

* Known valid APJ fixture.
* Wrong magic.
* Missing board ID.
* Invalid Base64.
* Invalid zlib stream.
* Declared-size mismatch.
* Oversized package.
* Internal plus external flash.
* Known checksum vectors from the upstream uploader.

### Acceptance

* Package parsing is deterministic.
* No unbounded allocations are performed.
* Known upstream checksum vectors pass.

---

# Task 6 — Define device and serial abstractions

Create platform-independent interfaces:

```csharp
public interface IFirmwareSerialDeviceCatalog
{
    Task<IReadOnlyList<SerialDeviceDescriptor>> GetDevicesAsync(
        CancellationToken cancellationToken = default);
}

public interface IFirmwareDeviceMonitor
{
    IAsyncEnumerable<FirmwareDeviceChange> WatchAsync(
        CancellationToken cancellationToken = default);
}

public interface IFirmwareSerialPortFactory
{
    Task<IFirmwareSerialPort> OpenAsync(
        SerialPortOpenOptions options,
        CancellationToken cancellationToken = default);
}

public interface IFirmwareSerialPort : IAsyncDisposable
{
    string PortName { get; }
    Stream Stream { get; }
    bool IsOpen { get; }
}
```

`SerialDeviceDescriptor` should support:

* transient port name;
* stable OS device identifier where available;
* VID;
* PID;
* USB serial number;
* product name;
* manufacturer;
* board hints;
* arrival timestamp.

Never treat `COM7` or equivalent as the stable device identity.

## Windows implementation

Implement in the appropriate platform/host project:

* current serial-port snapshot;
* device-arrival/removal monitoring where available;
* polling fallback;
* cancellation;
* deduplication;
* safe disposal.

Reuse existing MissionPlanner serial/device services where possible.

Do not scan and open every serial port continuously. Bootloader probing should occur only during an active firmware workflow.

### Tests

Use fake device catalogues and scripted arrival/removal sequences:

```text
COM7 application disappears
COM9 bootloader appears
COM9 disappears
COM8 application appears
```

### Acceptance

* Device re-enumeration with a changed COM port is handled.
* Monitoring ends promptly on cancellation.
* Duplicate OS notifications do not produce duplicate devices.

---

# Task 7 — Implement bootloader protocol transport

Create a protocol-focused client:

```csharp
public interface IArduPilotBootloaderClient : IAsyncDisposable
{
    Task<BootloaderIdentity> IdentifyAsync(
        CancellationToken cancellationToken = default);

    Task EraseAsync(
        CancellationToken cancellationToken = default);

    Task ProgramAsync(
        ApjFirmwarePackage package,
        IProgress<FirmwareProgress>? progress = null,
        CancellationToken cancellationToken = default);

    Task<FirmwareVerificationResult> VerifyAsync(
        ApjFirmwarePackage package,
        CancellationToken cancellationToken = default);

    Task RebootAsync(
        CancellationToken cancellationToken = default);
}
```

Implement the exact PX4/ArduPilot serial bootloader protocol based on the upstream uploader.

## Protocol requirements

Support the modern commands required for:

* synchronization;
* bootloader identification;
* board ID;
* board revision;
* bootloader revision;
* available application-flash size;
* chip information where supported;
* external-flash size where supported;
* erase;
* chunked programming;
* checksum retrieval;
* verification;
* reboot.

## Engineering requirements

* Centralize protocol constants.
* Use exact-length async reads.
* Handle partial reads.
* Enforce command-specific timeouts.
* Use bounded retries only where the protocol permits.
* Reject malformed replies.
* Validate sync/status bytes.
* Log protocol stages without logging entire binary images.
* Make time, retry and delay policies injectable for tests.
* Do not use arbitrary sleeps as the primary synchronization mechanism.
* Permit known bootloader-specific settle times through named options.

## Chunking

Respect:

* bootloader protocol maximum chunk size;
* serial buffering;
* image padding;
* internal versus external flash;
* alignment requirements.

## Tests

Create a scripted in-memory serial transport that can:

* fragment responses;
* delay replies;
* return invalid sync;
* time out;
* disconnect mid-erase;
* disconnect mid-program;
* report board mismatch;
* report insufficient flash;
* report wrong CRC;
* reboot successfully.

### Acceptance

* Protocol tests run without physical hardware.
* Every protocol timeout is bounded.
* No infinite serial read is possible.
* Board identity is available before erase.
* Verification failure never reports success.

---

# Task 8 — Implement bootloader discovery

Create:

```csharp
public interface IBootloaderDiscoveryService
{
    Task<DiscoveredBootloader> FindAsync(
        BootloaderDiscoveryRequest request,
        IProgress<FirmwareProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
```

## Discovery behavior

1. Snapshot serial devices before the operation.
2. Probe likely bootloader devices first:

   * newly arrived devices;
   * matching USB IDs;
   * matching product or bootloader strings;
   * explicitly selected device.
3. Open candidate at the required baud rate.
4. Synchronize and identify.
5. Close rejected candidates immediately.
6. Return both:

   * OS device identity;
   * bootloader identity.

## Safety

Do not erase or program during discovery.

Do not consider a serial port compatible merely because it can be opened.

## Timeouts

Use configurable:

* overall discovery timeout;
* per-port open timeout;
* sync timeout;
* retry interval.

## Manual fallback

Return progress states such as:

```text
Waiting for the flight controller bootloader.
Unplug and reconnect the controller if it is not detected.
```

The UI must remain responsive while waiting.

### Acceptance

* Discovery handles port-name changes.
* Wrong serial devices are ignored.
* A correct bootloader is returned only after identification.
* All candidate ports are disposed.

---

# Task 9 — Implement bootloader-entry strategies

Define:

```csharp
public interface IBootloaderEntryStrategy
{
    int Priority { get; }

    Task<BootloaderEntryResult> TryEnterAsync(
        BootloaderEntryContext context,
        CancellationToken cancellationToken = default);
}
```

Implement strategies:

### Already-in-bootloader strategy

Probe the selected or newly arrived device directly.

### Temporary MAVLink reboot strategy

When no active MissionPlanner session exists but the application serial port is available:

1. Acquire exclusive serial ownership.
2. Create a temporary, minimal MAVLink connection using existing MAVLink code.
3. Detect heartbeat with a bounded timeout.
4. Send reboot-to-bootloader.
5. Await ACK when appropriate.
6. Fully dispose the temporary MAVLink connection.
7. Wait for device removal and bootloader arrival.

Do not start the complete MissionPlanner vehicle session merely to issue the reboot.

### Manual reconnect strategy

Request user unplug/replug or hardware reset and then continue discovery.

The domain project should publish an interaction request; the UI decides how to present it.

### Acceptance

* Strategies are independently testable.
* Failure of one strategy permits the next appropriate strategy.
* Serial ownership is released before bootloader discovery begins.
* Temporary MAVLink channels and request registrations are not reused.

---

# Task 10 — Implement compatibility validation

Create:

```csharp
public interface IFirmwareCompatibilityService
{
    FirmwareCompatibilityResult Check(
        ApjFirmwarePackage firmware,
        BootloaderIdentity bootloader);
}
```

Validate:

* board ID;
* supported board revision constraints;
* firmware image size;
* bootloader-reported maximum firmware size;
* external-flash requirements;
* minimum bootloader revision where known;
* secure/non-secure compatibility when metadata supports it.

Default result for board mismatch:

```text
Blocked
```

Provide diagnostic details:

```text
Firmware board ID: 50
Detected board ID: 9
```

## Force mode

Do not implement force flashing as a boolean casually passed through the normal API.

Create a separately named advanced operation requiring:

* explicit application configuration;
* typed confirmation;
* repeated board details;
* acknowledgement that recovery hardware may be required.

It may remain unimplemented in the first release.

### Acceptance

* Mismatched firmware cannot reach erase/program stages.
* Image-size mismatch cannot reach erase.
* Compatibility results are visible to UI without parsing exception messages.

---

# Task 11 — Implement firmware download and storage

Create:

```csharp
public interface IFirmwareArtifactDownloader
{
    Task<DownloadedFirmwareArtifact> DownloadAsync(
        FirmwareArtifact artifact,
        IProgress<FirmwareProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
```

Requirements:

* HTTPS by default.
* Configurable maximum file size.
* Stream directly to temporary storage.
* Progress based on bytes when content length exists.
* Handle absent content length.
* Atomic completion: partial downloads are not returned as valid.
* Delete failed temporary files.
* Optional reusable cache keyed by immutable artifact identity.
* Store source URL, download time and metadata.
* Verify provided checksums when the manifest supplies one.
* Always parse and validate APJ content after download.

Custom firmware selection should pass a stream or file abstraction into the firmware project. The firmware project must not open MAUI file pickers.

### Acceptance

* Cancelled downloads remove partial files.
* Oversized downloads stop early.
* Corrupt files fail before device erase.
* Tests use fake HTTP handlers.

---

# Task 12 — Implement the firmware installation orchestrator

Create:

```csharp
public interface IFirmwareInstallationService
{
    Task<FirmwareOperationResult> InstallAsync(
        FirmwareInstallationRequest request,
        IProgress<FirmwareProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
```

## Workflow

```text
Acquire exclusive firmware-operation lease
    ↓
Verify normal MissionPlanner connection is disconnected
    ↓
Resolve or download firmware package
    ↓
Parse and validate APJ
    ↓
Find/select physical serial device
    ↓
Enter or discover bootloader
    ↓
Identify bootloader
    ↓
Check board and size compatibility
    ↓
Request final confirmation
    ↓
Erase
    ↓
Program
    ↓
Verify checksum
    ↓
Reboot
    ↓
Wait for application device
    ↓
Return result
```

## Connection gateway

Define:

```csharp
public interface IFirmwareConnectionGateway
{
    bool IsVehicleConnected { get; }

    ConnectionTransportKind? ActiveTransportKind { get; }

    Task RequestDisconnectAsync(
        CancellationToken cancellationToken = default);
}
```

For the first release:

* installation fails with `FirmwareConnectionConflictException` when connected;
* the UI shows the same guidance as original Mission Planner;
* automatic disconnect may be added later.

## Confirmation

Do not reference dialogs in the service. Define an interaction abstraction:

```csharp
public interface IFirmwareUserInteraction
{
    Task<bool> ConfirmInstallationAsync(
        FirmwareInstallationConfirmation confirmation,
        CancellationToken cancellationToken = default);

    Task AcknowledgeManualActionAsync(
        FirmwareManualAction action,
        CancellationToken cancellationToken = default);
}
```

The UI implementation can use the existing safe MissionPlanner modal/dialog approach.

### Acceptance

* Entire workflow is testable using fake services.
* No physical erase occurs before compatibility and final confirmation.
* Serial port is always disposed.
* Failure returns the stage, reason and technical detail.
* Success requires completed verification.

---

# Task 13 — Implement connected Bootloader Update

This use case is separate from normal firmware installation.

Create:

```csharp
public interface IEmbeddedBootloaderUpdateService
{
    Task<BootloaderUpdateResult> UpdateAsync(
        BootloaderUpdateRequest request,
        CancellationToken cancellationToken = default);
}
```

Define a host-provided MAVLink gateway:

```csharp
public interface IConnectedVehicleFirmwareGateway
{
    bool IsConnected { get; }
    bool IsArmed { get; }

    Task<CommandResult> FlashEmbeddedBootloaderAsync(
        CancellationToken cancellationToken = default);
}
```

The adapter should use the existing MissionPlanner command service and command-ACK infrastructure.

## Preconditions

* vehicle connected;
* vehicle disarmed;
* no firmware operation active;
* supported ArduPilot autopilot;
* explicit warning accepted.

## Command

Use the exact ArduPilot command and parameter semantics from official documentation. Do not duplicate raw MAVLink encoding if the project already has a command service.

## Result handling

Map:

* accepted;
* temporarily rejected;
* denied;
* unsupported;
* failed;
* timeout.

On success, inform the user that the controller must be rebooted.

Some boards do not contain an embedded bootloader image and may reject the operation; this is a supported failure result rather than a generic exception. ([ArduPilot.org][6])

### Acceptance

* The command cannot execute while armed.
* ACK results are surfaced precisely.
* No application-firmware uploader is invoked.
* Unit tests cover every ACK result.

---

# Task 14 — Implement the firmware page mode model

Create a presentation-neutral mode resolver:

```csharp
public enum FirmwarePageMode
{
    Connected,
    Disconnected,
    OperationInProgress,
    UnsupportedPlatform
}
```

Rules:

```text
Connected:
    Show warning that application firmware cannot be installed.
    Show Bootloader Update.
    Hide/disable catalogue flashing actions.

Disconnected:
    Show firmware catalogue.
    Show All Options.
    Show Stable/Beta/Latest selection.
    Show Load Custom Firmware.
    Show device status.

OperationInProgress:
    Show progress and current stage.
    Disable navigation that could abandon an unsafe operation.

UnsupportedPlatform:
    Explain that direct firmware installation is not available.
```

The resolver should consume connection state rather than having the view query global application objects.

### Acceptance

* Mode changes immediately when connection state changes.
* No normal install command can execute while connected.
* Bootloader Update is available only when its preconditions are met.

---

# Task 15 — Implement the MAUI/Uranium firmware UI

Implement the page in the existing UI project, not in `MissionPlanner.Firmware`.

Suggested types:

```text
FirmwarePage
FirmwarePageViewModel
FirmwareCatalogItemViewModel
FirmwareProgressViewModel
FirmwareInteractionService
```

## Connected view

Match the intent of the first screenshot:

* explanatory message;
* explicit Disconnect instruction;
* Bootloader Update action;
* no firmware vehicle tiles;
* no custom firmware action;
* no normal flash command.

A later enhancement may add:

```text
Disconnect and Continue
```

but not in the first version.

## Disconnected view

Use a data-driven firmware catalogue rather than fixed view-specific code.

Display primary stable vehicle choices:

* Rover;
* Plane;
* Copter;
* Helicopter where applicable;
* Sub;
* Antenna Tracker;
* Blimp when available.

Commands:

* select primary vehicle firmware;
* All Options;
* Stable;
* Beta;
* Latest/developer;
* Load Custom Firmware;
* refresh catalogue.

Do not assume every vehicle type exists in every manifest response.

## Board information

Show:

* detected device;
* board/platform;
* USB identity;
* bootloader board ID after detection;
* selected firmware board ID;
* selected version and channel.

## Progress

Display meaningful stages:

```text
Downloading firmware
Waiting for flight controller
Identifying bootloader
Checking compatibility
Erasing flash
Programming 43%
Verifying firmware
Rebooting
Waiting for ArduPilot
Completed
```

During erase/program/verify:

* disable page navigation;
* disable duplicate commands;
* prevent application shutdown where practical;
* present power-disconnection warnings.

## Dialogs

Do not introduce popup logic into `MissionPlanner.Firmware`.

Use the existing MissionPlanner interaction/modal abstraction. Because of the previous Windows handler-teardown issue, avoid binding the native teardown operation directly to an awaited button-command task where that pattern has already proven unstable.

### Acceptance

* Connected and disconnected presentations match the screenshots’ behavior.
* The UI is driven by state and catalogue data.
* The page remains responsive during discovery and upload.
* Duplicate flash clicks cannot start multiple operations.
* Re-entering the page does not retain stale operation state.

---

# Task 16 — Add local custom firmware

Implement UI file selection for:

* `.apj`;
* `.px4`.

Pass an opened stream or safe local-file abstraction to `IFirmwarePackageReader`.

Display parsed metadata before enabling upload:

```text
Description
Board ID
Platform
Image size
Build identity
```

Require final confirmation after the real bootloader identity has been obtained.

Do not support `.hex` in the modern bootloader workflow. Report that `.hex` requires a future DFU/legacy workflow.

ArduPilot distinguishes `.apj`/`.px4` GCS-loadable images from `.hex` and `_with_bl.hex` images intended for DFU-style tools. ([ArduPilot.org][7])

### Acceptance

* Invalid extensions are rejected clearly.
* Malformed files are rejected before device access.
* A custom board mismatch is blocked.
* File handles are disposed.

---

# Task 17 — Implement recovery and reconnect behavior

After successful bootloader reboot:

1. Observe bootloader-device removal.
2. Wait for the application device to appear.
3. Match it using:

   * USB serial number;
   * stable device path;
   * VID/PID;
   * known product transitions;
   * operation timing.
4. Report the new application port.
5. Optionally offer reconnect.

Do not automatically rebuild the old MAVLink session in the firmware library.

Expose:

```csharp
FirmwareOperationResult.ApplicationDevice
FirmwareOperationResult.ReconnectSuggested
```

The application connection layer decides whether and how to reconnect.

### Acceptance

* Success does not depend on retaining the original COM-port name.
* A missing application device produces “flash succeeded, reconnect not detected,” not “flash failed.”
* The old MAVLink parser, channels and pending registrations are never reused.

---

# Task 18 — Add diagnostics and structured logging

Use structured logging with:

* operation ID;
* firmware source;
* firmware board ID;
* detected bootloader board ID;
* original and bootloader device identifiers;
* state transition;
* retry count;
* elapsed time;
* bytes programmed;
* verification result.

Do not log:

* entire firmware images;
* unbounded binary responses;
* secrets or signing material.

Add an optional diagnostic report model suitable for copying from the UI.

Example:

```text
Operation: 2fc...
Package: Copter 4.7.0 stable
Firmware board ID: 9
Detected board ID: 9
Bootloader revision: 5
Original device: COM7
Bootloader device: COM9
Result: Verification failed
```

### Acceptance

* Every failure includes an operation ID and state.
* Logs distinguish discovery, compatibility, protocol and verification failures.
* Tests assert important structured events where existing test patterns support it.

---

# Task 19 — Documentation

Create:

```text
docs/Firmware.md
```

Include:

* scope;
* connected versus disconnected behavior;
* architecture diagram;
* dependency rules;
* firmware catalogue;
* APJ package format;
* serial-device ownership;
* bootloader protocol;
* state machine;
* safety model;
* cancellation model;
* supported platforms;
* supported image formats;
* unsupported legacy/DFU paths;
* troubleshooting;
* licence attribution.

Add a sequence diagram:

```text
UI
 → FirmwareInstallationService
 → ArtifactDownloader
 → PackageReader
 → DeviceDiscovery
 → BootloaderClient
 → Flight Controller Bootloader
```

Update:

* `FEATURES.md`;
* `ai.md` when new architectural constraints should guide future Codex work.

### Acceptance

* Documentation matches implemented behavior.
* Unsupported functions are stated explicitly.
* Upstream-derived code is attributed.

---

# Task 20 — Complete test matrix

## Unit tests

* manifest parsing;
* APJ parsing;
* compatibility;
* state transitions;
* catalogue filtering;
* release selection;
* download validation;
* operation exclusivity;
* connected-mode rules;
* bootloader ACK mapping.

## Protocol tests

* identify;
* erase;
* program;
* verify;
* reboot;
* fragmented reads;
* timeouts;
* malformed status;
* wrong CRC;
* disconnection.

## Orchestrator tests

* successful end-to-end simulated flash;
* connected-session rejection;
* download failure;
* invalid package;
* no device;
* board mismatch;
* insufficient flash;
* erase failure;
* program failure;
* verification failure;
* application device not rediscovered;
* duplicate operation request;
* cancellation before erase;
* cancellation request after erase starts.

## View-model tests

* connected view;
* disconnected view;
* operation-progress view;
* unsupported platform;
* command enablement;
* stable/beta/latest switching;
* custom-file selection;
* confirmation result;
* interaction request.

## Hardware tests

Create an explicitly excluded/manual test category for:

* one supported F4 board;
* one supported H7 board;
* port changes on reboot;
* repeated upload;
* board mismatch;
* manual unplug/replug;
* bootloader update command.

CI must not require physical hardware.

---

# Task 21 — Deferred feature backlog

Create tracked backlog entries rather than partial implementations.

## Legacy firmware installation

* AVR `.hex`;
* Arduino/STK protocol;
* VRBrain;
* retired board warnings;
* separate UI route matching Install Firmware Legacy.

## DFU

* STM32 DFU detection;
* `.hex` and `_with_bl.hex`;
* bootloader recovery;
* vendor/tool dependency assessment.

## Secure firmware

Research before implementation:

* secure bootloader variants;
* signed firmware;
* key storage;
* supported boards;
* key revocation and recovery;
* UI warning requirements.

Do not treat secure flashing as merely another APJ checkbox.

## Force Bootloader

Study the original Mission Planner implementation and upstream command semantics before exposing this action.

## Other transports

* UART through telemetry adapter;
* DroneCAN;
* BlueOS/network upload;
* SD-card `.abin`;
* mobile USB host support.

---

# Final acceptance criteria

The initial feature is complete when all of the following are true:

1. `MissionPlanner.Firmware` and its tests build cleanly.
2. The firmware project has no MAUI/UI dependency.
3. Connected mode blocks normal firmware installation.
4. Connected mode supports safe Bootloader Update through existing MAVLink command infrastructure.
5. Disconnected mode loads and caches the ArduPilot firmware catalogue.
6. Stable, beta, latest, all-options and custom APJ selection work.
7. APJ packages are parsed and bounded safely.
8. The bootloader is discovered even when the COM port changes.
9. Board-ID and image-size checks occur before erase.
10. Bootloader identify, erase, program, verify and reboot work against the simulated transport.
11. Verification is mandatory for success.
12. Duplicate operations are blocked.
13. The serial device is disposed on all paths.
14. Cancellation cannot strand the board through an abrupt mid-erase disconnect.
15. The application-device reappearance is reported separately from flash success.
16. All automated tests pass.
17. At least one real supported board completes a documented Windows hardware smoke test.
18. Existing MissionPlanner connection, parameter, MAVFTP and UI tests remain green.

The most important architectural boundary for Codex is:

> `MissionPlanner.Firmware` owns firmware metadata, package validation, device discovery orchestration, bootloader protocol and update workflows. The MAUI project owns presentation, user interaction, platform integration and adapters to the existing MissionPlanner connection system.

[1]: https://ardupilot.ardupilot.org/planner/docs/mission-planner-initial-setup.html "https://ardupilot.ardupilot.org/planner/docs/mission-planner-initial-setup.html"
[2]: https://ardupilot.org/dev/docs/bootloader.html "https://ardupilot.org/dev/docs/bootloader.html"
[3]: https://ardupilot.org/dev/docs/license-gplv3.html "https://ardupilot.org/dev/docs/license-gplv3.html"
[4]: https://cocalc.com/github/Ardupilot/ardupilot/blob/master/Tools/ardupilotwaf/chibios.py "https://cocalc.com/github/Ardupilot/ardupilot/blob/master/Tools/ardupilotwaf/chibios.py"
[5]: https://ardupilot.org/dev/docs/gcs-resources.html "https://ardupilot.org/dev/docs/gcs-resources.html"
[6]: https://ardupilot.org/copter/docs/common-bootloader-update.html "https://ardupilot.org/copter/docs/common-bootloader-update.html"
[7]: https://ardupilot.org/dev/docs/pre-built-binaries.html "https://ardupilot.org/dev/docs/pre-built-binaries.html"
