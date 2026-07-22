# MAVLink Dialect Coverage

MissionPlanner's MAVLink wire catalog is based on the official `ardupilotmega.xml`
dialect and all of its transitive includes. The vendored inputs are pinned to
`mavlink/mavlink` revision `de1e078a3a7c53c9262a95b7417959a0f8bf4150`; provenance,
license, and the complete include set are recorded in
`src/Core/MissionPlanner.MavLink/Dialects/README.md`.

## Coverage baseline

Task 01 introduced a deterministic, read-only inventory tool. Run it from the repository
root with:

```powershell
dotnet run --project src/Tools/MissionPlanner.MavLink.Generator/MissionPlanner.MavLink.Generator.csproj -- .
```

The default output is the ignored transient file `artifacts/mavlink-coverage.json`.
The report contains the official dialect, ID, name, CRC extra, minimum and maximum
payload lengths, current registry/model/decoder/domain coverage, and a coverage
classification for every inherited message. It does not affect application runtime.

At the initial baseline, the resolved dialect contains 325 messages:

| Classification | Count |
|---|---:|
| Registry only | 272 |
| Typed wire message | 4 |
| Domain telemetry | 18 |
| Protocol workflow | 21 |
| Deprecated | 10 |

Current implementation coverage is 41 `MessageIds` constants, 29 CRC entries, 29 typed
models, 30 typed decoders, 18 domain-handled messages, and 16 messages producing domain
observations. These counts describe the Task 01 baseline and will change as subsequent
full-dialect tasks are completed.

The baseline explicitly identifies `MessageIds.MissionChanged = 52` as a legacy constant
that is absent from the pinned official dialect. It is retained during the inventory-only
task to avoid changing the public runtime surface. No decoder or CRC registration uses it.

Tests in `MavLinkCoverageBaselineTests` verify inheritance uniqueness, representative
official and legacy-generated wire values, existing constants and CRC values, decoder ID
membership, and byte-stable report generation. The legacy tree remains read-only.

## Generated message-definition registry

Task 02 replaces the hand-written CRC switch with a generated registry containing all 325
resolved messages. Each immutable definition exposes its ID, name, CRC extra, minimum and
maximum payload length, declaring dialect, and deprecation state. Lookup uses a frozen
dictionary; typed message decoders are not involved in frame validation.

The MAVLink 2 parser now rejects known messages outside their generated payload-length
window, accepts lengths from the non-extension minimum through the extension-inclusive
maximum, handles the optional 13-byte signature separately, and includes the generated
message name and length bounds in rejection diagnostics. Unknown IDs remain rejected at
this stage; Task 04 adds their lossless raw fallback behavior.

`CommonMavLinkCrcExtraProvider` remains as a compatibility adapter over the registry. The
older MAVLink v1 packet builder also uses the same generated definitions, eliminating its
separate CRC table. The coverage report consequently shows CRC validation for all 325
selected-dialect messages.

The committed registry source is
`src/Core/MissionPlanner.MavLink/Generated/MavLinkMessageDefinitions.g.cs`. It carries an
auto-generated marker, source revision, dialect provenance, and manual-edit warning. Its
Task 02 generation command is:

```powershell
dotnet run --project src/Tools/MissionPlanner.MavLink.Generator/MissionPlanner.MavLink.Generator.csproj -- registry . src/Core/MissionPlanner.MavLink/Generated/MavLinkMessageDefinitions.g.cs
```

`MavLinkMessageDefinitionRegistryTests` compare every generated definition with the
vendored XML, detect generated-source drift, exercise formerly unknown ArduPilot frames,
and cover valid, invalid, truncated-extension, zero-payload, and signed-frame lengths.

## Generated protocol enums and commands

Task 03 resolves and merges all 221 enum definitions in the selected dialect include
graph. The generated `MavLinkEnums.g.cs` contains protocol-only enum types in
`MissionPlanner.MavLink.Generated`, including the complete `MavCmd` catalog and the
camera, gimbal, ADS-B, generator, EFI, ESC, fence, rally, mount, mission, parameter,
navigation, and ArduPilot-specific values.

Underlying C# storage types are inferred from the widest XML message field that references
each enum and widened when necessary for the declared values. `[Flags]` is emitted only
when XML declares `bitmask="true"`. Decoders may cast undefined numeric values to these
types; generated code does not use `Enum.IsDefined` or otherwise discard future values.

