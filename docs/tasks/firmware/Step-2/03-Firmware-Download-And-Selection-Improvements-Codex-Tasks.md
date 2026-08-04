# Codex Tasks — Firmware Download and Hardware-Target Selection

## Objective

Harden the current serial/APJ firmware implementation and provide a clear, non-destructive firmware-download workflow before adding DFU.

Do not replace the existing `MissionPlanner.Firmware` architecture.

## Required outcome

A disconnected user can:

```text
Browse catalogue
→ identify exact hardware target
→ download and validate firmware
→ inspect package/provenance/cache information
→ optionally install it later
```

The user must not need to begin bootloader discovery merely to prove that download and package parsing work.

---

# Task 1 — Correct current interaction defects

Status: Completed on 2026-08-04. Shared `FirmwareInteractionCodes` now owns host interaction identifiers, interaction boundaries preserve accept/reject results, and rejection cancels before discovery or erase. Automated coverage includes acceptance, operator rejection, and external-token cancellation.

## Scope

Inspect:

```text
MissionPlanner.Firmware/Entry/BootloaderEntryStrategies.cs
MissionPlanner.Firmware/Installation/InstallationInterfaces.cs
MissionPlanner.Firmware/Installation/FirmwareInstallationService.cs
MissionPlanner.App/.../InstallFirmware/FirmwareInteractionService.cs
```

## Changes

1. Replace duplicated interaction strings with shared typed codes or constants.
2. Resolve the current mismatch between:

```text
entry.manual-unplug-replug
bootloader.manual-reconnect
```

3. Change user interaction APIs so rejection is represented explicitly.
4. Do not discard confirmation Boolean results.
5. On rejection, stop before destructive work and return Cancelled or throw a controlled `OperationCanceledException`.
6. Keep caller-token cancellation distinct from user rejection where useful for diagnostics.

## Tests

- Every domain-emitted interaction code has a UI mapping.
- Accept continues.
- Reject cancels.
- External token cancellation cancels.
- No erase occurs after rejection.

## Acceptance

No raw interaction code appears in the UI, and Cancel reliably prevents continuation.

---

# Task 2 — Introduce typed device selection

Status: Completed on 2026-08-04. `FirmwareDeviceItemViewModel` preserves the complete serial descriptor and recommendation evidence, ambiguous matches require explicit selection, and the selected descriptor is passed to both bootloader entry and discovery. Discovery tests verify selected devices outrank unrelated COM devices.

## Problem

The view model currently converts `SerialDeviceDescriptor` objects into display strings. It cannot pass a selected application device into `BootloaderEntryContext`.

## Changes

Create:

```csharp
FirmwareDeviceItemViewModel
```

Expose:

```csharp
ObservableCollection<FirmwareDeviceItemViewModel> DetectedDevices
FirmwareDeviceItemViewModel? SelectedDevice
```

The item should preserve:

- `SerialDeviceDescriptor`.
- Port name.
- Stable OS ID.
- USB VID/PID.
- USB serial number.
- Manufacturer.
- Product name.
- Board hint.
- Detection/recommendation status.

Pass the selected descriptor into:

```csharp
BootloaderEntryContext.ApplicationDevice
BootloaderDiscoveryRequest.SelectedDevice or PreferredDevice
```

Add a strong preference for selected/newly arrived device in bootloader discovery.

## UX

- Auto-select only when there is exactly one high-confidence candidate.
- Otherwise require explicit choice.
- Explain why a device is recommended.

## Tests

- One device auto-selection.
- Multiple devices require selection.
- Selected application device enables temporary MAVLink reboot strategy.
- Port change after reboot is still detected.
- Wrong unrelated COM device is not preferred.

---

# Task 3 — Replace first-item firmware selection

Status: Completed on 2026-08-04. `FirmwareTargetSelector` provides typed filters, evidence reasons, and confidence; the UI displays every matching platform and never falls back to the first vehicle-family item. Search covers platform, manufacturer/brand, and board ID, while automatic selection requires exactly one current high-confidence hardware match.

## Problem

The current view model groups normal choices by vehicle type and selects the first item when no USB match is found. It also selects the first resulting choice automatically.

## Changes

Create a query/filter model:

```csharp
FirmwareTargetQuery
FirmwareTargetRecommendation
FirmwareTargetMatchReason
FirmwareTargetConfidence
```

Add filters:

