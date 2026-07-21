# Task: Add Vehicle Display Name and Identity Presentation

## Objective

Add a consistent, human-readable display name for connected vehicles, similar to the label shown by ArduPilot ground stations:

```text
SysID 1:Copter
```

The display name must be derived from the vehicle’s MAVLink identity and must not be confused with a user-assigned vehicle name.

The implementation must preserve the underlying structured identity:

* MAVLink system ID
* MAVLink component ID
* MAVLink vehicle type
* MAVLink autopilot type
* Derived firmware family
* Firmware version, when available

## Important distinction

MAVLink does not guarantee that the flight controller provides a general-purpose user-defined vehicle name.

Therefore, the first implementation should introduce a derived display label.

Example from ArduPilot:

```text
SysID 1:Copter
```

This should be generated from:

```text
System ID = 1
Autopilot = MAV_AUTOPILOT_ARDUPILOTMEGA
Vehicle type = MAV_TYPE_QUADROTOR
Derived firmware family = Copter
```


Example from original MissionPlanner:

```text
UDP14550-1-QUADROTOR
```



Do not store the entire formatted string as the authoritative identity.

The structured values remain authoritative. The display string is presentation data.

---

## Proposed domain model

Review the existing vehicle identity and state classes before adding new types.

The current architecture likely already contains some or all of:

```csharp
VehicleId
VehicleIdentityState
VehicleState
VehicleSession
HeartbeatMessage
```

Prefer extending the existing identity model rather than creating duplicate concepts.

A suitable identity model may look like:

```csharp
public sealed record VehicleIdentityState
{
    public byte VehicleType { get; init; }

    public byte Autopilot { get; init; }

    public byte MavLinkVersion { get; init; }

    public VehicleFirmwareFamily FirmwareFamily { get; init; }

    public FirmwareVersion? FirmwareVersion { get; init; }

    public string DisplayName { get; init; } = string.Empty;
}
```

If the existing model already includes system ID through `VehicleId`, do not duplicate it in `VehicleIdentityState`.

The formatted name may instead be exposed as a computed property:

```csharp
public string DisplayName =>
    VehicleDisplayNameFormatter.Format(
        VehicleId,
        Identity.FirmwareFamily,
        Identity.VehicleType);
```

Choose the design that best matches the current domain model.

---

## Firmware-family enum

Use or introduce a firmware-family enum:

```csharp
public enum VehicleFirmwareFamily
{
    Unknown,
    Copter,
    Plane,
    Rover,
    Sub,
    Tracker
}
```

Do not use display strings such as `"Copter"` as the internal representation.

---

## Firmware-family mapping

Derive the firmware family from both:

```text
HEARTBEAT.autopilot
HEARTBEAT.type
```

Only apply ArduPilot-specific family mapping when:

```text
autopilot == MAV_AUTOPILOT_ARDUPILOTMEGA
```

Suggested mapping:

```csharp
public static VehicleFirmwareFamily Map(
    MavAutopilot autopilot,
    MavType vehicleType)
{
    if (autopilot != MavAutopilot.ArduPilotMega)
    {
        return VehicleFirmwareFamily.Unknown;
    }

    return vehicleType switch
    {
        MavType.FixedWing
            => VehicleFirmwareFamily.Plane,

        MavType.GroundRover
            or MavType.SurfaceBoat
            => VehicleFirmwareFamily.Rover,

        MavType.Submarine
            => VehicleFirmwareFamily.Sub,

        MavType.AntennaTracker
            => VehicleFirmwareFamily.Tracker,

        MavType.Quadrotor
            or MavType.Coaxial
            or MavType.Helicopter
            or MavType.Hexarotor
            or MavType.Octorotor
            or MavType.Tricopter
            or MavType.Dodecarotor
            or MavType.Decarotor
            => VehicleFirmwareFamily.Copter,

        _ => VehicleFirmwareFamily.Unknown
    };
}
```

