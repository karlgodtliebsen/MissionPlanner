# Vehicle and Firmware Identity

## Objective

Add a reliable, connection-scoped vehicle and firmware identity model based on MAVLink `HEARTBEAT` and `AUTOPILOT_VERSION`. The identity will later be used for parameter metadata version selection, capability-driven protocol selection, cache keys, diagnostics, and UI display.

## Scope

Implement the identity pipeline only. Do not change parameter-transfer performance or MAVFTP behaviour in this task.

## Required MAVLink support

### `AUTOPILOT_VERSION` message

Add message ID `148` and CRC extra `178` to the MAVLink registry/CRC provider.

Decode these fields according to the MAVLink common dialect:

- `capabilities : ulong`
- `flight_sw_version : uint`
- `middleware_sw_version : uint`
- `os_sw_version : uint`
- `board_version : uint`
- `flight_custom_version : byte[8]`
- `middleware_custom_version : byte[8]`
- `os_custom_version : byte[8]`
- `vendor_id : ushort`
- `product_id : ushort`
- `uid : ulong`
- `uid2 : byte[18]` when present as a MAVLink 2 extension field

The decoder must tolerate MAVLink 2 trailing-zero truncation and zero-fill absent extension bytes.

### Requesting the message

After the first valid vehicle heartbeat, send `COMMAND_LONG` with:

- `command = MAV_CMD_REQUEST_MESSAGE` (`512`)
- `param1 = 148`
- target system/component from the registered vehicle

Request once per connection session. Allow a bounded retry if no response arrives. Cancellation or disconnect must terminate the wait without surfacing an unobserved exception.

## Domain model

Add strongly typed immutable records/enums. Suggested shape:

```csharp
public enum FirmwareFamily
{
    Unknown,
    ArduCopter,
    ArduPlane,
    Rover,
    ArduSub,
    AntennaTracker,
    APPeriph,
    Blimp
}

public enum FirmwareReleaseType : byte
{
    Development = 0,
    Alpha = 64,
    Beta = 128,
    ReleaseCandidate = 192,
    Official = 255
}

public sealed record FirmwareSemanticVersion(
    byte Major,
    byte Minor,
    byte Patch,
    FirmwareReleaseType ReleaseType);

public sealed record VehicleFirmwareIdentity(
    FirmwareFamily Family,
    byte MavType,
    byte Autopilot,
    FirmwareSemanticVersion? FlightVersion,
    string? FlightGitHash,
    ulong Capabilities,
    uint BoardVersion,
    ushort VendorId,
    ushort ProductId,
    ulong? HardwareUid,
    string? HardwareUid2);
```

Names may be adjusted to match existing conventions, but preserve the separation between:

- firmware family
- detailed MAV vehicle type
- semantic firmware version
- hardware/vendor identity
- protocol capabilities

## Firmware-family mapping

Derive the broad firmware family from the combination of `HEARTBEAT.autopilot` and `HEARTBEAT.type`.

Only apply ArduPilot-family mappings when autopilot is `MAV_AUTOPILOT_ARDUPILOTMEGA`.

Map at minimum:

- fixed wing -> ArduPlane
- quadrotor, coaxial, helicopter, hexarotor, octorotor, tricopter, dodecarotor, decarotor -> ArduCopter
- ground rover / surface boat -> Rover
- submarine -> ArduSub
- antenna tracker -> AntennaTracker
- blimp -> Blimp
- otherwise -> Unknown

Keep the original MAV type in the identity so UI can distinguish Quadrotor from Helicopter while both use ArduCopter firmware.

## Version decoding

Decode `flight_sw_version` as:

```text
bits 31..24 = major
bits 23..16 = minor
bits 15..8  = patch
bits 7..0   = release type
```

Do not parse the version from status text.

Convert `flight_custom_version` to a lowercase hexadecimal Git identifier. Treat an all-zero array as unknown.

## Vehicle session integration

Extend `VehicleSession`/`VehicleState` with an identity sub-state rather than adding many unrelated top-level properties.

Required behaviour:

1. Heartbeat creates the initial identity with family, MAV type, and autopilot.
2. `AUTOPILOT_VERSION` enriches the existing identity.
3. A reconnect creates a new session identity; stale version data from the previous connection must not leak into it.
4. Publish the normal vehicle-state-updated domain event after enrichment.
5. Preserve backwards-compatible convenience properties only where already required by UI code.

## Display naming

Add a pure formatter/service that produces a display value such as:

```text
ArduCopter 4.6.2
ArduPlane 4.7.0-dev
ArduCopter (version unknown)
```

Do not claim an exact flight-controller marketing name from vendor/product IDs unless a maintained mapping contains that pair.

A user-assigned vehicle name is a separate future concern and must not be inferred from `SYSID_THISMAV`.

## Capabilities

Expose capability checks through named methods or flags, for example:

```csharp
identity.Supports(MavProtocolCapability.Ftp)
identity.Supports(MavProtocolCapability.ParamFloat)
identity.Supports(MavProtocolCapability.ParamUnion)
```

Do not scatter raw bit masks through consumers.

## Tests

Add tests for:

1. Complete `AUTOPILOT_VERSION` decoding.
2. MAVLink 2 truncated extension fields.
3. Version packing for official, development, beta, and release-candidate builds.
4. All-zero custom version producing no Git hash.
5. ArduCopter mapping from quadrotor heartbeat.
6. ArduPlane mapping from fixed-wing heartbeat.
7. Non-ArduPilot autopilot resulting in `Unknown` family.
8. Session enrichment after `AUTOPILOT_VERSION`.
9. No identity leakage after disconnect/reconnect.
10. Request-message command is sent once per connection and is cancellation-safe.

## Acceptance criteria

- The connected SITL ArduCopter is displayed with its actual semantic firmware version.
- `VehicleState` exposes firmware family, detailed vehicle type, capabilities, board version, and stable hardware IDs when available.
- Reconnect produces a clean identity and re-requests `AUTOPILOT_VERSION`.
- No parsing relies on status text or parameter names.
- Existing telemetry and parameter tests continue to pass.
