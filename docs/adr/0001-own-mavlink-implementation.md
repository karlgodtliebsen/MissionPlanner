# ADR 0001: Own the MAVLink implementation

- Status: Accepted
- Date: 2026-07-29

## Context

`MissionPlanner.MavLink` previously referenced `Asv.Mavlink` 4.2.0, but no
source file used `Asv.Mavlink`, `Asv.Store`, or any other `Asv.*` API. The
unused reference introduced a large transitive graph, including MessagePack
3.1.4 and its published security advisories.

Mission Planner already owns its MAVLink framing, parsing, CRC calculation,
message definitions, generated registry, commands, parameters, missions,
MAVFTP, and connection lifecycle behavior.

## Decision

The official MAVLink XML dialect definitions are the protocol source of truth.
`MissionPlanner.MavLink` is the owned implementation that generates and
implements the wire model used by the new solution.

No general-purpose MAVLink framework is referenced unless a future,
concrete requirement cannot reasonably be implemented in the owned layer.
Such a dependency requires a separate architecture decision and must not
duplicate the existing protocol stack.

## Consequences

- The unused `Asv.Mavlink` package and its transitive `Asv.*`, MessagePack,
  reactive, storage, serial, logging, and configuration graph are removed.
- MAVLink behavior remains covered by deterministic protocol and lifecycle
  tests, plus bounded opt-in SITL smoke tests.
- Updates to MAVLink dialects flow from official XML through the existing
  generation and conformance pipeline.
- Real-flight-controller serial smoke testing remains a manual release
  validation because CI must not perform hardware writes.
