# Config 05 — Onboard OSD layout and item configuration

## Objective

Complete Onboard OSD configuration with screen/item discovery, parameter-backed placement and options, visual preview, and write/readback support.

## Dependencies

Config task 01.


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

Use OSD parameter metadata and presence to discover screens/items. Reference legacy OSD behavior but implement a platform-neutral layout model and MAUI canvas/graphics preview.

## Implementation requirements

1. Discover OSD screens and items from parameter-name patterns and metadata.
2. Model enabled state, row/column, options, units, warnings, and screen selection.
3. Render a character-grid preview with drag/drop or accessible numeric placement controls.
4. Validate bounds and detect overlaps; allow overlaps only with explicit warning if firmware supports dynamic items.
5. Add screen enable/options and font/upload hooks only when an underlying service exists.
6. Apply changes through the shared parameter session with confirmed readback.
7. Support firmware differences and custom items by metadata discovery rather than fixed lists.
8. Add reset/import/export layout.

## Tests

- Parameter pattern discovery tests.
- Grid bounds/overlap tests.
- Drag/keyboard movement view-model tests.
- Import/export and write/readback tests.

## Acceptance criteria

- OSD screens/items can be configured visually.
- Layout is derived from the connected firmware.
- Invalid coordinates cannot be applied.
- Existing Full Parameters functionality is not duplicated.

## Completion

Completed 2026-07-22. Onboard OSD now discovers screens, built-in/custom item stems,
placement fields, and additional options from live parameter names and metadata. A
platform-neutral character-grid model drives the MAUI graphics preview and accessible
numeric/directional placement controls. Bounds and overlap validation, explicit dynamic-
overlap acknowledgement, selected-screen confirmed apply, reset-to-live, atomic
family-tagged import/export, lifecycle handling, and DI registration are implemented and
covered by deterministic discovery, layout, movement, file, readback, and view-model tests.
