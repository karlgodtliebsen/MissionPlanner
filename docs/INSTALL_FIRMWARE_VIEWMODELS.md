# Install Firmware ViewModels

`InstallFirmwareViewModel` coordinates refresh, download, APJ installation, DFU installation,
embedded bootloader updates, capability gating, cancellation, and progress dialogs. It does
not own the catalogue's filter collections or the individual panels' local selection state.

The panel ViewModels inherit `ViewModelBase` and are registered as singletons in
`ApplicationConfigurator.AddViewsModelsConfiguration`:

| ViewModel | Responsibility |
| --- | --- |
| `FirmwareCatalogViewModel` | Catalogue recommendations, release channel, filters, selected release, refresh requests. |
| `DetectedDeviceViewModel` | Serial-device choices, recommendation and selection status, install requests. |
| `CustomFirmwareViewModel` | Local APJ/PX4 picking, package validation and metadata, exact-board-match option. |
| `STM32BootloaderViewModel` | DFU device selection, local combined HEX picking and platform input, DFU operation requests. |
| `SelectedFirmwareViewModel` | Selected catalogue release details, download requests, URL copying. |
| `ValidatedPackageViewModel` | Prepared package details and validation state, install requests. |
| `DiagnosticsReportViewModel` | Last diagnostic report and clipboard command. |
| `FirmwareHelpViewModel` | Embedded help, support links, Device Manager command. |

Each extracted AXAML view uses `UserControlViewBase<TViewModel>` and its own compiled
`x:DataType`. The page explicitly binds child `DataContext` properties. Singleton registration
ensures that DI in the generic view base and the parent's constructor resolves the same
objects, including the device/validation/diagnostic panels reused in multiple tabs.

Children publish ordinary `Action<T>` events within this narrowly owned UI scope. The parent
subscribes in `ActivateAsync` and unsubscribes in `Deactivate` before cancelling its work.
Activation is guarded against duplicate subscriptions. Parent disposal never disposes the
singleton children. Local file operations are cancelled when their panel unloads, and a late
picker/parser result is checked for cancellation before it can update state.

`FirmwarePanelRequest` carries an operation, cancellation token, optional catalogue scope,
and a completion task. The parent's synchronous event handler assigns that task, and the
child command awaits it. This preserves command completion/error propagation without
`async void` event handlers. Selection events carry the selected item, package, path, or
channel; the parent synchronizes dependent panels and installation capabilities.

Validation: `FirmwarePanelViewModelTests` covers shared instances, repeated parent
activation/deactivation, catalogue filtering and retained selection, awaited operation
requests, and ignored late picker results. Desktop and Browser/WASM builds also validate
the compiled AXAML bindings.
