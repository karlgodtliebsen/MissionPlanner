# TASK 01 — Model Firmware and Device Identity Sources

## Goal
Remove the ambiguous single notion of a detected board id.

## Required concepts
Model separately, using existing types where possible:

```csharp
PhysicalDeviceIdentity
BootloaderIdentity
RunningFirmwareIdentity
SelectedFirmwareIdentity
```

Suggested contents:

```text
PhysicalDeviceIdentity
  MCU family
  USB VID/PID
  hardware UID / serial

BootloaderIdentity
  board id
  target/platform
  bootloader version
  confidence/source

RunningFirmwareIdentity
  target/platform
  board id
  vehicle type
  firmware version
  git hash

SelectedFirmwareIdentity
  target/platform
  board id
  vehicle type
  version
  source kind / URI
```

## Source attribution
Add/retain explicit source information such as:

```text
UsbDevice
DfuDescriptor
BootloaderProtocol
AutopilotVersion
StatusText
ApjManifest
UserSelection
```

Do not overwrite one identity with another.

## AUTOPILOT_VERSION
Decode `board_version` into `RunningFirmwareIdentity.BoardId`.

Do not expose this value as physical-board identity.

## Running target
Capture boot/startup target strings where available, e.g. `speedybeef4`.

## Local APJ
Parse embedded metadata:
- board id
- platform/target
- vehicle type
- version
- git hash where available

Embedded APJ metadata is authoritative over folder/file naming.

## Tests
Cover:
- `134 << 16` decoding;
- APJ board-id parsing;
- running target from startup text;
- missing bootloader identity;
- conflicting identity sources remain separate.

## Acceptance
No ambiguous generic `DetectedBoardId` remains in application/domain identity models.
