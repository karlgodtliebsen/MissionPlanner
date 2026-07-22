# Task 03 — Generate Protocol Enums, Flags, and Command Constants

## Objective

Generate the protocol-level enums and command IDs required by all generated message fields while preserving domain isolation.

## Required generated definitions

Include all enums referenced by `common.xml` and `ardupilotmega.xml`, including at minimum:

- `MAV_TYPE`
- `MAV_AUTOPILOT`
- `MAV_COMPONENT`
- `MAV_STATE`
- `MAV_MODE_FLAG`
- `MAV_RESULT`
- `MAV_PARAM_TYPE`
- `MAV_SEVERITY`
- `MAV_PROTOCOL_CAPABILITY`
- `FIRMWARE_VERSION_TYPE`
- `MAV_LANDED_STATE`
- `MAV_VTOL_STATE`
- `GPS_FIX_TYPE`
- `MAV_CMD`
- mission result/type/frame enums
- camera, gimbal, ADS-B, generator, EFI, ESC, fence, rally, mount, and ArduPilot-specific enums referenced by generated models

## Naming

Convert XML names into project-consistent C# names without losing numeric identity. Preserve an escape hatch for unknown future numeric values by storing enum-typed fields without validation that rejects undefined numbers.

Use `[Flags]` only where the dialect defines bitmask semantics.

Do not duplicate existing domain enums such as `FirmwareFamily` or `VehicleMode`. Add explicit mapping functions between protocol enums and domain concepts.

## Commands

Refactor `MavLinkCommandIds.cs` so command values are generated from `MAV_CMD`, or make the hand-written facade delegate to the generated enum.

Existing code using command constants must continue compiling or be migrated cleanly.

## Tests

- representative enum values match official XML;
- flags combine correctly;
- unknown numeric enum values survive decode/encode round trips;
- existing firmware identity tests use generated protocol definitions;
- existing arm/disarm, request-message, mission, and parameter commands retain their numeric values.

## Acceptance criteria

- No large manually copied enum block from the legacy generated file.
- Generated definitions are deterministic.
- Domain projects do not become coupled to generated wire structs unnecessarily.