The small `MavLinkCommandIds` compatibility facade now derives arm/disarm, set-mode,
request-message, and legacy home-position constants from generated `MavCmd` members.
Existing command services no longer contain literal command numbers.

Generated protocol enums do not replace domain concepts such as `FirmwareFamily` or the
domain capability subset. `MavLinkDomainMappings` explicitly translates between those
layers. This also corrected blimp identity mapping to official `MAV_TYPE_AIRSHIP` (7),
instead of the unrelated current `MAV_TYPE_WINCH` value (42).

Regenerate the enum source with:

```powershell
dotnet run --project src/Tools/MissionPlanner.MavLink.Generator/MissionPlanner.MavLink.Generator.csproj -- enums . src/Core/MissionPlanner.MavLink/Generated/MavLinkEnums.g.cs
```

`MavLinkGeneratedEnumTests` cover representative values and storage types, flags,
undefined-value round trips, compatibility command values, domain mappings, enum-count
uniqueness, and deterministic source drift.

## Lossless raw-message fallback

Task 04 separates frame validity from typed decoder coverage. After the parser validates a
selected-dialect frame's length and CRC, `MavLinkMessageDecoders` first offers it to a
registered typed decoder. When no typed decoder exists, or that decoder declines the frame,
the pipeline emits `RawMavLinkMessage` instead of silently discarding valid traffic.

The raw message preserves the MAVLink version, sequence, system and component IDs, actual
message ID, MAVLink 2 compatibility flags, exact payload, signature bytes, complete original
frame, receive endpoint and timestamp, and the generated message definition and name. It
does not use a synthetic fallback message ID. Typed messages continue to win whenever their
decoder accepts the frame, so protocol workflows such as MAVFTP are unchanged.

Unhandled raw messages are diagnostic traffic, not connection faults, and are logged at
debug level with their ID, known name, and payload length. IDs absent from the selected
dialect remain rejected by the frame parser because their CRC extra cannot be established;
the parser emits a trace diagnostic containing the unknown ID. Such a frame cannot safely
be promoted to a CRC-valid raw message until its dialect definition is installed.

`MavLinkRawFallbackTests` cover lossless signed-frame preservation, typed-decoder priority,
decoder rejection fallback, invalid CRC rejection, the unknown-ID policy, debug-level
dispatch logging, and removal of the old fictitious fallback ID.

## Generated wire models and decoders

Task 05 generates protocol-order C# records and registry-validated decoders for every
non-deprecated selected-dialect message that does not have an established hand-written
wire contract. This produces 287 generated models and decoders: 283 non-deprecated
messages plus `HWSTATUS`, `BATTERY2`, `MISSION_ITEM`, and `MISSION_REQUEST`, which are
retained as explicit deprecated compatibility exceptions because they remain useful on
supported ArduPilot vehicles. Together with the 32 hand-written overrides, all 315
non-deprecated messages have typed decode coverage.

Generated scalar properties use the exact signed, unsigned, and IEEE wire primitive.
Numeric fixed arrays retain their element type; fixed `char` arrays are exposed as ASCII
strings and stop at the first null. Models expose fields in XML protocol order even though
the generated codec reads and writes MAVLink's size-sorted base-field wire order. MAVLink 2
extension fields are padded with protocol zero defaults when any trailing bytes are omitted,
including truncation partway through a multi-byte field.

Every generated model derives from `GeneratedMavLinkMessage`. Its `EncodePayload` method
provides the outbound representation and can either retain maximum schema length or trim
zero-valued extension suffixes to the registry minimum. Decoders use the central registry
for ID, CRC metadata, and minimum/maximum bounds, copy at most the 255-byte payload into a
zeroed stack buffer, and never read beyond received data.

The following message schemas deliberately retain hand-written models and decoders because
they participate in existing domain or protocol workflows:

`AHRS2`, `ATTITUDE`, `AUTOPILOT_VERSION`, `BATTERY_STATUS`, `COMMAND_ACK`,
`EKF_STATUS_REPORT`, `FILE_TRANSFER_PROTOCOL`, `GLOBAL_POSITION_INT`, `GPS_RAW_INT`,
`HEARTBEAT`, `HOME_POSITION`, `LOCAL_POSITION_NED`, `MEMINFO`, `MEMORY_VECT`,
`MISSION_ACK`, `MISSION_COUNT`, `MISSION_CURRENT`, `MISSION_ITEM_INT`,
`MISSION_ITEM_REACHED`, `MISSION_REQUEST_INT`, `MISSION_REQUEST_LIST`,
`NAV_CONTROLLER_OUTPUT`, `PARAM_VALUE`, `POWER_STATUS`, `RAW_IMU`, `RC_CHANNELS`,
`SCALED_PRESSURE`, `SERVO_OUTPUT_RAW`, `STATUSTEXT`, `SYS_STATUS`, `TIMESYNC`, and
`VFR_HUD`.

