# Flight Data 09 — Integration, lifecycle, documentation and completion audit

Status: **Completed.**

## Objective

Verify the eight tabs as one coherent Flight Data subsystem, remove stale placeholder infrastructure and complete documentation.

Dependencies: tasks 01–08.

Apply all constraints from `00-README.md`.

## Integration audit

1. Confirm no placeholder-only content remains in:

   ```text
   PreflightTabView
   GaugesTabView
   TransponderTabView
   StatusTabView
   ServoRelayTabView
   AuxFunctionTabView
   ScriptsTabView
   PayloadControlTabView
   ```

2. Review `FlightDataTabViewModelBase`; its current constructor `key` is unused. Retain only useful shared behavior, prefer composition, or remove the base if it adds no value.
3. Verify every tab with the transient `TabViewLifecycleContent<T>` lifecycle.
4. Verify `Dispose()` cancels work, releases leases/timers and unsubscribes events without mutating UI-bound collections.
5. Verify active-vehicle switching, disconnect/reconnect and repeated tab/page navigation.
6. Verify every outbound tab is prohibited during replay.
7. Verify command tabs use operation gates and distinguish ACK from observed-state confirmation.
8. Verify UI publication rates are bounded.
9. Review DI lifetimes and extend `FlightDataInfrastructureTests` for all added services/ViewModels.

## Cross-tab presentation consistency

Standardize offline, unsupported, stale, busy, success/error, confirmation wording, units, timestamps, copy/export behavior and vehicle/component selectors. Reuse existing presentation abstractions instead of tab-specific duplicates.

## Test matrix

Document and execute appropriate coverage for:

```text
Windows
Android
Mac Catalyst
SITL
real serial FC
single vehicle and active-vehicle switch
disconnect/reconnect
tab switch and page navigation
replay mode
light/dark theme
keyboard/touch interaction
```

Automated tests must remain deterministic and bounded.

## Documentation completion

1. Complete `docs/FLIGHT_DATA.md` with architecture, lifecycle, shared telemetry catalog, all eight tabs, replay safety and troubleshooting.
2. Ensure `docs/README.md` links it.
3. Update all eight statuses and limitations in `docs/FEATURES.md`.
4. Update `docs/MAVLINK_DOMAIN_PROMOTION.md` and `docs/mavlink-promotion-catalog.json`.
5. Reconcile changes in `docs/MAVLINK.md`, `docs/PARAMETERS.md`, `docs/PLANNER_SETTINGS.md`, `docs/MAVFTP.md`, and `docs/ARCHITECTURE_DECISION_RECORDS.md`.
6. Mark old `docs/tasks/flight-data/` tasks completed/superseded without deleting useful history.
7. Update `docs/tasks/README.md`.
8. Record known limitations explicitly rather than leaving placeholder text.

## Acceptance criteria

- All eight tabs are functional and documented.
- No tab decodes or sends MAVLink from the View layer.
- No UI-bound collection is mutated from `Dispose()`.
- Replay and operation safety are enforced consistently.
- DI and deterministic tests pass.
- `docs/FEATURES.md` accurately reflects implementation and limitations.
