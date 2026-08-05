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
then activates and enables the new content while the control is loaded. Loading activates
the selected tab and unloading deactivates it. Do not drive tab lifecycle from page
`OnAppearing`/`OnDisappearing` or ordinary navigation notifications: popup overlays can
produce those notifications even though the selected tab still owns the visible workflow.
`TabViewLifecycleContent<TViewModel>` implements
that contract by resolving a new ViewModel from DI on activation and clearing the binding,
disposing the ViewModel, and dropping its reference on deactivation.

Lifecycle selection is implemented in the protected TabView selection override, not from
the public `SelectedTabChanged` event. UraniumUI may clear `oldValue.Content` before raising
that event when `RecreateAlways` caching is active, so event-based cleanup must not
dereference the old `TabItem.Content`.

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

### Rich data-bound headers

Use `ExtendedTabView` when each static lifecycle tab needs a larger header view bound to a
separate data collection. `HeaderItemsSource` is aligned with `Tabs` by index; the header
template receives that item while the content keeps its lifecycle-owned binding context.
`SelectedHeaderItem` supports two-way selection binding. Keep both collections in the same
stable order and do not put lifecycle ownership on the header view. The control propagates
the attached `ExtendedTabView.IsHeaderSelected` state through a custom header view tree, so
nested elements can style selection with a self-relative `DataTrigger`; this state belongs
to the control and must not be duplicated in the header ViewModel.

For a reusable header `ContentView`, inherit `ExtendedTabHeaderView` and bind its nested
visuals to the ordinary `IsHeaderSelected` property on the root view. This avoids relying
on attached-property binding propagation through a templated `ContentView`. Debug builds
write one diagnostic line for each actual header selection transition, including header
type, bound data type, title when present, and the new state.

`ExtendedTabHeaderView` is also the preferred base class for consistent rich-card chrome.
It owns the border, selected left marker, and content host; derived XAML supplies only the
inner content because `HeaderContent` is the class content property. `SelectionColor` and
`SelectedStrokeThickness` can be styled per application theme without duplicating the
selection trigger or marker layout in every header view. The unselected border uses a
transparent brush rather than a null stroke because the MAUI Windows border handler cannot
convert a `SolidPaint` whose color is null while attaching the visual tree.

Selection visuals remain template-owned. Rich card headers should use the established
narrow primary-color bar on the left edge; compact horizontal headers may retain
UraniumUI's bottom indicator. Bind the bar's visibility to the same header selection state
rather than adding selection state to the header data model.

```xml
<tabViews:ExtendedTabView
    HeaderItemsSource="{Binding Workflows}"
    SelectedHeaderItem="{Binding SelectedWorkflow, Mode=TwoWay}"
    TabHeaderItemColumnWidth="290">
    <material:TabView.TabHeaderItemTemplate>
        <DataTemplate x:DataType="local:WorkflowHeaderModel">
            <local:WorkflowHeader />
        </DataTemplate>
    </material:TabView.TabHeaderItemTemplate>
    <material:TabItem Title="First">
        <local:FirstWorkflowView />
    </material:TabItem>
    <material:TabItem Title="Second">
        <local:SecondWorkflowView />
    </material:TabItem>
</tabViews:ExtendedTabView>
```

The Mandatory Hardware page is the production reference. Its rich workflow cards come
from `Workflows`, while each setup content view resolves its transient ViewModel only when
selected and disposes it on tab change or actual removal from the visual tree. The
`ExtendedTabViewPage` sample is the compact control demonstration.

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
- Rich headers use `HeaderItemsSource`; header views do not create lifecycle ViewModels.
- Popup display does not deactivate the selected tab; only selection or visual-tree
  lifecycle changes do.
- Paired event attachment and detachment occur in the matching lifecycle methods.
- Shell destinations use a deferred `DataTemplate` and a unique, stable route.
- ViewModels navigate through an injected abstraction and honor workspace guards.
