# Install Firmware ViewModels

`InstallFirmwareViewModel` coordinates download, APJ installation, DFU installation,
embedded bootloader updates, capability gating, cancellation, and progress dialogs. It does
not own the catalogue's filter collections or the individual panels' local selection state.

The panel ViewModels inherit `ViewModelBase` and are registered as singletons in
`ApplicationConfigurator.AddViewsModelsConfiguration`:

| ViewModel | Responsibility |
| --- | --- |
| `FirmwareLandingViewModel` | Read-only connection, serial and DFU summaries and next-step guidance. Observes existing discovery models only while active; never starts a scan. |
| `FirmwareCatalogViewModel` | Catalogue service calls, activation/refresh/cancellation and its progress dialog; recommendations, release channel, filters and selected release. |
| `DetectedDeviceViewModel` | Serial discovery through the injected platform catalogue, USB/board matching, retained device selection and install requests. |
| `CustomFirmwareViewModel` | Local APJ/PX4 picking, package validation and metadata, exact-board-match option. |
| `STM32BootloaderViewModel` | DFU device/tool discovery and readiness status, local combined HEX picking, platform input and DFU operation requests. |
| `SelectedFirmwareViewModel` | Selected catalogue release details, download requests, URL copying. |
| `ValidatedPackageViewModel` | Prepared package details and validation state, install requests. |
| `DiagnosticsReportViewModel` | Last diagnostic report and clipboard command. |
| `FirmwareHelpViewModel` | Embedded help, support links, Device Manager command. |

Each extracted AXAML view uses `UserControlViewBase<TViewModel>` and its own compiled
`x:DataType`. The page explicitly binds child `DataContext` properties. Singleton registration
ensures that DI in the generic view base and the parent's constructor resolves the same
objects, including the device/validation/diagnostic panels reused in multiple tabs.

Children publish ordinary `Action<T>` events within this narrowly owned UI scope. The parent
subscribes in `ActivateAsync` and unsubscribes in `DeactivateAsync` before cancelling its work.
Activation is guarded against duplicate subscriptions. Parent disposal never disposes the
singleton children. Local file operations are cancelled when their panel unloads, and a late
picker/parser result is checked for cancellation before it can update state.

`FirmwarePanelRequest` carries an installation/download operation, cancellation token,
and a completion task. The parent's synchronous event handler assigns that task, and the
child command awaits it. This preserves command completion/error propagation without
`async void` event handlers. Selection events carry the selected item, package, path, or
channel; the parent synchronizes dependent panels and installation capabilities.

Validation: `FirmwarePanelViewModelTests` covers shared instances, repeated parent
activation/deactivation, catalogue filtering and retained selection, awaited operation
requests, and ignored late picker results. Desktop and Browser/WASM builds also validate
the compiled AXAML bindings.

## Panel-owned loading

Opening the page starts serial and DFU discovery through the child ViewModels, without
fetching a manifest. The page retains discovery ownership until it closes, so unloading a
workflow tab does not stop discovery. Page deactivation releases ownership and cancels and
joins both children. Catalogue loading still follows its own panel activation.
The internal, UI-context `FirmwarePanelLoader` coalesces duplicate requests, owns cancellation
tokens, and waits for a previous activation to finish before starting another. Results are
checked for cancellation before updating observable state. A channel change cancels the
old catalogue request before loading the latest channel; failures release the progress dialog
and allow retry. Refresh no longer masquerades as an installation operation in the parent.

The catalogue observes the shared device panel's `Action<IReadOnlyList<SerialDeviceDescriptor>>`
event only while active. New manifest data updates device-match evidence without another
serial scan. The catalogue does not refresh DFU devices, and the DFU panel does not download
the manifest. A detected DFU device enables only the STM32 workflow; otherwise a discovered
serial port enables catalogue and custom firmware. No devices, a connected vehicle, an
unsupported platform, or an installation in progress disables all three workflow tabs.
Information and Help remain accessible. After attaching or changing hardware, the page's
Refresh devices command scans both device types, including when every workflow tab is disabled.
Serial discovery provides candidates; board compatibility remains an installation validation.

Panel unload preserves selections and cached catalogue choices. The page no longer forwards
TabControl selection events to a global reset routine. The parent still invalidates validation
at the page boundary and coordinates mutually exclusive custom/catalogue choices, installation
capabilities, confirmation/progress sequencing, and safe flashing. It supplies an installation
interlock to the read panels and subscribes to their `Action<bool>` refresh-state events to
prevent competing install commands. Panels also observe connection changes independently.

The validated-package panel owns its enabled binding through `HasPreparedFirmware`, derived
from `PreparedFirmware`. Successful download/preparation enables the panel. Rebuilding
recommendations for the same manifest entry preserves validation, including transient grid
selection resets during that rebuild. Selecting another entry or clearing the selection
invalidates the prepared package. Installation retains its separate capability checks.

Catalogue loading is platform-neutral. Browser can open the catalogue panel, with inline
status because the existing progress service requires a desktop Window. Direct flashing
remains subject to platform capabilities. Serial and DFU discovery continue to use existing
injected platform services; no native implementation moved into the shared App.

`FirmwarePanelLoadingTests` verifies independent activation, absence of eager parent I/O,
progress cleanup, ten cancellation/unload cycles, ignored late results, retained selections,
channel replacement, failure/retry, and the installation interlock. All tests use fake services;
they do not open COM ports or flash hardware.

Verification on 2026-09-08: Desktop, Browser and `src/MissionPlanner.slnx` builds
passed using `dotnet build <project-or-solution> --no-restore -p:UsedAvaloniaProducts= -v quiet`.
`src/Tests/Run-AllTests.ps1` passed 938 .NET tests (including 62 UI tests) and 7
JavaScript tests. The 29 existing skips were unchanged. Results are in
`TestResults/all-tests/20260908-002401-802`.
