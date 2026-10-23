# Task 07 — Domain Promotion Policy and Observation Catalog

## Objective

Define which wire messages deserve domain observations and handlers. Do not create a domain type for every protocol packet.

## Required documentation

Add a design document defining these categories:

### Vehicle-state telemetry
Messages that update durable/current vehicle state, such as attitude, position, power, GPS, radio, landed state, estimator health, and firmware identity.

### Domain event
Messages representing meaningful transitions, such as arming changes, connection changes, mission item reached, or command outcomes.

### Protocol workflow
Messages consumed by dedicated services, such as mission, parameter, command, FTP, log transfer, camera, and gimbal protocols.

### Diagnostic/raw telemetry
Messages useful in an inspector or log but not part of the primary vehicle aggregate.

### Outbound-only
Requests and setpoint messages not expected to update vehicle state.

## Catalog

Create a machine-readable promotion catalog keyed by message ID/name with:

- category
- owning handler/service
- observation/model type if promoted
- intended update frequency
- stale timeout if relevant
- UI consumers

## Domain rules

- Observations are immutable facts derived from one or more wire messages.
- `VehicleSession` applies observations and owns state transitions.
- Handlers translate protocol messages into observations; they do not contain UI logic.
- High-rate messages should not emit expensive global domain events on every packet unless required.
- Protocol workflow responses should remain within their service/dispatcher and not be forced into `VehicleState`.

## Tests

- every domain handler references a catalog entry;
- no message is assigned to multiple single-owner handlers accidentally;
- every promoted observation has application tests;
- registry-only messages remain accessible through raw-message inspection.

## Acceptance criteria

- The team has an explicit rule preventing “one handler per MAVLink message” architecture.
