# Mandatory Hardware Lifecycle Audit

The four added workflows were checked against the lifecycle used by the existing
Mandatory Hardware TabView content.

- Each ViewModel is transient and each XAML View derives from
  `TabViewLifecycleContent<TViewModel>`.
- Each ViewModel listens only to `IActiveVehicleContext`; no low-level MAVLink
  connection interface is injected by the new UI or services.
- Initial construction handles either an already-connected vehicle or an empty
  connection state.
- Active-vehicle changes cancel the previous linked operation before loading the
  current vehicle. Disconnect clears editable and diagnostic collections.
- `Dispose` removes active-vehicle subscriptions and cancels/disposes owned token
  sources, preventing duplicate subscriptions after repeated tab activation.
- Parameter apply commands now return their asynchronous task to the generated
  command instead of using `async void`, so failures and cancellation remain in
  the owned workflow operation.
- Services verify the requested vehicle is still the active online vehicle before
  reading or writing. All writes use `IVehicleParameterService` and the shared
  registry/metadata services.
- Registrations contain one transient entry per ViewModel and service; obsolete
  commented registrations and a duplicate Safety registration were removed.

