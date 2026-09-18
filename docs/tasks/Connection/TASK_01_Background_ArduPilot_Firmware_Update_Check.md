# TASK — Background ArduPilot Firmware Update Check

## Goal

Add a non-blocking background check which, after a vehicle has connected and its firmware identity is known, determines whether a newer **official/stable ArduPilot firmware** is available for that exact vehicle/board target and informs the user.

The check must never delay or fail the vehicle connection.

## Reference behavior in original MissionPlanner

Inspect the original MissionPlanner implementation, especially `MainV2.cs`.

After a successful connection, classic MissionPlanner starts a background task which:

1. reads the connected vehicle's `VersionString`;
2. calls `APFirmware.GetReleaseNewest(APFirmware.RELEASE_TYPES.OFFICIAL)`;
3. finds the corresponding vehicle family;
4. compares the installed version to the newest official version;
5. displays a "New firmware available" message when the online version is newer;
6. provides a release-notes link.

The relevant classic code compares `VersionDetection.GetVersion(comPort.MAV.VersionString)` against `item.MavFirmwareVersion`.

Do **not** copy the old `Task.Run`/UI implementation literally. Recreate the behavior using the existing Next Gen architecture, DI, async APIs and domain/application services.

If the repository contains the imported/original MissionPlanner source (`src-v.1.38` or equivalent), inspect it as an additional reference.

## Existing Next Gen functionality to reuse

Before writing new code, inspect and reuse the existing services for:

- `AUTOPILOT_VERSION` / connected vehicle firmware identity;
- vehicle board/platform identification;
- firmware manifest/catalog loading and caching;
- firmware target/artifact resolution;
- Install Firmware navigation/view models;
- application notifications/dialogs;
- cancellation/lifetime management.

Do not introduce a second firmware-catalog parser or a separate HTTP implementation.

## Important firmware identity requirement

A firmware update must be matched to the **actual compatible firmware target**, not just `BoardId`.

This is important because multiple ArduPilot artifacts can share a board id while representing different targets/build variants, for example:

```text
BETAFPV-F405
BETAFPV-F405-heli
BETAFPV-F405-I2C
BETAFPV-F405-I2C-heli
```

Therefore:

- do not identify the update artifact solely from board id;
- preserve exact platform/target identity;
- preserve vehicle family / MAV type / build variant;
- reuse the target-resolution rules already used by Install Firmware;
- if exact compatibility cannot be established, do not present a one-click upgrade to an arbitrary artifact.

False-positive firmware recommendations are worse than no recommendation.

## Suggested architecture

Introduce an application service similar in responsibility to:

```csharp
public interface IVehicleFirmwareUpdateService
{
    Task<FirmwareUpdateCheckResult> CheckAsync(
        VehicleId vehicleId,
        CancellationToken cancellationToken = default);
}
```

Suggested result model:

```csharp
public sealed record FirmwareUpdateCheckResult(
    FirmwareUpdateStatus Status,
    FirmwareSemanticVersion? InstalledVersion,
    FirmwareSemanticVersion? AvailableVersion,
    FirmwareManifestEntry? AvailableFirmware,
    string? Reason);
```

Possible statuses:

```csharp
public enum FirmwareUpdateStatus
{
    Unknown,
    UpToDate,
    UpdateAvailable,
    UnsupportedVehicle,
    TargetNotResolved,
    CatalogUnavailable
}
```

Names can be adapted to existing conventions.

Keep version comparison and compatibility rules out of the ViewModel.

## Trigger

Start the check after all of the following are true:

1. vehicle connection has completed;
2. vehicle is identified as ArduPilot;
3. firmware version has been obtained from `AUTOPILOT_VERSION` / existing identity service;
4. board/platform target resolution has completed sufficiently to establish compatibility.

Run it asynchronously.

The connection UI must already be usable while this happens.

A normal implementation should check once per successful connection. Reuse the existing manifest cache/TTL so reconnecting does not repeatedly download the complete manifest.

