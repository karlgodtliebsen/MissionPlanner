# Config 07 — CubeLAN 8-port switch and extensible vendor devices

## Objective

Complete CubeLAN 8 Port Switch configuration through a dedicated device service and establish an extensible pattern for vendor-specific network/CAN peripherals.

## Dependencies

Config task 01 and verified CubeLAN protocol information.


## Repository constraints

- Work only under `src/`, `docs/`, `scripts/`, and test-data folders belonging to the new solution.
- Treat `src-v.1.38/` as read-only reference material. Never modify, format, move, or include legacy files in commits.
- Preserve the existing layered architecture: wire protocol in `MissionPlanner.MavLink`, transport in `MissionPlanner.Transport`, application/domain behavior in `MissionPlanner.Core`, and MAUI presentation in `MissionPlanner.App`.
- Do not call MAVLink transports directly from views or code-behind. Use application/domain services injected into view models.
- Keep code-behind limited to view lifecycle and unavoidable platform/UI integration.
- Use CommunityToolkit.Mvvm patterns already present in the solution.
- Reuse `VehicleId`, `VehicleSession`, `VehicleRegistry`, command ACK tracking, parameter services, domain event hub, generated MAVLink messages, and decoder catalog rather than creating parallel abstractions.
- All vehicle-changing operations must be connection-aware, cancellation-aware, target the active `VehicleId`, and expose command acknowledgement or an explicit failure state.
- Add structured logging at workflow boundaries; do not log high-frequency telemetry on every update.
- Add unit tests for domain/application behavior and view-model tests. Add smoke/integration tests only where they are deterministic.
- Update DI registrations in the existing configurators and add DI validation tests.
- The solution must build with nullable warnings treated consistently with the repository.

## Scope

First inspect what protocol and discovery support actually exists in the current source and legacy reference. Do not fabricate network endpoints or commands. If hardware protocol information is unavailable, implement discovery/model/UI boundaries with a documented unsupported state and test doubles.

## Implementation requirements

1. Define a vendor-device abstraction with identity, transport, capabilities, configuration snapshot, validation, apply, and reboot/reconnect state.
2. Implement CubeLAN discovery only using documented mechanisms present in repository or official protocol artifacts.
3. Model eight ports, labels, enablement, mode/VLAN/PoE or other settings only when verified by protocol definitions.
4. Add read-before-edit, dirty tracking, apply/readback, and rollback/export.
5. Securely handle authentication if required; redact secrets.
6. Avoid binding the generic abstraction to CubeLAN-specific fields.
7. Add clear Unsupported/Not discovered/Authentication required states.
8. Document required real-hardware verification separately from automated tests.

## Tests

- Fake-device discovery/read/apply/readback tests.
- Validation and rollback tests.
- Authentication redaction tests.
- UI state tests for unavailable/unsupported devices.

## Acceptance criteria

- The page is functional when supported protocol data exists and honest when it does not.
- No undocumented commands are sent.
- Eight-port state can be read and confirmed after apply.
- Vendor-specific code remains isolated.

## Completion

Completed 2026-07-22. CubeLAN now uses the repository-documented MAVLink `DEVICE_OP` I²C
mechanism at bus 0/address `0x50`, with generated-message MAVLink 2 wire encoding and
request, vehicle, component, and endpoint reply correlation. The page reads before editing,
shows exactly eight ports, exposes only the verified COS, priority, EEE, VLAN tagging, and
8-by-8 membership bits, tracks dirty state, validates, confirms each changed byte and the
full readback, attempts confirmed rollback, and exports a non-secret verified subset.

A generic typed vendor-device contract keeps identity, transport, capabilities,
configuration, validation, apply/rollback, authentication redaction, and reboot/reconnect
state independent of CubeLAN. Unsupported, not-discovered, authentication-required, and
disconnected states are explicit. PoE, enablement, modes, VLAN IDs, editable labels,
authentication, and reboot/reconnect commands remain intentionally absent because the
repository contains no verified definitions. Deterministic fake-device and UI tests cover
the implemented workflow; [CUBELAN.md](../../CUBELAN.md) records the separate physical
hardware verification checklist.
