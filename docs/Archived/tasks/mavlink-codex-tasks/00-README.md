# MAVLink Full-Dialect Coverage — Codex Work Package

## Decision

This is intentionally split into sequential tasks rather than delivered as one implementation patch.

The current solution has a clean, explicit architecture:

- frame parsing and CRC validation in `MissionPlanner.MavLink`
- typed wire messages and decoders in `MissionPlanner.MavLink`
- message dispatch and domain observations in `MissionPlanner.Core`
- UI projections above the domain

The legacy Mission Planner generated dialect contains hundreds of message definitions. Treating every wire message as a domain observation and creating a domain handler for every message would be an architectural error. Full coverage must mean:

1. every message in the selected dialect is known to the frame parser;
2. every message can be CRC-validated;
3. every message can be represented losslessly as a raw message;
4. selected messages have generated typed wire models and decoders;
5. only messages with real domain meaning create observations and handlers;
6. generation is deterministic and protected by tests.

## Source constraints

- Modify only `src/`, `docs/`, `scripts/`, and generated test fixtures required by these tasks.
- Never modify anything under `src-v.1.38`.
- The legacy tree is read-only reference material.
- Prefer the official MAVLink XML dialect files as the authoritative source.
- Use `common.xml` and `ardupilotmega.xml`; include inherited dialects transitively.
- Record the MAVLink source revision used for generation.

## Execution order

Complete tasks in numeric order. Each task must build and pass tests before starting the next task.

1. Inventory and coverage baseline
2. Introduce the generated message-definition registry
3. Generate protocol enums and command constants
4. Make unknown typed messages losslessly available as raw messages
5. Generate typed wire models and decoders
6. Registration, DI, and decoder-catalog validation
7. Define the domain-promotion policy
8. Promote core vehicle-state telemetry
9. Promote navigation, estimator, sensor, and environment telemetry
10. Cover control, mission, parameter, camera, gimbal, and peripheral protocols
11. Build conformance fixtures and end-to-end tests
12. Add reproducible generation, CI drift detection, and documentation

## Global acceptance criteria

- No valid `common` or `ardupilotmega` frame is discarded merely because no hand-written decoder exists.
- CRC extra, minimum payload length, maximum payload length, message name, and dialect origin are available for every generated message.
- MAVLink 2 truncated extension fields are handled correctly.
- Unknown future message IDs remain diagnosable and do not crash the connection.
- Generated files are clearly marked and are not manually edited.
- Domain handlers exist only for messages that update a meaningful aggregate, observation, event, command workflow, or protocol service.
- Existing MAVFTP, mission, parameter, vehicle identity, and reconnect tests remain green.
- Nothing under `src-v.1.38` is changed.
