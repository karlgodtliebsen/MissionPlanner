# Mission planning implementation

Added on top of `MissionPlanner-20260714-v3`.

## Included

- Mission aggregate and typed mission items: waypoint, takeoff, land, RTL and change-speed.
- Qualified mission altitude references: home, MSL and terrain.
- Sequence maintenance, editing operations and validation.
- Domain-to-MAVLink mission mapper.
- MAVLink message IDs and CRC extras for mission transfer.
- Incoming message records and decoders for MISSION_COUNT, MISSION_REQUEST_INT,
  MISSION_ITEM_INT, MISSION_ACK and MISSION_ITEM_REACHED.
- MAVLink v2 mission encoder for count, item, request-list, request-int, ack and clear-all.
- Mission upload/download/clear service with retries, timeouts and progress.
- Dependency-injection registrations.

## Important

The container used to prepare this archive does not contain the .NET SDK, so `dotnet build`
could not be executed here. Structural checks were performed and the MAVLink/Core dependency
boundary was explicitly verified to avoid a circular project reference.

Recommended first local command:

```powershell
dotnet build .\MissionPlanner.slnx
```

The first UI integration should use `Mission`, `MissionValidator`, and
`IMissionTransferService`; map pins should remain projections of the mission model.