- Vehicle family.
- Release channel.
- Platform.
- Manufacturer/brand.
- Board ID.
- Bootloader string.
- USB VID/PID.
- Version.
- Git SHA.

Add a search box matching platform, manufacturer, brand and board ID.

## Selection rules

1. Never select `FirstOrDefault()` merely because a catalogue returned items.
2. Select automatically only when all available evidence yields one unambiguous high-confidence target.
3. Clearly label recommendations:

```text
Exact USB match
Exact bootloader alias match
Previously selected target
Manual selection
```

4. Require explicit confirmation when target was manually chosen without hardware evidence.

## View model item details

Expose:

```text
Vehicle family
Manufacturer/brand
Platform
Board ID
Version
Channel
Git SHA
Artifact URL
Format
USB IDs
Bootloader aliases
```

## Frame imagery

Do not reproduce the original Mission Planner’s frame-geometry gallery.

Optional small vehicle-family icons are acceptable, but target selection must be textual/data-driven and centered on the hardware platform.

## Tests

- No selection with ambiguous targets.
- Exact USB match recommendation.
- Manufacturer/platform search.
- Board-ID search.
- Stable/Beta/Latest filtering.
- Specialized vehicle family remains distinct.

---

# Task 4 — Add a non-destructive Download & Validate use case

Status: Completed on 2026-08-04. `IFirmwarePreparationService` downloads, atomically stores, reparses, hash-validates, and checks manifest/package board identity without any device dependency. The UI exposes Download & Validate, package/cache evidence, URL copy, and installation reuses the prepared package.

## New core API

Add a presentation-neutral service, for example:

```csharp
public interface IFirmwarePreparationService
{
    Task<FirmwarePreparationResult> PrepareAsync(
        FirmwarePreparationRequest request,
        IProgress<FirmwareProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
```

The service must:

1. Resolve/download the selected artifact.
2. Store it atomically.
3. Open it from storage.
4. Parse APJ/PX4.
5. Validate package bounds.
6. Compare package board ID with manifest target board ID.
7. Calculate/record SHA-256.
8. Return package metadata and cache evidence.

It must not:

- Open serial ports.
- Probe devices.
- Enter bootloader mode.
- Erase/program/verify hardware.

## Result model

Include:

```text
Selected manifest entry
Artifact metadata
Package board ID
Description/platform/build
Internal image size
External image size
SHA-256
DownloadedAt
WasCacheHit
Cache identity/path abstraction
Warnings
```

## UI

Add buttons:

```text
Download & Validate
Install Validated Firmware
Copy Download URL
Save Copy As…
Open Cache Folder (desktop only)
Clear Cached Artifact
```

Installation should reuse the already validated package/artifact rather than redownloading it unnecessarily.

## State model

Add or reuse explicit non-destructive states:

```text
Downloading
ValidatingPackage
Prepared
```

Do not mark the whole firmware operation as destructive while only downloading.

## Tests

- Fresh download.
- Cache hit.
- Manifest/package board mismatch.
- Corrupt APJ.
- Oversized artifact.
- Missing content length.
- Cancellation.
- No serial/device calls.

---

# Task 5 — Add persistent catalogue cache

Status: Completed on 2026-08-04. `PersistentFirmwareCatalogCache` layers memory over atomic JSON persistence under an injected cache root and retains source, validators, retrieval time, content, and schema version. Restart, corrupt-entry, concurrent-reader, conditional-refresh, and stale-fallback behaviors are covered by automated tests.

## Requirements

Replace the single memory cache registration with a layered cache:

```text
Memory cache
    over
Persistent cache
```

Persist:

- Manifest payload or normalized catalogue.
- Source URI.
- ETag.
- Last-Modified.
- Downloaded time.
- Parser/schema version.

## Behavior

- Fresh memory entry wins.
- Persistent fresh entry is loaded after restart.
- Conditional HTTP refresh updates cache.
- Valid stale entry is returned if network fails.
- Corrupt persistent entry is discarded safely.
- Writes are atomic.

Use an injected application-cache path abstraction. Do not directly depend on MAUI file-system APIs in the firmware project.

## Tests

- Restart simulation.
- ETag 304.
- Last-Modified 304.
- Offline stale fallback.
- Corrupt persistent file.
- Concurrent readers/writer.
- Schema-version invalidation.

---

# Task 6 — Harden firmware HTTP configuration