Use the project’s existing MAVLink enum names. Do not introduce duplicate MAVLink enums if equivalent definitions already exist.

Review uncommon ArduPilot vehicle types and extend the mapping where appropriate.

---

## Display-name formatter

Create a dedicated formatter or value object.

Suggested interface:

```csharp
public interface IVehicleDisplayNameFormatter
{
    string Format(
        VehicleId vehicleId,
        VehicleFirmwareFamily firmwareFamily,
        MavType vehicleType);
}
```

A static domain helper is also acceptable if dependency injection adds no value.

Suggested behaviour:

```csharp
public static string Format(
    VehicleId vehicleId,
    VehicleFirmwareFamily family,
    MavType vehicleType)
{
    var typeName = family switch
    {
        VehicleFirmwareFamily.Copter => "Copter",
        VehicleFirmwareFamily.Plane => "Plane",
        VehicleFirmwareFamily.Rover => "Rover",
        VehicleFirmwareFamily.Sub => "Sub",
        VehicleFirmwareFamily.Tracker => "Tracker",
        _ => FormatMavType(vehicleType)
    };

    return $"SysID {vehicleId.SystemId}:{typeName}";
}
```

Examples:

```text
SysID 1:Copter
SysID 2:Plane
SysID 3:Rover
SysID 4:Sub
SysID 5:Tracker
```

For an unknown firmware family, use the MAVLink vehicle type when it can be represented meaningfully:

```text
SysID 7:Quadrotor
SysID 8:Fixed Wing
SysID 9:Unknown
```

Do not include `ComponentId` in the normal user-facing vehicle name unless the UI is specifically showing MAVLink component-level information.

A vehicle is generally identified by its system ID, while components are subordinate endpoints within that system.

---

## Vehicle-session integration

When the first valid heartbeat registers a vehicle:

1. Read the system and component IDs from the MAVLink envelope.
2. Read `type`, `autopilot`, and MAVLink version from the heartbeat.
3. Derive `VehicleFirmwareFamily`.
4. Update the vehicle identity state.
5. Produce or expose the derived display name.
6. Publish the normal vehicle registration/state-update events.

Example conceptual flow:

```csharp
var firmwareFamily =
    firmwareFamilyMapper.Map(
        heartbeat.Autopilot,
        heartbeat.VehicleType);

session.ApplyHeartbeat(
    heartbeat,
    firmwareFamily);
```

The display name must update correctly if a later heartbeat provides corrected identity information.

Avoid emitting unnecessary state updates when the derived identity has not changed.

---

## Interaction with firmware version

The vehicle display name and firmware display name are separate concepts.

Vehicle label:

```text
SysID 1:Copter
```

Firmware label:

```text
ArduCopter 4.6.2
```

Possible combined UI presentation:

```text
SysID 1:Copter
ArduCopter 4.6.2
```

Do not initially combine both into one persisted domain string such as:

```text
SysID 1:Copter ArduCopter 4.6.2
```

The UI can compose them when required.

The display name must work before `AUTOPILOT_VERSION` has been received.

---

## UI integration

Replace raw vehicle identifiers in vehicle-selection UI elements with the new display name where appropriate.

Likely locations include:

* Vehicle menu
* Active-vehicle selector
* Connection status area
* Vehicle summary header
* Configuration views
* MAVFTP view header
* Parameter editor header

The UI should still expose the raw identity where diagnostically useful.

Example:

```text
Primary:
SysID 1:Copter

Secondary or tooltip:
System 1, Component 1
ArduCopter 4.6.2
```

Do not replace all occurrences of `VehicleId.ToString()` globally without reviewing their purpose.

Logs, protocol diagnostics, dictionary keys, and exception messages may still need the exact technical form:

```text
1:1
```

The friendly display name is intended for user-facing presentation.

---

## Multiple vehicles

The implementation must support multiple simultaneously discovered MAVLink systems.

Example:

