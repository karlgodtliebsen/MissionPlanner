# Task 03 — Create STM32 DFU Catalogue Workflow

**Repository:** `karlgodtliebsen/MissionPlanner`  
**Branch:** `feature/add-DFU-bootload`

## Goal

Create a clean `STM32 DFU -> Catalogue` workflow that reuses the existing firmware catalogue selection but presents the **correct DFU artifact semantics**.

Normal catalogue behavior validates/installs `.apj`/`.px4`.

The STM32 DFU catalogue workflow selects an ArduPilot platform/release, then resolves and flashes the sibling combined image:

```text
*_with_bl.hex
```

Do not display an APJ package as though it were the artifact programmed by STM32CubeProgrammer.

## Existing components to reuse

Inspect and reuse:

- `FirmwareCatalogViewModel`
- `FirmwareCatalogueSelectorView`
- `SelectedFirmwareView`
- shared selected-firmware state
- `IDfuInstallationService`
- existing DFU artifact resolver/inspector
- existing global firmware operation lease/policy
- existing STM32CubeProgrammer provider

Do not duplicate catalogue loading/filtering.

## Suggested presentation

Create a DFU-specific composition, e.g.:

- `STM32DfuCatalogueView.axaml`
- `STM32DfuCatalogueView.axaml.cs`

Conceptually:

```text
FirmwareCatalogueSelectorView    (shared)
SelectedFirmwareView             (shared)
DFU artifact/readiness panel     (DFU-specific)
DFU device/tool readiness        (DFU-specific)
Install action
```

If `FirmwareCatalogueView` is too APJ-specific because it hardcodes `ValidatedPackageView`, refactor at the view-composition level. Do not fork the catalogue ViewModel.

## DFU artifact/readiness presentation

Present enough information to make clear what will be flashed:

- selected ArduPilot platform;
- selected release/channel/version;
- selected manifest source;
- resolved combined HEX filename/URL when known;
- HEX inspection/validation state;
- selected DFU endpoint;
- STM32CubeProgrammer readiness;
- target-safety warning/status.

If artifact resolution is currently lazy inside the install service, add only a thin application-layer preview/preparation path that reuses the same resolver and inspector. Do not implement a second resolution algorithm.

## Required semantic flow

```text
Selected catalogue manifest/APJ entry
        ↓
platform + release identity
        ↓
resolve sibling *_with_bl.hex
        ↓
inspect Intel HEX
        ↓
flash via STM32 ROM DFU
```

The UI must never label the selected `.apj` as the DFU package being programmed.

## Catalogue filtering

Keep the existing catalogue search/filter infrastructure.

If no exact ArduPilot target has been safely resolved from the source controller, allow manual search/selection.

Do not auto-select a target solely from STM32 MCU ID.

## Regression to investigate

When selected platform is `BETAFPV-F405`, verify that the manifest/source used by the DFU resolver belongs to the same platform and vehicle variant.

Do not accept a mismatch such as selected normal `BETAFPV-F405` while the source path points at `BETAFPV-F405-heli/arducopter-heli.apj`.

If the cause is catalogue projection/filtering, fix it generically and add a regression test. Do not hardcode BETAFPV as a special case.

## Acceptance criteria

1. STM32 DFU Catalogue uses the existing catalogue data/filtering.
2. Selecting a row does not create a second copy of catalogue state.
3. UI distinguishes the selected catalogue entry from resolved `*_with_bl.hex`.
4. APJ validated-package metadata is not presented as the DFU flash artifact.
5. Installation invokes existing `IDfuInstallationService`.
6. Existing confirmation/safety checks remain.
7. DFU endpoint/tool readiness gates install.
8. Anonymous DFU does not automatically infer an exact FC platform.
9. Normal `Firmware -> Catalogue` APJ behavior is unchanged.
10. Platform/source variant consistency has regression coverage.
11. Relevant tests pass and affected projects compile.
