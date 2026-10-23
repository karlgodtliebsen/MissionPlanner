# Task 01 — Inventory and Coverage Baseline

## Objective

Create a deterministic report showing exactly what the new MissionPlanner supports versus the official MAVLink dialect and the read-only legacy generated definitions.

## Required analysis

Inspect:

- `src/Core/MissionPlanner.MavLink/Messages/MessageIds.cs`
- `src/Core/MissionPlanner.MavLink/Services/CommonMavLinkCrcExtraProvider.cs`
- `src/Core/MissionPlanner.MavLink/Messages/*Message.cs`
- `src/Core/MissionPlanner.MavLink/Decoding/*MessageDecoder.cs`
- `src/Core/MissionPlanner.Core/Vehicles/Handlers`
- `src/Core/MissionPlanner.Core/Vehicles/Observations`
- the generated MAVLink definitions available in the legacy tree, without modifying them
- official `common.xml` and `ardupilotmega.xml`

## Deliverables

Add a tool or test utility that produces a machine-readable coverage report containing, per message:

- dialect
- message ID
- MAVLink name
- CRC extra
- minimum payload length
- maximum payload length
- whether present in `MessageIds`
- whether known to the CRC provider
- whether a typed model exists
- whether a typed decoder exists
- whether a domain handler exists
- whether a domain observation exists

Suggested output:

`artifacts/mavlink-coverage.json`

Do not commit transient build output unless repository conventions explicitly allow it. The test should be capable of recreating the report.

## Classification

Classify each message as one of:

- `RegistryOnly`
- `TypedWireMessage`
- `DomainTelemetry`
- `ProtocolWorkflow`
- `OutboundOnly`
- `Deprecated`
- `UnsupportedByDesign`

## Tests

Add tests proving:

- no duplicate message IDs exist after dialect inheritance is resolved;
- no duplicate message names exist with conflicting IDs;
- all current hand-written message IDs match the official dialect;
- all current CRC extras match the official dialect;
- existing decoder IDs are represented in the official registry;
- the report is stable across repeated generation.

## Acceptance criteria

- A reviewer can see exact coverage counts.
- Existing incorrect constants are identified explicitly.
- The task changes no runtime behaviour.
- Nothing under `src-v.1.38` is modified.
