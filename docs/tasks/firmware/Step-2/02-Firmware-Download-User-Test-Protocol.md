# MissionPlanner Firmware Download — User Test Protocol

## Purpose

This protocol allows Karl to verify the firmware-catalogue and firmware-artifact download functionality separately from actual flight-controller programming.

There are two protocols:

1. A temporary protocol that can be used against the current uploaded source.
2. The preferred protocol after Codex adds the non-destructive **Download & Validate** command.

## Safety boundary

Firmware download testing must not erase or program a flight controller.

For the temporary current-state test:

- Physically disconnect all flight controllers.
- Disconnect USB-to-serial adapters that could be mistaken for a controller.
- Do not put any controller into bootloader mode.
- Expect the install workflow to fail later with “device not found” after the artifact has been downloaded.

The preferred Download & Validate workflow must never open a serial port or start bootloader discovery.

---

# Part A — Verify automated download tests

Run from the repository root.

## A1. Complete firmware test project

```powershell
dotnet test .\src\Tests\MissionPlanner.Firmware.Tests\MissionPlanner.Firmware.Tests.csproj -c Debug
```

Expected repository result at the time of the uploaded snapshot:

```text
106 passed
1 skipped manual-hardware theory
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

# Part B — Current UI temporary download test

## Important limitation

The current UI has no Download Only command. Clicking Install performs download first and then proceeds toward bootloader discovery.

This temporary test is safe only when no flight controller or candidate serial device is physically connected.

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

Note: rapid channel switching is currently a known race-risk. Test normal deliberate changes first; record any duplicate/stale results.

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

## B4. Trigger current download path without hardware

1. Confirm again that no flight controller is connected.
2. Press **Install**.
3. Accept the final pre-download/install warning only if the selected platform information is correct.
4. Observe the operation state.
5. Confirm that it reaches:

```text
Downloading firmware
```

6. Wait for it to progress to a later non-destructive state such as:

```text
Waiting for flight controller
```

7. Do not connect hardware.
8. Allow the bounded discovery operation to terminate with device-not-found.

Pass criteria for the download portion:

- Download begins and completes without HTTP/package exception.
- The state advances beyond Downloading.
- Failure occurs at device discovery, not artifact download or APJ parsing.
- Diagnostic report identifies the selected source and the later device-discovery failure.

This is not the desired long-term workflow; it is only a temporary way to prove that download succeeded before device discovery.

## B5. Inspect artifact cache

Current cache location on Windows:

```text
%TEMP%\MissionPlanner\FirmwareArtifacts
```

Inspect it in PowerShell:

```powershell
$cache = Join-Path $env:TEMP 'MissionPlanner\FirmwareArtifacts'
Get-ChildItem $cache | Sort-Object LastWriteTime -Descending | Select-Object -First 10 Name, Length, LastWriteTime
```

A completed cached artifact currently consists of a hashed `.bin` file and a `.meta` file.

Inspect recent metadata:

```powershell
Get-ChildItem $cache -Filter *.meta |
  Sort-Object LastWriteTime -Descending |
  Select-Object -First 3 |
  ForEach-Object {
      "--- $($_.FullName)"
      Get-Content $_.FullName
  }
```

The metadata contains:

1. Cache key.
2. Source URI.
3. Download timestamp.
4. Size.
5. SHA-256.

Pass criteria:

- A new data/metadata pair is present.
- Source URI is the selected artifact URL.
- Size is greater than zero.
- SHA-256 is present.
- Timestamp corresponds to the test.

## B6. Verify cache reuse

Repeat the same selection and current temporary workflow.

Observe:

- It should not create uncontrolled duplicate cache artifacts for the same immutable source.
- Download should be faster or log/cache diagnostics should indicate cached use.
- Metadata remains valid.

Record whether the UI currently exposes cache use. It likely does not; this is a required improvement.

---

# Part C — Preferred Download & Validate protocol

Codex should implement the workflow in `03-Firmware-Download-And-Selection-Improvements-Codex-Tasks.md` before this becomes the primary user protocol.

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
   - cache path or cache ID;
   - source URL;
   - fresh-download versus cache status.

Pass criteria:

- No serial port is opened.
- No bootloader discovery starts.
- No erase/program/verify stage occurs.
- Package board ID agrees with the selected manifest entry.
- The operation completes as Validated.

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