Task 05 also corrected the retained `MemoryVectMessage` contract: its ID is
`MEMORY_VECT` (249), not `MEMINFO` (152), and its required `type` byte is now represented.
`MISSION_REQUEST_LIST` and `MEMORY_VECT` received matching hand-written decoders so every
documented override remains typed in both directions.

Regenerate both committed wire files with:

```powershell
dotnet run --project src/Tools/MissionPlanner.MavLink.Generator/MissionPlanner.MavLink.Generator.csproj -- wire . src/Core/MissionPlanner.MavLink/Generated/MavLinkWireMessages.g.cs src/Core/MissionPlanner.MavLink/Generated/MavLinkWireDecoders.g.cs
```

`MavLinkGeneratedWireModelTests` compare the generated sources byte-for-byte, validate
schema types, integer boundaries, IEEE special values, arrays and strings, exercise every
valid extension-truncation length, reject both underlength and overlength payloads, verify
all overrides, and cover the requested AHRS, radio, wind, vibration, flight-information,
extended-state, hardware-status, autopilot-version, and ESC telemetry families.

## Decoder catalog and startup validation

Task 06 removes the hand-maintained decoder array from `MavLinkConfigurator`.
`AddGeneratedMavLinkDecoders` now registers one singleton `IMavLinkMessageDecoderCatalog`
and the decoding facade. The generated decoder artifact deterministically creates all 287
generated decoders and all 32 declared custom decoders; normal startup performs no assembly
scanning. Regenerating after adding an ordinary dialect message automatically updates DI.

Each of the 319 typed schema slots is independently recorded with its message ID, CRC extra,
minimum and maximum payload length, and one ownership kind:

- `Generated` for schema-generated wire implementations;
- `HandWrittenOverride` for retained semantic wire contracts;
- `ProtocolWorkflow` for custom mission, parameter, command, MAVFTP, and time-sync decoders.

`MavLinkMessageDecoderCatalog` fails during first DI resolution when registrations contain
duplicate IDs, refer to an ID absent from the selected registry, declare a stale CRC or
payload-length window, omit an expected generated decoder, or add a custom decoder without
a generated declaration. The complete catalog consequently exposes exactly one effective
typed decoder per declared slot. Direct construction of `MavLinkMessageDecoders` remains
available for isolated tests and tools; it still falls back to raw messages outside the
explicit test decoder set.

Enabling this validation found two dormant hand-written metadata defects: `PARAM_VALUE`
advertised its message ID (22) as its CRC extra instead of the official value 220, and
`MEMINFO` left its CRC extra at the default zero instead of 208. Both now match the central
registry.

`MavLinkDecoderCatalogTests` validate application-provider resolution, generated coverage,
one-for-one overrides, deterministic duplicate and registry/schema failure messages,
MAVFTP and parameter decoder continuity, and isolated manual decoder construction.

## Core vehicle-state telemetry

Task 08 completes the normal flight-display state path for heartbeat/identity, attitude,
global and local position, HUD motion, primary and secondary GPS, landed/VTOL state, power,
radio/RC, servo output, home position, and status text. The existing cohesive handlers own
these families; no packet-specific handler classes were added.

Generated `ATTITUDE_QUATERNION`, `GPS2_RAW`, `EXTENDED_SYS_STATE`, `RADIO_STATUS`, and
ArduPilot `RADIO` records now feed focused observations. Deprecated ArduPilot `BATTERY2` is
an intentional generated compatibility exception and updates secondary-battery state;
`BATTERY_STATUS` uses its battery instance ID for the same purpose. The handwritten
`SYS_STATUS` contract now retains battery current, controller load, communication quality,
and present/enabled/healthy sensor bitmaps instead of discarding them.

