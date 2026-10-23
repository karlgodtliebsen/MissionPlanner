# Mandatory Hardware Architecture Analysis

This analysis records the implementation constraints used for the four missing
Mandatory Hardware workflows. The seven existing workflows remain the design
reference; the legacy WinForms implementation is used only to recover behavior.

## Existing Next Gen architecture

- `MandatoryHardwareView` owns one Ursa `TabView`. Each tab uses
  `SlimTabHeaderView`, a `TabItemViewModel`, and lifecycle content that resolves
  and disposes the tab ViewModel as the tab becomes active or inactive.
- `MandatoryHardwareViewModel` obtains ordered workflow evaluations from
  `ISetupWorkflowCatalog`. It projects those evaluations into the existing tab
  status model and listens for active-vehicle and parameter-registry changes.
- Workflow services live in `MissionPlanner.Core.Setup.MandatoryHardware` and
  expose interfaces from `Setup.Abstractions`. They use the shared active
  vehicle, parameter registry, metadata, and parameter-write services.
- Views do not access MAVLink or parameter stores. ViewModels orchestrate a
  workflow service, expose bindable state and commands, cancel work on disposal,
  and unsubscribe from connection/parameter events.
- Parameter-backed pages derive available controls from the connected vehicle's
  reported parameters. `PeripheralSettingFactory` supplies metadata-backed
  captions, descriptions, ranges, units, and enum options. Writes use the
  existing vehicle parameter service and are explicit; opening a page never
  changes vehicle configuration.
- Completion is stored and evaluated by the existing setup completion/catalog
  infrastructure. Applicability is distinct from completion and must remain
  capability-aware.
- Domain registrations belong in `DomainConfigurator`; transient tab ViewModels
  and Views belong in `ApplicationConfigurator`. Navigation continues to use the
  existing index-aligned TabView/catalog model.

## Legacy behavior to preserve

| Workflow | Legacy source | Relevant behavior |
|---|---|---|
| Failsafe | `ConfigFailSafe` | Present only reported vehicle-specific parameters. Copter battery/throttle/GCS settings and Plane throttle/action settings differ. Preserve warnings and validate thresholds before explicit writes. |
| Initial Tune Parameters | `ConfigInitialParams` | Copter and QuadPlane workflow. Load only supported tuning inputs, preserve legacy calculations/conversions, show proposed values, and require explicit apply. |
| HW ID | `ConfigHWIDs` | Diagnostic list built from reported `*_ID` and `*_DEVID` parameters, excluding index and FrSky fields. Decode known device identifiers without inventing unavailable data. |
| ADSB | `ConfigADSB` | Present the reported `ADSB_*` and `AVD_*` settings only. Use metadata mappings and validate address/configuration values before explicit writes. |

## Implementation decisions

- Canonical stems are `FailSafe`, `InitTuneParameters`, `HwId`, and `Adsb`.
- No new connection abstraction, navigation mechanism, parameter cache, or
  completion store will be introduced.
- All four pages use XAML plus code-behind and a transient disposable ViewModel,
  matching the existing Mandatory Hardware lifecycle.
- Missing parameters produce an unsupported or partial presentation, not
  defaults. Disconnect clears editable/diagnostic state; reconnect reloads the
  newly active vehicle.
- HW ID is informational: when applicable, successful retrieval is meaningful
  completion and does not pretend that hardware identifiers are configurable.
- Tests cover service behavior and lifecycle state using the existing test
  doubles and project layout. Integration is completed only after the workflow
  catalog, ordered tabs, DI, and capability-aware counts agree.