Status: Completed on 2026-08-04. All manifest and artifact traffic uses the named `MissionPlanner.Firmware` client with configurable product identity and request timeout, bounded connect time, decompression, cancellation propagation, and existing streaming byte limits. No large-download retry policy is registered.

## Changes

Register a named/typed client:

```text
MissionPlanner.Firmware
```

Configure:

- MissionPlanner User-Agent.
- Bounded request timeout.
- Automatic decompression where appropriate.
- Maximum manifest/artifact bytes enforced by streaming code.
- Cancellation propagation.
- No unsafe automatic retry of large artifact downloads.

Make official manifest and firmware-host settings configurable through `FirmwareOptions`/configuration.

## Tests

- User-Agent present.
- Timeout honored.
- Cancellation honored.
- Oversized response rejected while streaming.
- No synchronous `.Result`/`.Wait()`.

---

# Task 7 — Make manifest parsing resilient per entry

Status: Completed on 2026-08-04. Entries parse independently with total/accepted/skipped diagnostics and categorized reasons. Invalid fields or unsupported formats skip one entry, unknown fields remain available, mirrors deduplicate deterministically, and invalid documents or all-invalid manifests fail.

## Changes

Parse each manifest entry independently.

Return diagnostics containing:

```text
Total entries
Accepted entries
Skipped entries
Skip reasons by category
```

Skip isolated malformed items for:

- Invalid URI.
- Invalid board ID.
- Invalid USB ID.
- Unknown/unsupported format.
- Missing required fields.

Fail the whole operation only when:

- JSON/gzip is invalid; or
- no usable entries remain.

Preserve unknown future fields where reasonable.

## Tests

- One malformed item among valid entries.
- All malformed.
- Unknown new fields.
- Invalid USB identifier.
- Duplicate mirror entries.

---

# Task 8 — Harden artifact cache

## Changes

1. Replace `%TEMP%` with an injected durable application-cache root.
2. Commit artifact and metadata atomically.
3. Add orphan/partial cleanup.
4. Revalidate size and SHA before cache reuse.
5. Add cache quota/age policy.
6. Add cache enumeration and removal APIs.
7. Never expose raw platform path as a required domain concept; return an optional diagnostic path through host adapter.

## Tests

- Metadata write failure leaves no valid partial entry.
- Process interruption simulation.
- Corrupt data.
- Corrupt metadata.
- Concurrent same-key download.
- Quota cleanup.

---

# Task 9 — Serialize catalogue refresh

## Changes

Implement one of:

- CancellationTokenSource per refresh; or
- monotonically increasing request version plus a semaphore.

Requirements:

- New channel selection cancels/invalidates the previous request.
- Only latest result mutates observable collections.
- Collection mutation occurs on dispatcher/UI thread.
- Refresh command exposes running state.
- Install selection is cleared only when it is no longer valid.

## Tests

- Stable request finishes after Beta request but is ignored.
- Rapid Stable/Beta/Latest changes.
- Cancellation during HTTP.
- No duplicate collection rows.

---

# Task 10 — Add safe pre-destructive cancellation

## UI

Add Cancel while state is:

```text
LoadingCatalog
Downloading
ValidatingPackage
WaitingForDevice
EnteringBootloader
IdentifyingBootloader
CheckingCompatibility
WaitingForApplication (where safe)
```

Disable or change semantics during:

```text
Erasing
Programming
Verifying
```

If cancellation is requested during a destructive stage:

- Record it.
- Do not tear down protocol mid-command.
- Continue to the next safe boundary.
- Explain the behavior in UI.

## Tests

- Cancel download.
- Cancel discovery.
- Cancel before confirmation.
- Cancel requested during erase does not dispose port abruptly.

---

# Task 11 — Update tests, documentation and user protocol

Update:

```text
docs/FIRMWARE.md
docs/tasks/firmware/Test matrix.md
docs/tasks/firmware/Hardware smoke test.md
```

Add the user protocol from:

```text
02-Firmware-Download-User-Test-Protocol.md
```

## Final acceptance

This task group is complete when:

1. The user can explicitly find the exact platform.
2. No ambiguous target is auto-selected.
3. Device selection remains typed through the workflow.
4. Temporary MAVLink reboot can be used when an application device is selected.
5. Download & Validate performs no hardware access.
6. Persistent catalogue cache works across restart.
7. Artifact cache is atomic and integrity-checked.
8. Cancel has reliable semantics.
9. Focused and complete firmware tests pass.
10. Karl can complete the download protocol without connecting a flight controller.
