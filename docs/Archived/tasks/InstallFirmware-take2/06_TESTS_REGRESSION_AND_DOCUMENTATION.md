# Task 06 — Tests, Regression, Cleanup and Documentation

**Repository:** `karlgodtliebsen/MissionPlanner`  
**Branch:** `feature/add-DFU-bootload`

## Goal

Finish the restructure with automated tests, regression coverage, cleanup and documentation.

Do not introduce a second firmware/DFU implementation.

## Required test coverage

### Navigation/composition

Cover that:

- top-level contexts are Firmware / STM32 DFU / Help;
- normal Firmware Catalogue remains available;
- normal Custom Firmware remains available;
- STM32 DFU has Device / Catalogue / Custom HEX contexts.

Prefer ViewModel/state tests over brittle pixel/UI automation unless the repository already has a UI-testing convention.

### Betaflight -> DFU transition

Cover:

1. source cannot be re-verified -> no reboot;
2. port busy -> actionable error;
3. verified Betaflight + UID -> confirmation requested;
4. user cancels -> no reboot;
5. successful correlated handoff -> resulting DFU endpoint selected;
6. successful handoff -> navigate to STM32 DFU / Catalogue;
7. failed/ambiguous handoff -> stay in Device context;
8. no automatic flash after handoff.

### DFU catalogue semantics

Cover:

- normal APJ validation remains normal Firmware behavior;
- DFU catalogue install resolves the combined HEX using the existing resolver;
- selected platform/source vehicle variant remain consistent;
- selected APJ is not reported as the programmed DFU artifact;
- tool/device readiness gates installation.

### Custom HEX

Cover:

- reject non-HEX;
- reject non-combined HEX when current policy requires `*_with_bl.hex`;
- accept valid local combined HEX;
- Clear works;
- exact platform is required where applicable;
- install remains gated by DFU/tool/safety readiness.

### Anonymous DFU

Cover:

- `0483:DF11` present without proven handoff can still be used as a DFU endpoint;
- exact FC target is not inferred solely from USB/MCU identity.

## Build/regression matrix

Run the repository's existing relevant tests/builds for at least:

- `MissionPlanner.Firmware`;
- `MissionPlanner.App`;
- corresponding unit/integration test projects;
- Windows/Desktop target used by this feature;
- Browser/WASM compile target.

## Cleanup

- remove dead/duplicate `LandingView` or old `STM32BootloaderView` composition if no longer referenced;
- remove obsolete commented-out STM32 button blocks;
- reuse selector/subviews rather than copying catalogue XAML;
- rename UI-only `STM32Bootloader...` types to `STM32Dfu...` only where clarity improves enough to justify the churn;
- preserve existing service/domain names when already semantically correct.

## Documentation

Update:

- `docs/INSTALL_FIRMWARE_VIEWMODELS.md`
- `docs/FIRMWARE.md`
- `docs/BETAFLIGHT_DFU_AND_ARDUPILOT_CONVERSION.md`

Document the final workflows:

```text
Normal ArduPilot update:
ArduPilot/application or serial bootloader
-> Firmware -> Catalogue/Custom -> APJ

Betaflight conversion:
Betaflight COM/MSP
-> STM32 DFU -> Device / Enter DFU
-> correlated STM32 ROM DFU
-> Catalogue -> *_with_bl.hex

Already in STM32 ROM DFU:
STM32 DFU -> Device confirms endpoint
-> Catalogue/Custom HEX -> *_with_bl.hex
```

Document explicitly:

- STM32 ROM DFU is not normally a COM port;
- `0483:DF11` alone does not prove exact board identity;
- successful Betaflight-to-DFU correlation preserves source evidence but does not itself choose/flash firmware;
- Betaflight firmware installation remains deferred/out of scope.

## Final acceptance criteria

1. No duplicate firmware catalogue, serial scanner, DFU scanner or DFU flashing service exists.
2. Normal APJ firmware installation remains functional.
3. STM32 DFU combined-HEX installation remains functional.
4. Betaflight -> DFU handoff remains physically correlated and safe.
5. UI hierarchy clearly separates installation mechanism from firmware source.
6. APJ and DFU HEX semantics are not mixed.
7. Automated tests pass.
8. Desktop build passes.
9. Browser/WASM build still compiles.
10. Documentation matches implementation.