```text
SysID 1:Copter
SysID 2:Copter
SysID 3:Plane
```

Do not assume system ID 1.

The display label must remain stable across:

* UI refreshes
* reconnects
* state updates
* telemetry updates
* parameter loading
* MAVFTP activity

---

## Reconnect behaviour

When a connection is lost:

* The last known display identity may remain visible with an offline indication.
* Do not replace the label with `Unknown` solely because the active transport has disconnected.
* Clear the identity only when the corresponding vehicle session or registry entry is intentionally removed.

When the same vehicle reconnects:

* Recreate or update the session normally.
* Derive the same display name from the new heartbeat.
* Avoid retaining references to an old disposed session.
* Ensure UI subscriptions move to the current session or registry state.

---

## Optional future user-defined name

Do not implement a user-defined vehicle name unless there is already a reliable source for one.

However, design the display model so that a user-defined alias can be introduced later.

Possible future precedence:

```text
1. User-defined local alias
2. Vehicle-provided name, if a reliable protocol source exists
3. Derived label such as "SysID 1:Copter"
```

A possible future model:

```csharp
public sealed record VehicleDisplayIdentity(
    string DerivedName,
    string? VehicleProvidedName,
    string? UserAlias)
{
    public string EffectiveName =>
        UserAlias
        ?? VehicleProvidedName
        ?? DerivedName;
}
```

Do not add this abstraction now unless it naturally fits the current codebase.

---

## Logging

Add focused debug logging when vehicle identity is initially resolved or changes.

Example:

```text
Resolved vehicle identity:
VehicleId=1:1,
Autopilot=ArduPilotMega,
MavType=Quadrotor,
FirmwareFamily=Copter,
DisplayName="SysID 1:Copter"
```

Avoid logging the display name repeatedly on every heartbeat.

---

## Tests

Add unit tests covering at least the following.

### Firmware-family mapping

```text
ArduPilotMega + Quadrotor => Copter
ArduPilotMega + Hexarotor => Copter
ArduPilotMega + FixedWing => Plane
ArduPilotMega + GroundRover => Rover
ArduPilotMega + Submarine => Sub
ArduPilotMega + AntennaTracker => Tracker
Non-ArduPilot autopilot + Quadrotor => Unknown
Unknown MAV_TYPE => Unknown
```

### Display formatting

```text
VehicleId(1,1) + Copter => "SysID 1:Copter"
VehicleId(2,1) + Plane => "SysID 2:Plane"
VehicleId(3,1) + Rover => "SysID 3:Rover"
VehicleId(4,1) + Unknown + Quadrotor => suitable fallback
```

### Vehicle session

Verify that applying the first heartbeat:

* Sets the firmware family
* Produces the expected display identity
* Preserves the component ID separately
* Does not require `AUTOPILOT_VERSION`

Verify that repeated identical heartbeats do not cause unnecessary identity changes.

Verify that a changed vehicle type updates the derived display label.

### UI/view-model tests

Where practical, verify that:

* The vehicle selector displays `SysID 1:Copter`
* Disconnection changes connection status but does not corrupt the display name
* Reconnection restores the same label
* Multiple vehicles receive distinct labels

---

## Acceptance criteria

The task is complete when:

1. A connected ArduCopter vehicle appears as:

   ```text
   SysID 1:Copter
   ```

2. The system ID is not hard-coded.

3. The label is derived from heartbeat identity.

4. The implementation distinguishes:

   ```text
   VehicleId
   Firmware family
   Firmware version
   User-facing display name
   ```

5. Non-ArduPilot vehicles do not incorrectly receive an ArduPilot family.

6. The label survives normal disconnect/reconnect UI transitions.

7. Existing technical logging and protocol correlation continue to use the structured `VehicleId`.

8. Unit tests cover mapping, formatting, session updates, and reconnect-related presentation behaviour.

9. No files under `src-v.1.38` are modified.

10. The solution builds and all affected tests pass.