Do not poll the firmware servers every few seconds.

## Release channel policy

For automatic notifications, compare against the newest:

```text
OFFICIAL / stable
```

release for the exact target.

Do not automatically recommend:

- beta;
- development/latest;
- custom builds;
- another vehicle family;
- another board variant.

If the installed firmware is newer than current stable, custom, beta, or development firmware, do not incorrectly suggest a "downgrade" as an upgrade.

Version comparison must be semantic/numeric, not string comparison.

## User notification

If a newer compatible stable release exists, show a non-blocking notification/dialog with at least:

```text
Firmware update available

Vehicle:       Pavo20 Pro / SysID 1
Target:        BETAFPV-F405
Installed:     ArduCopter 4.7.1
Available:     ArduCopter 4.x.y Stable
```

Actions:

```text
View Upgrade
Later / Close
Release Notes
```

`View Upgrade` should navigate to the existing Install Firmware page and, where supported by the current navigation model, pre-filter/preselect:

- exact platform/target;
- vehicle family;
- build variant;
- Stable channel;
- available version.

Do **not** start flashing automatically.

The existing firmware installation workflow remains responsible for:

- connection/disconnection requirements;
- bootloader/DFU handling;
- artifact validation;
- actual flashing.

## Notification suppression

Avoid repeated prompts.

At minimum:

- only notify once per `vehicle identity + installed version + available version` during the application session;
- reconnecting the same unchanged vehicle must not immediately show the same prompt repeatedly.

If the application already has a persistent "do not show again" mechanism, integrate with it rather than creating a second preference system.

Optional persistent suppression may be keyed by:

```text
platform + vehicle type + available version
```

A newer release must be allowed to notify again.

## Failure behavior

Firmware update checking is opportunistic.

Network/catalog errors must:

- not affect vehicle connection;
- not show alarming modal errors during normal operation;
- be written to the application log at an appropriate level;
- allow a future connection/check to retry.

Cancellation during disconnect/application shutdown must be clean.

## Logging

Add structured application-log entries for:

- update check started;
- installed identity/version;
- resolved target;
- catalog cache hit/download where existing services expose it;
- no update;
- update available;
- target unresolved;
- check cancelled;
- check failed.

Do not log the entire firmware manifest.

## Tests

Add unit/application tests covering at least:

### Version comparison

```text
Installed 4.7.1, Available 4.7.1 -> UpToDate
Installed 4.7.1, Available 4.7.2 -> UpdateAvailable
Installed 4.8.0, Available 4.7.2 -> UpToDate / no downgrade prompt
```

### Target matching

Verify that targets sharing the same board id are not collapsed or confused.

For a catalogue containing:

```text
BETAFPV-F405
BETAFPV-F405-heli
BETAFPV-F405-I2C
BETAFPV-F405-I2C-heli
```

a connected `BETAFPV-F405` multicopter must resolve only to the compatible `BETAFPV-F405` multicopter artifact.

### Other cases

- non-ArduPilot vehicle -> no check/prompt;
- exact target unresolved -> no unsafe upgrade action;
- manifest unavailable -> connection unaffected;
- cancellation during disconnect;
- same update does not prompt twice in one session;
- newer release can prompt later;
- `View Upgrade` navigates with correct target/filter context.

Use fake catalog/clock/navigation/notification services where appropriate.

## Acceptance criteria

- Firmware update check runs in the background after a successful ArduPilot connection.
- Vehicle connection is never blocked by the check.
- Installed firmware version comes from the actual connected vehicle identity.
- Available firmware comes from the existing firmware catalogue.
- Only a newer official/stable compatible firmware produces a prompt.
- Exact target/build compatibility is preserved; BoardId alone is never treated as artifact identity.
- No automatic flashing occurs.
- Repeated reconnects do not spam the same notification.
- Failures are logged and otherwise non-disruptive.
- Relevant tests pass on desktop and Browser/WASM-compatible application layers.
