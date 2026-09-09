# Install Firmware ViewModels

## Navigation and artifact semantics

```text
Install Firmware
├── Firmware
│   ├── Catalogue        — APJ/PX4 through the ArduPilot serial bootloader
│   └── Custom Firmware  — local APJ/PX4 and its board metadata
├── STM32 DFU
│   ├── Device / Enter DFU
│   ├── Catalogue        — selected release -> sibling *_with_bl.hex
│   └── Custom HEX       — local *_with_bl.hex and explicit platform
└── Help & Support
```

The top-level contexts use horizontal tabs; nested workflow tabs are on the left.
The persistent context card and Refresh devices action remain above them. Device / Enter DFU
and Help remain accessible without a programmable device. Serial and DFU installation retain
separate capability gates. Each scrolling content area is constrained by the page's star row.

`InstallFirmwareViewModel` owns operation coordination, confirmations, cancellation and progress.
`SelectedSectionIndex` and `SelectedDfuTabIndex` correspond to the UI-only `FirmwareSection` and
`Stm32DfuSection` enums. Selecting Custom HEX explicitly selects the local source. A retained local
file never overrides an official catalogue request from the Catalogue context.

## State ownership

All panel ViewModels inherit `ViewModelBase` and use singleton DI registration.

| ViewModel | Responsibility |
| --- | --- |
| `FirmwareLandingViewModel` | Device/Enter DFU identity summaries, shared selectors, actionable reboot errors and page-owned reboot requests. |
| `FirmwareCatalogViewModel` | One shared catalogue: service loading, filters, recommendations, selected manifest entry and optional reviewed DFU target. |
| `DetectedDeviceViewModel` | Existing platform serial discovery, identity enrichment and selected serial descriptor. |
| `CustomFirmwareViewModel` | Normal local APJ/PX4 selection, parsing and board-ID override policy. |
| `STM32BootloaderViewModel` | Existing DFU discovery, CubeProgrammer readiness, local combined HEX selection, inspected DFU artifact and correlated source evidence. |
| `SelectedFirmwareViewModel` | Shared selected manifest identity, source copying and normal APJ download request. |
| `ValidatedPackageViewModel` | Normal prepared APJ metadata and install request; never the DFU programming artifact. |
| `DiagnosticsReportViewModel` | Diagnostic report and clipboard. |
| `FirmwareHelpViewModel` | Embedded help and curated support actions. |

`STM32DfuDeviceView` replaces LandingView. `STM32DfuCatalogueView` reuses
`FirmwareCatalogueSelectorView` and `SelectedFirmwareView`, with the APJ download action hidden.
`STM32DfuCustomHexView` uses the existing local HEX picker. Both DFU source views use
`STM32DfuArtifactView` for inspected filename, source, platform, checksum, size, readiness and
explicit preparation/install actions. The old overloaded STM32BootloaderView is removed.

The selected catalogue APJ is release/platform identity. DFU preparation delegates to the same
`IDfuArtifactResolver` used by installation, under the global `PrepareDfuArtifact` operation lease.
It resolves and inspects combined Intel HEX, without programming. Source changes invalidate its
preview; late results for a replaced source are rejected. Installation still resolves/revalidates
its source and runs provider and target safety checks before programming. An APJ validated package
is never presented or supplied as the DFU artifact.

The resolver rejects disagreement between the explicit platform, manifest platform/board and
source platform directory, including `Board` versus `Board-heli`. It never substitutes another
target or a bootloader-only artifact when a sibling is unavailable.

## Lifecycle and events

Children send ordinary `Action<T>` events in this narrow parent/child UI scope. The parent
subscribes in ActivateAsync and unsubscribes in DeactivateAsync. `FirmwarePanelRequest` lets the
parent assign a completion Task which the child command awaits; no async-void event handlers.

Page activation starts existing serial and DFU discovery without eagerly downloading a catalogue.
The page owns discovery until it closes, so tab changes do not stop the device models. Catalogue
loading follows panel activation, using the existing coalescing/cancellable FirmwarePanelLoader.
File picker operations observe panel lifetime cancellation and reject late results.

Normal `HasPreparedFirmware` is derived from PreparedFirmware. The validated panel owns its
binding and is enabled after preparation. Rebuilding recommendations for the same manifest entry
preserves preparation; selecting another entry or clearing selection invalidates it.

## Device entry and safety

Reboot to DFU freshly probes only the selected serial controller. A successful exact BTFL identity
and MCU UID are required before explicit propeller-removal confirmation. Confirmation closes
before the reboot progress dialog opens. The existing handoff service rechecks identity and armed
state, sends the supported reboot, and accepts only a newly appearing endpoint at the source's
physical USB location. Port-busy outcomes survive discovery/cache projection and appear visibly
in Device / Enter DFU, with instructions to disconnect Betaflight Configurator and retry.

A successful handoff must still rediscover the exact endpoint generation. It retains the source
receipt, selects that endpoint, then navigates to STM32 DFU / Catalogue. It never starts flashing.
Failure or ambiguity remains in Device / Enter DFU. Selecting a different DFU endpoint does not
inherit the source's identity. Existing exact reviewed compatibility mappings can select a unique
matching release; no mapping or ambiguous releases require manual selection. STM32 USB/MCU
identity never creates an exact target mapping.

A pre-existing 0483:DF11 endpoint requires no COM device. It is anonymous unless a matching
handoff was observed: STM32 ROM DFU is normally a USB endpoint, not a COM port, and does not prove
the exact FC PCB. Manual BOOT/DFU reconnect or BOOT + RESET guidance remains available.

Installation requires Windows, no active telemetry connection, a selected ready DFU driver,
validated CubeProgrammer availability, the selected source and exact local platform where needed.
The existing installer repeats tool/device/HEX/target checks and final confirmation. Power-critical
handling, deferred cancellation and verification remain in the existing services. Browser builds
share presentation and catalogue code but cannot perform native serial/DFU operations.

## Verification and task commits

The InstallFirmware-take2 tasks were implemented incrementally with separate commits. Tests cover
navigation composition; normal APJ download/validation; custom file and local-path policy; source
isolation and HEX preparation; tool gating; busy/unidentified ports; confirmation cancellation;
correlated handoff navigation and no automatic flashing; and anonymous DFU identity limits.
Full-suite and Desktop/Browser build results are recorded with task 06. Automated tests use fakes;
this restructure does not constitute physical flash or Pavo 20 compatibility acceptance.

Final verification (2026-09-09): full suite passed 1000 .NET + 7 JavaScript tests with 29 existing skips. After two additional filename-variant cases, targeted Firmware tests passed 254 (1 skip), UI tests passed 83, and Desktop/Browser builds passed. See [execution results](tasks/InstallFirmware-take2/EXECUTION_RESULTS.md).
