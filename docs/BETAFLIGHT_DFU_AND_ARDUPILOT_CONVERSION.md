# Betaflight identity and DFU conversion

## Task progress

| Task | Status |
| --- | --- |
| 01 MSP protocol | Implemented; firmware tests pass |
| 02 Identity | Implemented; automated fixtures, hardware verification pending |
| 03 Discovery integration | Implemented in the existing firmware serial snapshot pipeline |
| 04 ROM DFU reboot | Implemented; hardware verification pending |
| 05 Physical correlation | Implemented; Windows physical acceptance pending |
| 06 Conversion | Pending |
| 07 UI | Pending |
| 08 Hardware acceptance and regression | Pending |
| 09 Betaflight firmware installation | Explicitly deferred |

## Protocol boundary

`Betaflight/Protocol` in MissionPlanner.Firmware contains bounded v1/native-v2 framing and
`IBetaflightMspClient`. It does not change MAVLink parsing. Requests use the existing
`IFirmwareSerialPort` stream under exclusive caller ownership. `MspPortConnector` wraps the
existing platform factory with typed busy/unsupported/cancellation/timeout outcomes and
disposes a port that opens after the deadline. Callers must await requests sequentially and
dispose the connection. Timed-out requests close the stream to abort uncancellable native reads;
another request requires a new connection. A completed write is retained as evidence separately
from a successful response, for the later reboot/disconnect race.

Frames are bounded to 254 bytes for v1 and 1024 for native v2. Jumbo and v2-over-v1 encapsulation
are unsupported. Noise and checksum failures resynchronize without an unbounded buffer. Deadline
and caller cancellation are separate typed outcomes. Native v2 coverage is limited to framing;
no unverified extended identity request is currently sent.

Command IDs were checked on 2026-09-09 against the upstream
[v1 header](https://github.com/betaflight/betaflight/blob/master/src/main/msp/msp_protocol.h) and
[v2 header](https://github.com/betaflight/betaflight/blob/master/src/main/msp/msp_protocol_v2_betaflight.h).
The full v2 header and `msp.c` confirm `MSP2_MCU_INFO = 0x300C`; the initial search result
missed this definition. Task 02 adds it as an optional native-v2 query. Older BOARD_INFO
MCU IDs remain visible as raw identifiers. MCU identity never implies ArduPilot board compatibility.

## Typed identity

`IBetaflightDeviceProbe` opens one exclusive serial endpoint, validates API major 1 and exact
four-byte `BTFL`, then reads available firmware/board/build/UID/name/MCU details. Another MSP
variant is never classified as Betaflight. Optional rejection or timeout preserves proven identity
with an incomplete-details diagnostic; caller cancellation still cancels the entire probe.
`BetaflightBoardInfo` retains manufacturer IDs, capability bits, revision, signature and MCU ID.
Legacy prefixes and unknown tails are accepted; incomplete length-prefixed fields are rejected.
Current version labels are retained separately from the three numeric firmware version bytes.

## Validation

## Physical DFU handoff

`IBetaflightDfuHandoff` snapshots pre-existing DFU devices before reboot, then consumes the existing
`IDfuDeviceMonitor`. Only a newly present 0483:DF11 endpoint at the exact physical USB location
can match. Multiple matches, missing topology, device removal, and a remaining selected serial
endpoint cannot become a firmware target. The result retains source identity and physical location.
MCU UID is never compared to the unrelated DFU serial representation. Monitor cancellation and
the firmware-operation lease are released on every terminal path.

Windows resolves `DEVPKEY_Device_LocationPaths` through Configuration Manager in
`MissionPlanner.Library.Windows/Firmware/WindowsUsbTopologyProvider.cs`; composite interface
suffixes are stripped to compare physical USB ports. Browser uses the unsupported provider and
stops before reboot. Existing Windows serial arrival timestamps now persist while the same device
remains present instead of changing on every scan, which is essential to cache/revalidation logic.

## ROM reboot

`BootloaderEntryTarget` separates the existing ArduPilot serial strategies from the Betaflight
STM32 ROM strategy. A proven Betaflight device cannot silently fall through into serial ArduPilot
programming. The ROM strategy requires current stable USB identity, retained full UID, and
protocol-reported STM32 name/target evidence; legacy numeric MCU IDs without verified naming
remain insufficient. It rechecks exact BTFL and UID on the exclusively opened port, locates ARM
through BOXIDS and STATUS, and rejects armed or unknown status before sending semantic ROM mode 1.
The transport is disposed before transition polling. An ACK is only reboot initiation; otherwise
a definitely completed write plus observed removal is required. Failed writes, MSP rejection,
stale identity, and a serial device that remains present are not success. Task 05 must still
correlate a DFU endpoint before any programming. Physical hardware acceptance remains separately recorded; automated coverage alone does not establish it.

## Discovery integration

The existing serial catalogue remains enumeration-only. `IFirmwareDeviceIdentityService` enriches
that same snapshot, retaining typed `BetaflightIdentity` on each descriptor without overwriting USB
strings. The firmware device panel invokes enrichment once per scan. Cache keys include stable OS/USB
identity, product and arrival generation; disappearance invalidates entries. Positive and negative
results expire after 30 seconds. The device refresh action forces re-probing; installation invalidates
cached evidence. Ports without stable identity are not cached. `BetaflightOptions` controls bounded
request/discovery/cache timing. Discovery obtains the existing global firmware-operation lease and
does not probe while the normal connection owns transport resources. Browser returns the original
snapshot without native serial probing. No second scanner or device registry was introduced.

The current `FirmwareFamily` is also an ArduPilot catalogue discriminator, unlike the enum described
in the task notes. It is unchanged: the typed Betaflight identity represents the runtime protocol
family separately, without creating an ArduPilot catalogue target or MAVLink vehicle.

Task 01: `dotnet test src/Tests/MissionPlanner.Firmware.Tests/MissionPlanner.Firmware.Tests.csproj --no-restore -v quiet`.
Hand-authored vectors cover v1 request/reply/error, fragmented and multiple frames, noise,
corruption/resynchronization, empty payloads, bounds, and native v2. Fake streams and an explicitly
advanced clock cover request timeout/cancellation/disconnect. No physical device was opened.

Physical acceptance remains pending. No board mappings, conversion success, or Pavo 20
compatibility have been established by this work.

## InstallFirmware-take2 presentation

The three contexts are Firmware, STM32 DFU and Help & Support. STM32 DFU has Device / Enter DFU,
Catalogue and Custom HEX subviews. They reuse existing protocol, catalogue, device, resolver and
programming services; no second scanner or flashing stack was added.

A successful physically correlated reboot retains the receipt and selected DFU generation and
opens DFU Catalogue without starting a flash. Failed/ambiguous handoffs remain on Device with an
actionable error. An already-present 0483:DF11 device can be selected without a COM port, but is
not assigned an exact FC target from its USB/MCU identity. Reviewed exact mappings are used only
when registered; otherwise selection is manual. No general Pavo/BETAFPV mapping was introduced.

DFU Catalogue presents the manifest entry as release identity and the resolved `*_with_bl.hex`
as the programming artifact. Custom HEX retains filename/local-path policy and requires an exact
platform. The selected source context prevents hidden local state from overriding catalogue
installation. Betaflight firmware installation remains deferred. The older task table tracks the
broader conversion/hardware effort; this UI restructure does not complete its pending acceptance.