Scaling and sentinel handling occurs once at the handler boundary: centimetres and
millimetres become metres, centi-units become domain units, `UINT*_MAX` and negative
unknowns become nullable values, and normalized primary UI properties remain source
compatible. Timestamped flight, motion, GPS, power, health, and radio slices expose stale
checks. Cohesive telemetry handlers compare the immutable state before publishing, so an
identical duplicate frame does not cause a redundant `VehicleStateUpdated` event.

`CoreVehicleStateTelemetryTests` cover handler conversion, direct session application,
secondary receiver/battery isolation, stale semantics, duplicate suppression, and normal
`VehicleMessagePump` dispatch. The exhaustive generated wire tests provide canonical decode
fixtures for every promoted family and explicitly assert the Task 08 message CLR types.

## Protocol workflows and peripheral access

Task 10 keeps request/response ownership out of general vehicle-state handlers. The
connection-wide `ICommandAckTracker` is shared by outbound command services and inbound
`COMMAND_ACK` dispatch, correlates by vehicle and command, and rejects unrelated or duplicate
responses. Transient command services no longer dispose the shared MAVLink connection.

Mission upload and download use one INT-based transfer state machine. Responses are filtered
by source vehicle and mission type; download buffers early or out-of-order items and ignores
duplicates. Deprecated `MISSION_ITEM` and `MISSION_REQUEST` remain generated compatibility
records for legacy peers. The pinned official dialect revision does not define
`MISSION_CHANGED`, so no synthetic record is exposed for that notification.

Parameter loading first tries ArduPilot's packed `@PARAM/param.pck` file over MAVFTP and
automatically falls back to the classic `PARAM_REQUEST_LIST`/`PARAM_VALUE` stream when the
file, transport, or decoder is unavailable. Extended parameters remain typed protocol
messages. Existing MAVFTP connection, dispatcher, cancellation, and reconnect ownership is
unchanged.

Log transfer, camera, gimbal, mount, ADS-B, generator/EFI, ESC, winch, landing target,
OpenDroneID, cellular/Wi-Fi, CAN, serial control, tunnel, and device-operation messages are
typed and available to protocol workflows and inspectors. They deliberately remain without
application services until a current product feature needs an owning state machine.

`ProtocolWorkflowCoverageTests` exercise successful correlation, timeout, cancellation,
wrong-vehicle rejection, duplicate/out-of-order mission handling, service lifetime, packed
parameter preference/fallback, and representative typed access across every Task 10 family.

## Navigation, estimator, and sensor telemetry

Task 09 adds coherent estimator, vibration, pressure, range, environment, and vehicle-time
state without widening the legacy flat HUD contract. `AHRS` retains gyro-drift and estimator
error diagnostics; `AHRS2`/`AHRS3` retain alternate pose output. `SCALED_PRESSURE` and its
second and third instances remain separate, while distance sensors are keyed by sensor ID.
Wind direction/speed is normalized to NED components, non-finite altitude fields become
nullable, centimetre range values become metres, and `SYSTEM_TIME` is range-checked before
conversion to `DateTimeOffset`.

The new `SensorTelemetryHandler` owns only low-rate state useful to product diagnostics:
vibration, pressure, distance, terrain, wind, altitude, and system time. Raw/scaled/high-rate
IMU, `HIGHRES_IMU`, obstacle arrays, optical flow, and odometry remain fully typed diagnostic
traffic available to inspectors and logs, but do not allocate aggregate snapshots or publish
global state events. `TIMESYNC` remains owned by its request/response workflow.

`NavigationEstimatorSensorTelemetryTests` cover scale and sentinel conversion, multi-instance
sensors, stale state, estimator/vibration application, high-rate non-promotion, and a
representative message-pump path.

## Domain promotion policy

Task 07 classifies all 325 selected-dialect messages without equating wire coverage with
vehicle-domain state. The policy and category rules are documented in
[MAVLINK_DOMAIN_PROMOTION.md](MAVLINK_DOMAIN_PROMOTION.md); the complete deterministic input
for subsequent domain work is [mavlink-promotion-catalog.json](mavlink-promotion-catalog.json).

Diagnostic/raw is the default. Vehicle-state telemetry is translated into immutable
observations and applied only by `VehicleSession`; meaningful transitions are domain events;
protocol request/response families remain owned by dedicated services; and outbound-only
messages stay behind command/setpoint APIs. Each entry names one owner, observation/model
when applicable, intended frequency, stale timeout, and UI consumers. Catalog tests enforce
the existing six cohesive dispatcher handlers and prevent accidental multiple ownership.
