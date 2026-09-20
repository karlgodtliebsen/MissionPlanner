# DI registration audit

Audit of the starting tree for DI-Cleanup, 2026-09-20. No behavior was changed for task 01.
Locations below are repository-relative. Repeated ordinary registrations are grouped by purpose;
every factory expression in ApplicationConfigurator is listed.

| Location / current registration | Classification / purpose | Lifetime and dependencies | Risk | Recommended pattern / task |
| --- | --- | --- | --- | --- |
| ApplicationConfigurator: Options.Create(applicationOptions), CancellationTokenSource | Normal registration; application settings/shutdown | Singleton values, no provider retained | Externally created disposable source has no container ownership | Retain explicit host shutdown ownership; 04 |
| ApplicationConfigurator: Dispatcher.UIThread factory | Intentional composition-root usage; framework dispatcher | Singleton framework object, no capture | None; host/test override useful | Keep TryAdd; 04 |
| ApplicationConfigurator: MapHttpOptionsProvider.GetOptions() | Factory registration; settings projection | Singleton provider and options; lambda resolves once | Not a deferred provider capture | Keep with comment; 04 |
| ApplicationConfigurator: Func<int, Control> | Runtime keyed-selection candidate / service-locator smell | Transient delegate retains resolving provider; root normally used | Magic integer, root-retained disposable ViewModels, no owner scope | LogsSection + ILogsViewFactory + keyed controls; 02/04 |
| ApplicationConfigurator: AdvancedToolRegistration Nmea | Metadata registry / lifetime hazard | Singleton closure retains root; transient NmeaPage, singleton VM with transient session | Metadata owns activation/root capture | Metadata only, keyed Page and typed factory; 03/04 |
| ApplicationConfigurator: AdvancedToolRegistration Mirror | Metadata registry / lifetime hazard | Singleton closure, transient MirrorPage, singleton VM and transient sessions | Same | 03/04 |
| ApplicationConfigurator: AdvancedToolRegistration Signing | Metadata registry / lifetime hazard | Singleton closure, transient SigningPage, singleton VM/service | Same; intentional persistent signing state | 03/04 |
| ApplicationConfigurator: AdvancedToolRegistration Proximity | Metadata registry / lifetime hazard | Singleton closure, transient page, singleton VM + transient aggregator/session | Same | 03/04 |
| ApplicationConfigurator: AdvancedToolRegistration Inspector | Metadata registry / lifetime hazard | Singleton closure, transient page, singleton VM + transient aggregator/session | Same | 03/04 |
| ApplicationConfigurator: AdvancedToolRegistration Warnings | Metadata registry / lifetime hazard | Singleton closure, transient page, singleton VM + transient engine | Same | 03/04 |
| ApplicationConfigurator: NavigationPageFactory/AvaloniaNavigationService | Explicit factory registration | Singleton factory/provider and navigation stack; transient pages | Clearing stack drops pages but root retains IDisposable VMs; unload only deactivates | Explicit navigation ownership/scopes, release removed pages; 04 |
| ApplicationConfigurator: MainWindow/MainViewModel/MainShell/TopBar/StatusBar/LiveTelemetryInspector | Normal application-shell registrations | Singleton UI/session state, application lifetime | Do not turn shell objects into transient per-navigation objects | Keep shell identity; validate resolution; 05 |
| ApplicationConfigurator: firmware panel VMs/coordinator | Normal registration | Singleton retained workflow state; page wrappers transient | Workflow state intentionally survives panel changes | Keep lifetime, validate; 04/05 |
| ApplicationConfigurator: map/simulation/file-picker/notification/platform defaults | Normal registrations | Mostly transient adapters, singleton settings/runtime repositories | TryAdd needed for Browser/desktop/test replacements; same implementation under two transient interfaces is NOT an alias | Keep override seams, document; 04 |
| ApplicationConfigurator: ordinary pages, tab VMs and tools | Normal registrations | Transient views/VMs, singleton registries/options | Many IDisposable VMs resolved by framework from root | Scope runtime activation; 04 |
| ApplicationConfigurator: duplicate FlightPlannerMissionMapViewModel, FlightDataMissionMapViewModel, FlightDataViewModel, FlightPlannerViewModel, DialogDemoViewModel, PreferencesViewModel, InstallFirmwareViewModel, MandatoryHardwareViewModel | Normal registration, redundant | TryAdd masks duplicates | Future Add conversion would change enumeration | Remove repeats; ordinary Add for application-owned UI entries; 04 |
| ApplicationConfigurator: UseApplicationServices/UseApplicationAsync | Intentional composition-root usage | Resolves startup services and registers IDomainFactory runtime-context constructors | DomainFactory is a legitimate explicit factory; no metadata capture | Keep; 04 |
| DomainConfigurator: TelemetryRecordingService -> ITelemetryRecordingService; IReplaySessionManager -> IReplayClock; VehicleComponentRegistry -> IVehicleComponentRegistry | Alias to same instance | Singleton concrete/primary interface | Identity is intentional | Keep and test Same; 04/05 |
| DomainConfigurator: PayloadProtocolService -> camera/gimbal interfaces | Factory registration, NOT same-instance alias | Transient on each resolution | Looks like singleton alias but creates independent adapters | Direct implementation registrations, document stateless/transient intent; 04 |
| EventHubConfigurator + DomainConfigurator | Normal registrations / distinct singleton buses | IEventHub, IDomainEventHub, INavigationEventHub, IVehicleTelemetryEventHub own distinct singleton registrations | Aliasing telemetry to application bus would break isolation | Keep AddSingleton<IVehicleTelemetryEventHub, EventHub>(); test NotSame; 04/05 |
| LoggingLibraryConfigurator | Intentional composition factories | Singleton logger, buffer, level switch and file state; optional platform path | No deferred provider closure; logger owns sinks | Keep host/test override seams |
| MapsConfigurator, BrowserAppConfigurator, desktop configurators | Intentional composition factories | Singleton caches/repositories/options, HTTP factories, platform adapters | Filesystem/browser implementations must not be interchanged by shared UI setup | Keep platform overrides; validate Browser subset; 05 |
| MavLink decoder registrations | Factory / metadata registry | Singleton decoder catalog and definitions | IEnumerable order must not substitute for explicit message keys | Keep registry validation; no UI activation |
| Library factories and UseDomain/UseMavLink/UseSimulation | Explicit activation/composition boundaries | Factory resolution with local runtime context | Provider belongs here, not in ordinary services | Keep, validate consumers |
| ViewModelBase logger-only/default constructors | Service-locator smell | Resolves dispatcher and domain bus via Application.Current | Hidden dependencies and test global state | Constructor injection; 04 |
| XAML view bases, App, ServiceHelper | Framework activation boundaries | Parameterless XAML construction resolves VM/logger | Root provider use bypasses navigation lifetime | Scope-aware framework resolution; preserve design-mode bypass; 04 |
| MissionMapView(IServiceProvider) | Service-locator smell inside otherwise valid framework boundary | Only two dependencies actually needed | Over-broad constructor | Typed constructor dependencies; 04 |

## Enumeration and remaining delegates

AdvancedFeatureCatalog provides stable card order; AdvancedToolRegistry uses unique IDs, not
DI enumeration order. Metadata enumeration must never instantiate a page. Other IEnumerable
registrations include protocol handlers/catalogs and platform strategies; they are discovery or
dispatch collections, not requests to select the last registered service. Multiple advanced
metadata entries are consumed only as IEnumerable, never as one AdvancedToolRegistration.

Event callbacks, predicates, progress callbacks, parameter-edit callbacks, lazy asset-byte
caches and file-content stream delegates carry runtime behavior/data, not service providers.
They are not candidates for mechanical replacement.

## Validation plan

Build the unchanged solution after this audit. Tasks 02/03 add typed selection and tests;
task 04 addresses activation ownership, redundant registrations and implicit VM dependencies.
Task 05 validates real composition with scopes/build checks, Desktop/Browser builds, keyed
mapping/unknown-key/transient behavior, metadata uniqueness and bus/alias identities.
