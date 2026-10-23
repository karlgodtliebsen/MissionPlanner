# MissionPlanner Firmware Download — User Test Protocol

## Purpose

This protocol allows Karl to verify the firmware-catalogue and firmware-artifact download functionality separately from actual flight-controller programming.

The primary protocol uses the non-destructive **Download & Validate** command. It verifies catalogue, download, package, cache, and offline behavior without connecting or programming a flight controller.

## Safety boundary

Firmware download testing must not erase or program a flight controller.

Download & Validate must never open a serial port, request a bootloader transition, or start device discovery. Keep flight controllers disconnected while performing this protocol so the evidence has an unambiguous non-hardware boundary.

---

# Part A — Verify automated download tests

Run from the repository root.

## A1. Complete firmware test project

```powershell
dotnet test .\src\Tests\MissionPlanner.Firmware.Tests\MissionPlanner.Firmware.Tests.csproj -c Debug
```

Expected repository result at Step 03.11:

```text
133 passed
1 skipped manual-hardware test
0 failed
```

Record the actual current result rather than assuming this count remains unchanged.

## A2. Focused catalogue, HTTP, package and downloader tests

```powershell
dotnet test .\src\Tests\MissionPlanner.Firmware.Tests\MissionPlanner.Firmware.Tests.csproj `
  -c Debug `
  --filter "FullyQualifiedName~FirmwareCatalogTests|FullyQualifiedName~HttpFirmwareManifestClientTests|FullyQualifiedName~FirmwarePackageReaderTests|FullyQualifiedName~FirmwareArtifactDownloaderTests"
```

Pass criteria:

- No failed tests.
- Corrupt/truncated/oversized artifacts are rejected.
- Valid APJ/PX4 packages parse.
- Cache behavior passes.
- HTTP length and hash handling passes.

Save the console output as evidence.

---

# Part B — Catalogue and target-selection test

This part stops before artifact download and never invokes Install.

## B1. Prepare

1. Close other ground-control and serial applications.
2. Physically unplug every flight controller.
3. Unplug USB serial adapters where practical.
4. Start MissionPlanner.
5. Confirm that MissionPlanner is disconnected.
6. Open Setup → Install Firmware.

## B2. Verify catalogue retrieval

1. Select `Stable`.
2. Press **Refresh catalogue**.
3. Confirm that the status changes from loading to a nonzero choice count.
4. Record:
   - status text;
   - number of choices;
   - at least three displayed platforms;
   - date/time.
5. Select `Beta`; wait for refresh.
6. Select `Latest`; wait for refresh.
7. Return to `Stable`.
8. Press **All Options**.

Pass criteria:

- UI remains responsive.
- No duplicate or corrupted rows appear after channel changes.
- Each displayed item includes vehicle type, version, platform and board ID.
- Network failure produces a clear message or stale-cache result rather than an application crash.

Rapidly switch Stable/Beta/Latest once. The latest selected channel must win, with no duplicate or stale rows.

## B3. Select a known artifact

Select a target whose platform you can identify from the official ArduPilot firmware server.

Record:

```text
Vehicle family:
Release channel:
Version:
Platform:
Board ID:
```

Do not rely only on the vehicle family. Confirm the platform is an actual ArduPilot build target.

# Part C — Primary Download & Validate protocol

## C1. Select target explicitly

1. Open Install Firmware while disconnected.
2. Choose release channel.
3. Search by platform or manufacturer.
4. Select the exact board target.
5. Confirm that the details panel shows:
   - manufacturer/brand;
   - platform;
   - board ID;
   - vehicle family;
   - version;
   - Git SHA;
   - artifact URL;
   - format;
   - USB IDs/bootloader aliases where available.

Pass criteria:

- No firmware is preselected merely because it is first in a vehicle group.
- Install remains disabled until a target is explicitly selected.

## C2. Download and validate

1. Press **Download & Validate**.
2. Observe byte progress when content length is available.
3. Confirm completion without any serial-device prompt.
4. Confirm the result panel shows:
   - downloaded bytes;
   - SHA-256;
   - APJ magic/format;
   - package board ID;
   - internal image size;
   - external image size if present;
   - description/platform/build metadata;
   - cache ID;
   - source URL;
   - fresh-download versus cache status.

Pass criteria:

- No serial port is opened.
- No bootloader discovery starts.
- No erase/program/verify stage occurs.
- Package board ID agrees with the selected manifest entry.
- The result panel reports a validated package and Install is not invoked.

## C3. Cache test

1. Close and restart MissionPlanner.
2. Select the same artifact.
3. Press Download & Validate again.
4. Confirm the cache survives application restart.
5. Confirm integrity is rechecked before reuse.

Pass criteria:

- Persistent catalogue and artifact cache work across restart.
- Corrupt cache content is rejected and redownloaded.

## C4. Offline test

1. Complete one successful online catalogue and artifact download.
2. Close MissionPlanner.
3. Disconnect the PC from the network.
4. Restart MissionPlanner.
5. Open Install Firmware.

Pass criteria:

- A stale-but-valid cached catalogue is shown with an explicit stale/offline label.
- Previously downloaded artifacts remain inspectable.
- Selecting an uncached artifact produces a clear offline/download error.

## C5. Custom firmware test

1. Select a valid local `.apj` file.
2. Confirm MissionPlanner parses it without network access.
3. Verify board ID, platform, image size and build identity.
4. Select a malformed or renamed non-APJ file.

Pass criteria:

- Valid custom package becomes Validated.
- Malformed content is rejected before any device access.
- `.hex` is redirected to the DFU workflow rather than treated as APJ.

## C6. Release-risk presentation

Verify the UI presents:

- Stable: recommended for normal use.
- Beta: wider testing, possible defects.
- Latest: development build, experienced users only.
- Custom: provenance and compatibility are the user’s responsibility.

## Evidence sheet

```text
Date/time:
MissionPlanner commit:
Operating system:
Network state:
Release channel:
Vehicle family:
Manufacturer:
Platform:
Manifest board ID:
Package board ID:
Version:
Git SHA:
Source URL:
Downloaded bytes:
SHA-256:
Fresh/cache:
Cache location/ID:
Result:
Diagnostic report attached:
```

## Overall pass criteria

The firmware download subsystem is user-verified when:

1. Catalogue retrieval works for Stable/Beta/Latest.
2. Exact hardware target can be identified explicitly.
3. Download & Validate completes without accessing a flight controller.
4. APJ parsing verifies package metadata and bounds.
5. Cache reuse works across application restart.
6. Offline stale catalogue behavior is clear.
7. Corrupt artifacts are rejected.
8. Diagnostic evidence is copyable.
