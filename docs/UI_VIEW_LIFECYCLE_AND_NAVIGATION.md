# UI View Lifecycle and Shell Navigation

This guide defines the preferred MissionPlanner patterns for tabbed content and top-level
MAUI Shell navigation. These patterns make ViewModel ownership explicit and ensure that
subscriptions, cancellation sources, and other resources are released when a view is no
longer active.

## Choosing the navigation pattern

- Use `LifecycleTabView` for several child views that share one screen and are selected by
  tab headers. Only the selected tab should own a live ViewModel.
- Use Shell navigation for application workspaces and pages that belong in the flyout or
  Shell hierarchy. Each destination is a `ShellContent` with its own route.
- Do not nest a second navigation mechanism merely to change content. Choose the pattern
  that represents the user's navigation level.

## LifecycleTabView

The control is defined in
`src/UI/UraniumUI/UraniumUI.Material.Controls.Extensions/TabViews`. A working standalone
example is in `UraniumUI.Material.Extensions.Samples/ControlsSamples`, while the Flight
Data screen is the production reference.

When selection changes, `LifecycleTabView` disables and deactivates the old tab content,
then activates and enables the new content. `TabViewLifecycleContent<TViewModel>` implements
that contract by resolving a new ViewModel from DI on activation and clearing the binding,
disposing the ViewModel, and dropping its reference on deactivation.

### View and XAML pattern

Declare each child as a `TabViewLifecycleContent<TViewModel>`:

```csharp
using UraniumUI.Material.TabViews;

public partial class ExampleTabView : TabViewLifecycleContent<ExampleTabViewModel>
{
    public ExampleTabView()
    {
        InitializeComponent();
    }
}
```

Host it directly inside a `TabItem`:

```xml
<tabViews:LifecycleTabView TabHeaderItemColumnWidth="*">
    <material:TabItem Title="Example">
        <tabs:ExampleTabView />
    </material:TabItem>
    <material:TabItem Title="Status">
        <tabs:StatusTabView />
    </material:TabItem>
</tabViews:LifecycleTabView>
```

Keep the content view parameterless so XAML can construct it. Do not resolve or construct
its ViewModel in the view constructor; the lifecycle base class owns that operation.

### ViewModel ownership and disposal

The ViewModel must be `IDisposable` and registered as transient:

```csharp
services.TryAddTransient<ExampleTabViewModel>();
```

Use `Dispose` to release everything acquired by that activation, including EventHub
subscription handles, .NET event handlers, timers, cancellation token sources, and other
disposable services owned by the ViewModel. Disposal should be idempotent. Do not assume a
tab ViewModel survives a tab switch, and do not register it as a singleton.

```csharp
public sealed class ExampleTabViewModel : IDisposable
{
    private readonly IDisposable subscription;
    private readonly CancellationTokenSource lifetime = new();
    private bool disposed;

    public ExampleTabViewModel(IDomainEventHub events)
    {
        subscription = events.SubscribeDomainEventAsync<VehicleStateUpdated>(OnStateUpdatedAsync);
    }

    private Task OnStateUpdatedAsync(
        VehicleStateUpdated update,
        CancellationToken cancellationToken) => Task.CompletedTask;

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        lifetime.Cancel();
        lifetime.Dispose();
        subscription.Dispose();
    }
}
```

If a view itself attaches handlers to its ViewModel, override both lifecycle methods as a
pair: call `base.Activate()` before attaching, detach before `base.Deactivate()`, and never
retain the old ViewModel. `MessagesTabView` is the reference implementation.

```csharp
public override void Activate()
{
    base.Activate();
    ViewModel?.PropertyChanged += OnViewModelPropertyChanged;
}

public override void Deactivate()
{
    ViewModel?.PropertyChanged -= OnViewModelPropertyChanged;
    base.Deactivate();
}
```

Activation and deactivation are synchronous. Start asynchronous work from the ViewModel in
a fire-and-observe manner consistent with project conventions, tie it to an activation
cancellation token, and handle errors; do not make `async void` lifecycle overrides.

## Shell-based navigation

`AppShell.xaml` is the source of truth for top-level navigation. The Config workspace in
`Views/ConfigTuning/Tabs` is the reference implementation: one `FlyoutItem` contains a
`ShellContent` entry for every Config page.

### Declare a destination

Add the page namespace to `AppShell.xaml`, then declare the page with a stable, unique
route and a deferred `DataTemplate`:

```xml
<FlyoutItem Title="Config" Icon="Resources/Images/x_light_tuningconfig_icon_x.png">
    <ShellContent
        Title="Example"
        Route="ConfigExample"
        ContentTemplate="{DataTemplate tabs:ExampleTabView}" />
</FlyoutItem>
```

Use the `Config` prefix for Config route names and keep titles user-facing. Use
`ContentTemplate`; do not construct a page instance in `AppShell` code-behind. Place a page
under the `FlyoutItem` that owns its workspace rather than registering it as an unrelated
global route.

### Page and ViewModel pattern

Shell-owned pages should inherit `ContentPageView<TViewModel>` in both XAML and code-behind:

```xml
<navigation:ContentPageView
    x:TypeArguments="tabs:ExampleTabViewModel"
    x:Class="MissionPlanner.App.Views.ConfigTuning.Tabs.ExampleTabView"
    x:DataType="tabs:ExampleTabViewModel">
    <!-- page content -->
</navigation:ContentPageView>
```

```csharp
public partial class ExampleTabView : ContentPageView<ExampleTabViewModel>
{
    public ExampleTabView()
    {
        InitializeComponent();
    }
}
```

As with lifecycle tabs, keep the page constructor parameterless and register the disposable
ViewModel as transient. `ContentPageView<TViewModel>` resolves and binds it when Shell
navigates to the page, then clears the binding and disposes it when Shell replaces or
removes the page. Do not duplicate this ownership in `OnAppearing`, `OnDisappearing`, or the
constructor.

### Navigate from application code

ViewModels must not manipulate `Shell.Current` or depend on MAUI Shell types. Inject a
purpose-specific navigation abstraction such as `INavigationService`; its UI-layer
implementation performs Shell changes on the dispatcher. `ShellNavigationService` shows
how Setup opens an existing Config destination by selecting the matching Shell hierarchy.

Prefer stable route or destination identifiers in navigation APIs. Keep the lookup and
Shell hierarchy knowledge inside the UI navigation service, validate that the destination
exists, and fail with a useful exception rather than silently selecting the wrong page.

Before leaving a guarded workspace, use its navigation guard. Config pages share pending
parameter state, so navigation within Config and navigation away from Config have different
semantics; callers must preserve that distinction rather than bypassing
`IConfigNavigationGuard`.

## Review checklist

- The chosen control matches the navigation level: child tab or Shell destination.
- The view is parameterless and XAML-constructible.
- The ViewModel is transient, implements `IDisposable`, and owns its subscriptions and
  cancellation lifetime.
- The view does not resolve a ViewModel when a lifecycle base class already owns it.
- Paired event attachment and detachment occur in the matching lifecycle methods.
- Shell destinations use a deferred `DataTemplate` and a unique, stable route.
- ViewModels navigate through an injected abstraction and honor workspace guards.
