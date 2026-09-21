# Install Firmware ViewModels

## Navigation

The page has exactly three top-level tabs: **Information**, **Firmware**, and
**Help & Support**. Information is an informational Landing view. All operational
commands belong to Firmware; there are no nested firmware/DFU tabs. Online selection
uses the catalogue overlay and local selection uses the file picker. The catalogue
has inline refresh progress and a bounded virtualized grid.

## Workflow and installation plan

`FirmwareWorkflowContext` keeps physical presence, active telemetry transport,
observed runtime, boot environment, target evidence, artifact validity and target
compatibility separate. `FirmwareWorkflowResolver` and
`FirmwareInstallationPlanResolver` are deterministic core functions. The parent
rebuilds `CurrentPlan` when device, artifact, ownership or operation state changes.
`CurrentPlan.CanExecute` gates Install/Reinstall and dispatches to the existing AP
or DFU installer. The installers repeat their own safety checks before erase.

| Physical state | Preparation | Boot action | Installation |
| --- | --- | --- | --- |
| No controller, with or without network telemetry | Online and local | Manual DFU guidance | Blocked: no target |
| Betaflight serial, proven by MSP | Combined HEX | Explicit MSP-to-DFU handoff | Blocked until physical DFU and target review |
| Unknown serial | Online and local | Probe or manual BOOT/RESET | Blocked pending transport/target evidence |
| ArduPilot application, proven by heartbeat | APJ | Explicit AP bootloader entry | Blocked until bootloader identity |
| STM32 ROM DFU | Combined `*_with_bl.hex` | Already in DFU | Tool, artifact, target safety and typed platform review required |
| ArduPilot serial bootloader | APJ | Already in bootloader | Protocol board ID and strict compatibility required |

USB VID/PID and product text are hints. Only protocol board identity can produce
exact catalogue matching; shared USB identifiers never select a board automatically.
Runtime probing uses bounded MSP, isolated MAVLink and bootloader conversations.
A normal serial session blocks the COM port it owns, including while connecting.
TCP/UDP telemetry does not globally block firmware preparation or a separate USB
controller. Firmware operations also retain the shared exclusive operation lease.

## State ownership

| ViewModel | Responsibility |
| --- | --- |
| `InstallFirmwareViewModel` | Current plan, common artifact card, boot actions, source changes, progress, cancellation and installer dispatch |
| `FirmwareLandingViewModel` | Informational serial/DFU summaries; no boot or flash commands |
| `FirmwareCatalogueViewModel` | Catalogue loading, filters, hint-based recommendations and explicit manifest selection |
| `DetectedDeviceViewModel` | Serial snapshots, expiring protocol evidence and physical selection |
| `CustomFirmwareViewModel` | Local APJ import through the shared preparation service |
| `STM32BootloaderViewModel` | DFU discovery, tool readiness, combined HEX input and correlated source evidence |
| `SelectedFirmwareViewModel` | Internal selected manifest and download request state |
| `ValidatedPackageViewModel` | Internal prepared APJ consumed by the installer |
| `DiagnosticsReportViewModel` | Copyable terminal diagnostics |
| `FirmwareHelpViewModel` | Embedded help and support actions |

The selected/validated child models remain internal workflow inputs. The page uses
one `FirmwareArtifactSummary` presentation for both sources and both formats. It
shows provenance, hash/cache identity, target/version, size and validation separately
from compatibility. Official content includes URL/channel/Git identity; local APJ
includes original filename/path and import time. HEX retains inspected address ranges.

Local APJ import is bounded, structurally parsed, hashed and atomically cached before
it is marked valid. Validity never implies target compatibility. Both local and online
APJ use strict board matching; the local-only mismatch checkbox is removed. The
lower-level package parser retains its existing format support, while normal page
selection accepts APJ for serial and combined HEX for DFU.

DFU preparation uses the existing resolver/inspector under `PrepareDfuArtifact` and
can run without a connected controller. The chosen manifest identifies the exact
release/platform whose sibling combined HEX is resolved. A generic HEX filename is
rejected by the normal picker. Source changes invalidate the prepared preview; late
results cannot replace a newer selection. Typed `FLASH <platform>` review is bound
to the selected artifact and physical endpoint, and is invalidated on either change.
The final service confirmation still precedes destructive programming.

Clear Firmware clears artifact selection and validation while preserving physical
discovery, runtime evidence and device selection. Selecting serial or DFU selects
one physical installation target without emptying either discovery list.

## Lifecycle, progress and cancellation

Children use retained, narrowly scoped events. The parent subscribes on activation
and unsubscribes on deactivation. Page activation starts serial and DFU discovery;
the catalogue downloads when its overlay is shown. Changing top-level tabs does not
start a second workflow. File selection and asynchronous preparation reject stale
results and observe lifecycle cancellation.

Progress is owned by the page. `FirmwareDialogCoordinator` temporarily removes it
for operator confirmations and restores it afterwards. The progress area wraps text
and preserves Cancel access; terminal diagnostics close progress first. Manual
BOOT/RESET guidance is a cancellable, bounded wait with a distinct discovery-timeout
message. Operator cancellation is presented as Cancelled, never Completed or Failed.
AP erase/program/verify/reboot and providers without safe cancellation support retain
their existing deferred-cancellation behavior and tell the operator to keep power on.

## Verification

`FirmwareWorkflowTests` and `FirmwareInstallationPlanTests` cover the A–G capability
and format matrix. `FirmwarePlanViewModelTests`, `DfuWorkflowTests`,
`FirmwarePanelLoadingTests` and migration contracts cover page wiring and lifecycle.
`BetaflightConversionScenarioTests` covers reviewed handoff, mocked programming and
fresh runtime rediscovery, including verification failure. Existing firmware service,
protocol, cache, ownership and cancellation suites remain in place. Physical F4/H7
flash and visual desktop acceptance remain separate hardware checks.

## Connected normal upgrade

CanHandoffConnectedTarget allows the plan to install a compatible catalogue APJ while its
disarmed ArduPilot serial controller is connected. Probing retains its separate ownership
guard. Guidance identifies ArduPilot Bootloader and automatic reboot/upload/reconnect/verify;
DFU remains an explicit recovery action. The install request carries ExpectedRelease and
the result carries InstalledIdentity only after the running release is verified.
The page reports verified identity on success; a checksum-only upload cannot produce that result.