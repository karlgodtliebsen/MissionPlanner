# MAUI-to-Avalonia UI Migration Guide

This document is the working guide for migrating views and ViewModels from
`src/UI/MAUI/MissionPlanner.App` to
`src/UI/AvaloniaUI/MissionPlanner.AvaloniaUI.App`.

The goal is behavioral and content parity, not merely XAML that compiles. A migrated view must
contain the complete source UI, preserve its commands and lifecycle, use the shared Avalonia
styles and dialog infrastructure, and compile as part of the full solution.

## 1. Required workflow

### 1.1 Read both implementations before editing

Before changing a target view:

1. Read the complete MAUI `.xaml`, `.xaml.cs`, and `ViewModel.cs` files.
2. Read the existing Avalonia `.axaml`, `.axaml.cs`, and `ViewModel.cs` files.
3. Inventory the source view by visual section. Record every:
   - heading, status message, warning, and explanatory text;
   - input, selector, toggle, button, menu, list, grid, map, and custom control;
   - binding, command, command parameter, converter, visibility condition, and validation state;
   - event handler and code-behind interaction;
   - loading, empty, error, read-only, replay, and disconnected state;
   - dialog, file picker, navigation action, and lifecycle operation.
4. Compare the inventory with the target. Do not assume the existing target is complete.
5. Migrate every source section. A short placeholder, old stub, or previously migrated subset is
   not an acceptable replacement for the complete source content.

The Flight Data tabs are an important warning: several earlier target tabs compiled while only a
portion of their MAUI content had been copied. Compilation is a verification step, not a parity
check.

### 1.2 Preserve architecture and ownership

- Keep UI concerns in the Avalonia application.
- Keep domain behavior in Core and protocol behavior out of views.
- Reuse existing services, ViewModels, factories, dispatchers, styles, dialogs, and map presenters.
- Do not introduce a second implementation merely because a MAUI control has no direct Avalonia
  equivalent.
- Do not modify the MAUI source while using it as the behavioral reference.

### 1.3 Finish with a parity review

After the target compiles, compare source and target again, section by section. Confirm that every
source command is reachable, every important state is visible, and no source content disappeared
during layout conversion. Report any intentional omission in a `TODO` comment and in the task
summary.

## 2. Files, namespaces, and build action

- Rename `.xaml` files to `.axaml` in the Avalonia project.
- Rename the paired code-behind file to `.axaml.cs`.
- Use the Avalonia namespace:

  ```xml
  xmlns="https://github.com/avaloniaui"
  xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
  ```

- Change namespaces from `MissionPlanner.App...` to
  `MissionPlanner.AvaloniaUI.App...`.
- Ensure `x:Class`, the code-behind namespace, and the code-behind class name match exactly.
- Add `x:DataType` for compiled bindings and the correct `x:TypeArguments` on generic base views.
- The build action must be **Avalonia XAML**. Ordinary `.axaml` files are included by the Avalonia
  SDK automatically. Add an explicit `<AvaloniaXaml Update="...">` entry only when a file needs a
  non-default generator setting.
- Do not disable the XAML generator merely to hide migration errors. Existing disabled entries,
  such as the legacy mission-item grid, are temporary exceptions and require an explicit TODO.

Recommended header:

```xml
<utilities:UserControlViewBase
    x:TypeArguments="feature:FeatureViewModel"
    xmlns="https://github.com/avaloniaui"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:utilities="clr-namespace:MissionPlanner.AvaloniaUI.App.Utilities"
    xmlns:feature="clr-namespace:MissionPlanner.AvaloniaUI.App.Views.Feature"
    mc:Ignorable="d"
    d:DesignWidth="800"
    d:DesignHeight="450"
    x:Class="MissionPlanner.AvaloniaUI.App.Views.Feature.FeatureView"
    x:DataType="feature:FeatureViewModel">
</utilities:UserControlViewBase>
```

## 3. Select the correct Avalonia view type

Use the base type that matches the role of the view. Do not mechanically convert every MAUI
`ContentView` to the same target type.

| Source role | Avalonia target | Current example |
| --- | --- | --- |
| Reusable view/user control | `utilities:UserControlViewBase<TViewModel>` | `ConnectPopupView`, `StatusBarView` |
| Root navigable page | `utilities:NavigationViewBase<TViewModel>` containing a `ContentPage` | `FlightDataPage`, `DialogDemoPage` |
| Content page hosted by existing navigation | `utilities:ContentViewBase<TViewModel>` when that is the established feature pattern | Existing content-page views |
| Page that owns several page-level tabs | `utilities:TabbedPageViewBase<TViewModel>` | Existing tabbed setup/configuration pages |
| Content hosted inside a `TabItem` | `utilities:TabItemViewBase<TViewModel>` | Flight Data tab views |
| Plain reusable control with externally supplied `DataContext` | Non-generic `UserControlViewBase` or `UserControl` | `MissionMapView` |
| Custom-rendered control | Avalonia `Control` with Avalonia properties and rendering | `HudCanvas`, `FlightGaugeView` |

The generic base classes resolve the ViewModel from dependency injection, assign `DataContext`,
and participate in load/unload lifecycle handling. Do not add another `DataContext` assignment or
duplicate activation unless the view has additional owned resources.

### Root navigation page

```xml
<utilities:NavigationViewBase
    x:TypeArguments="flightData:FlightDataViewModel"
    xmlns="https://github.com/avaloniaui"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:utilities="clr-namespace:MissionPlanner.AvaloniaUI.App.Utilities"
    xmlns:flightData="clr-namespace:MissionPlanner.AvaloniaUI.App.Views.FlightData"
    x:Class="MissionPlanner.AvaloniaUI.App.Views.FlightData.FlightDataPage"
    x:DataType="flightData:FlightDataViewModel">
    <ContentPage Header="Flight Data">
        <!-- Complete page content -->
    </ContentPage>
</utilities:NavigationViewBase>
```

### Flight Data tab content

```xml
<utilities:TabItemViewBase
    x:TypeArguments="tabs:ActionsTabViewModel"
    xmlns="https://github.com/avaloniaui"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:utilities="clr-namespace:MissionPlanner.AvaloniaUI.App.Utilities"
    xmlns:tabs="clr-namespace:MissionPlanner.AvaloniaUI.App.Views.FlightData.Tabs"
    x:Class="MissionPlanner.AvaloniaUI.App.Views.FlightData.Tabs.ActionsTabItemView"
    x:DataType="tabs:ActionsTabViewModel">
    <!-- Complete source tab content -->
</utilities:TabItemViewBase>
```

The corresponding `TabItem` in `FlightDataPage.axaml` hosts the view:

```xml
<TabItem Header="Actions">
    <tabs:ActionsTabItemView />
</TabItem>
```

## 4. ViewModel migration

- Change `BaseViewModel` to `MissionPlanner.AvaloniaUI.App.Utilities.ViewModelBase`.
- Pass `ILogger<TViewModel>` to `base(logger)`.
- Remove duplicate local logger fields and use the protected `Logger` property.
- Replace injected MAUI `IDispatcher` calls with the protected `Dispatcher` supplied by
  `ViewModelBase`.
- Prefer `Dispatcher.Dispatch(...)` for synchronous queued UI work and
  `await Dispatcher.DispatchAsync(...)` when completion must be observed.
- Keep constructor injection for services. Register the ViewModel with the existing application
  configuration rather than resolving it manually in the view.
- Retain and dispose every EventHub subscription.
- Make activation and deactivation idempotent. Repeated load/unload cycles must not register the
  same event twice or dispose the same subscription twice.
- Cancel only work owned by the ViewModel. Do not add a `CancellationTokenSource` automatically.
  Use the callback or command token when it already represents the operation lifetime. If an owned
  source is necessary, detach event handlers first, cancel safely, await owned work where needed,
  and avoid racing `Cancel()` with `Dispose()`.
- Do not dispatch delayed updates after deactivation. Check the active state inside the dispatched
  callback, not only before queuing it.
- Call `base.Dispose()` from overrides after releasing feature-owned resources.

## 5. Core control mapping

| MAUI / Uranium | Avalonia |
| --- | --- |
| `VerticalStackLayout` | `StackPanel Orientation="Vertical"` |
| `HorizontalStackLayout` | `StackPanel Orientation="Horizontal"` |
| `Grid` | `Grid` |
| `ScrollView` | `ScrollViewer` |
| Display-only `Label` | Prefer `TextBlock Text="..."` |
| Content-oriented `Label` | `Label Content="..."` |
| `Entry` | `TextBox` |
| `Editor` / `AlignedEditorField` | `TextBox`; set `AcceptsReturn` and wrapping when multiline |
| `Picker` / `SelectField` | `ComboBox` |
| `Switch` | `ToggleSwitch` |
| `CheckBox` | `CheckBox` |
| `CollectionView` | `ItemsControl`, `ListBox`, or `DataGrid`, selected by behavior and scale |
| `Frame` | `Border` |
| `Border` | `Border` |
| `BoxView` | `Border` or `Rectangle` |
| `ActivityIndicator` | `ProgressBar IsIndeterminate="True"` |
| `ImageButton` | Styled `Button` containing an `Image` or icon control |
| `FlexLayout` used for wrapping actions | `WrapPanel` |
| Uranium `RightDockPanel` | Avalonia/Semi dock control when its behavior is verified, or a `Grid` with dock column and `GridSplitter` |
| `TabbedPage` / tab extension | `TabbedPageViewBase` or `TabControl` / `TabItem` |
| `BindingContext` | `DataContext` |
| `IsVisible` | `IsVisible` |
| `Command` | Usually unchanged |
| MAUI triggers/behaviors | Avalonia selectors, classes, pseudo-classes, behaviors, or converters |
| `VisualStateManager` | Avalonia styles/selectors/classes/pseudo-classes |

### Alignment and layout properties

| MAUI | Avalonia |
| --- | --- |
| `HorizontalOptions="Fill"` | `HorizontalAlignment="Stretch"` |
| `HorizontalOptions="Center"` | `HorizontalAlignment="Center"` |
| `HorizontalOptions="Start"` | `HorizontalAlignment="Left"` |
| `HorizontalOptions="End"` | `HorizontalAlignment="Right"` |
| `VerticalOptions="Fill"` | `VerticalAlignment="Stretch"` |
| `VerticalOptions="Center"` | `VerticalAlignment="Center"` |
| `VerticalOptions="Start"` | `VerticalAlignment="Top"` |
| `VerticalOptions="End"` | `VerticalAlignment="Bottom"` |
| `Spacing` | `Spacing` on `StackPanel`; `RowSpacing` / `ColumnSpacing` on `Grid` |
| `Padding` on layouts | Use a wrapping `Border Padding="..."`; Avalonia `Grid` and `StackPanel` do not have `Padding` |

Other common property changes:

| MAUI | Avalonia |
| --- | --- |
| `BackgroundColor` | `Background` |
| `TextColor` | `Foreground` |
| `FontAttributes="Bold"` | `FontWeight="Bold"` |
| `LineBreakMode="WordWrap"` | `TextWrapping="Wrap"` on `TextBlock` |
| `Border.Stroke` / `Stroke` | `BorderBrush` plus `BorderThickness` |
| `StyleClass="A,B"` | `Classes="A B"` |
| `ToolTipProperties.Content` | `ToolTip.Tip` |
| `Switch.IsToggled` | `ToggleSwitch.IsChecked` |
| `WidthRequest` / `HeightRequest` | `Width` / `Height` |
| `MinimumWidthRequest` / `MinimumHeightRequest` | `MinWidth` / `MinHeight` |
| `MaximumWidthRequest` / `MaximumHeightRequest` | `MaxWidth` / `MaxHeight` |

Use `Text` for `TextBlock` and `TextBox`; use `Content` for `Label`, `Button`, `CheckBox`, and
other content controls.

`RightDockPanel` must not be left unchanged: it belongs to the MAUI Uranium package and will not
compile as an Avalonia control. Preserve both its main content and dock content. Recreate the
layout with the project's Avalonia/Semi docking components if their behavior is already established,
or use a two-column `Grid` with a `GridSplitter`, explicit min/max dock widths, and a bound
expanded/collapsed state. Verify resizing, collapsing, narrow-window behavior, and persistence.

## 6. Inputs and selectors

### ComboBox

```xml
<ComboBox
    ItemsSource="{Binding MapStyles}"
    SelectedItem="{Binding SelectedMapStyle}"
    DisplayMemberBinding="{Binding .}" />
```

For object items, bind the display member, for example `DisplayMemberBinding="{Binding Name}"`.
Do not carry over `ItemDisplayBinding` from MAUI.

### TextBox

```xml
<TextBox
    PlaceholderText="Seven invariant parameters, separated by spaces"
    Text="{Binding ExpertParameters, Mode=TwoWay}" />
```

Use `PlaceholderText`, not the obsolete `Watermark` alias. A multiline editor normally uses
`AcceptsReturn="True"` and `TextWrapping="Wrap"`.

### NumericUpDown

```xml
<NumericUpDown
    Value="{Binding StepSize, Mode=TwoWay}"
    Minimum="0"
    Maximum="10000"
    Increment="10"
    FormatString="F1" />
```

Mappings:

- `Min` -> `Minimum`
- `Max` -> `Maximum`
- `StepSize` -> `Increment`
- `NumberFormat` -> `FormatString`
- `Title` -> a separate `TextBlock`/`Label`, or `PlaceholderText` where supported and appropriate

### Formatted bindings

Avalonia markup extensions require an escape prefix when the format begins with braces:

```xml
<TextBlock Text="{Binding Altitude, StringFormat='{}{0:F1}'}" />
<TextBlock Text="{Binding PendingCommand, StringFormat='{}Pending: {0}'}" />
```

Unescaped `StringFormat='{0:F1}'` is not valid Avalonia XAML.

## 7. Shared styles and theming

`Resources/Styles/SharedStyles.axaml` is included globally by `App.axaml`. Reuse its classes
before adding one-off sizes, margins, colors, or typography to individual views.

Current reusable patterns include:

- `Border.SectionCard` for grouped content;
- `TextBlock.SectionTitle` for section headings;
- typography classes such as `H1` through `H6` on supported controls;
- form sizing classes such as `size`, `small-size`, and `xsmall-size`;
- clear-button text box classes;
- button classes supplied by the active Semi/Ursa themes, such as `Secondary`, when already used
  by the application.

Apply classes with a space-separated list:

```xml
<Border Classes="SectionCard">
    <TextBlock Classes="SectionTitle" Text="Connection" />
</Border>
```

Theming rules:

- Prefer semantic `DynamicResource` brushes and existing Semi/Ursa resources over literal colors.
- Do not recreate styles locally if the same visual role exists in `SharedStyles.axaml`.
- Add a shared style only when multiple views use the same semantic pattern.
- Verify light and dark themes after migration.
- A view-level `Background` does not repair a custom Skia operation that clears the shared render
  target. Custom Skia controls must paint only their bounds and must not call
  `canvas.Clear(SKColors.Transparent)`. Fill the control-sized rectangle or render through normal
  Avalonia composition instead.

## 8. Dialog migration

Use the injected `IDialogService` and the existing Ursa-based overlay dialogs. Do not create a new
window, message-box abstraction, or ad-hoc popup when the service already supports the interaction.

Supported shared interactions include:

- confirmation;
- string, integer, and double prompts;
- choice selection;
- custom view/ViewModel overlays;
- cancellable progress;
- error and notification views.

Create options with `AvaloniaDialogService.CreateDialogOptions(...)` or
`IDialogService.CreateOptions(...)`, then pass the command cancellation token:

```csharp
var options = AvaloniaDialogService.CreateDialogOptions(
    "Confirm initial ArduPilot installation",
    "Continue",
    "Cancel");

var confirmed = await dialogService.ConfirmAsync(
    options,
    "Continue with installation?",
    cancellationToken);
```

Custom dialog example:

```csharp
var options = AvaloniaDialogService.CreateDialogOptions("Connect Vehicle", "OK", "Cancel");
var viewModel = serviceFactory.Create<ConnectPopupViewModel>();
var result = await dialogService.ShowOverlayDialogAsync<ConnectPopupView, ConnectPopupViewModel>(
    viewModel,
    options,
    cancellationToken: cancellationToken);
```

Custom overlay ViewModels derive from `DialogViewModelBase`. Resolve ViewModels through the
appropriate application/domain factory when they have dependencies; do not manually assemble
registered dependencies. Use `DialogDemoPage` and `DialogDemoViewModel` as the runnable catalog of
current dialog patterns.

Do not nest a second scroll viewer around dialog content that already contains a `ScrollViewer`,
`ListBox`, or `DataGrid`.

## 9. Mapsui map migration

Do not translate MAUI map code control-for-control. Use the shared Avalonia mission-map
implementation and follow the Flight Data map.

Primary references:

- `Views/FlightData/FlightDataPage.axaml`
- `Views/FlightData/FlightDataPage.axaml.cs`
- `Views/FlightData/FlightDataMissionMapView.cs`
- `Views/FlightData/FlightDataMissionMapViewModel.cs`
- `Views/Missions/MissionMapView.axaml`
- `Views/Missions/MissionMapView.axaml.cs`
- `Views/Missions/MissionMapPresenter.cs`
- `Maps/MapBasemapController.cs`
- `Maps/MapsuiBasemapFactory.cs` and related basemap factories

### Required structure

1. Reference the Avalonia Mapsui control:

   ```xml
   xmlns:mapsui="clr-namespace:Mapsui.UI.Avalonia;assembly=Mapsui.UI.Avalonia"
   ```

2. Host a named control:

   ```xml
   <mapsui:MapControl x:Name="MissionMap" />
   ```

3. Keep native Mapsui events in the view boundary. `MissionMapView` subscribes to map taps and
   pointer movement and converts Web Mercator coordinates with
   `SphericalMercator.ToLonLat(...)`.
4. Keep rendering and navigation in `MissionMapPresenter`. It owns the `Mapsui.Map`, memory layers,
   basemap switching, marker/route/planning rendering, viewport navigation, pointer throttling,
   attribution, and terrain-elevation lookup.
5. Keep mission/domain state in `MissionMapViewModel`. Project it to map snapshots rather than
   exposing Mapsui types from the ViewModel or Core.
6. Create presenters through `IDomainFactory`; do not manually resolve their registered
   dependencies.
7. Use `MapBasemapController` and the registered source/factory abstractions. Do not hard-code a
   tile provider in a view.
8. Perform map mutations on the map UI dispatcher.

### Flight Data reuse pattern

`FlightDataMissionMapView` derives from the shared `MissionMapView`, and
`FlightDataMissionMapViewModel` derives from `MissionMapViewModel`. `FlightDataPage` hosts the
specialized view and explicitly coordinates its extra lifecycle:

- activate the map ViewModel;
- call `MapView.ActivateAsync(ViewModel.Map)` when the page loads;
- call `MapView.DeactivateAsync()` and deactivate the map ViewModel when the page unloads;
- show loading state while asynchronous map initialization is running;
- unsubscribe native map and ViewModel events on deactivation;
- cancel owned map-source and pointer-elevation work;
- dispose the presenter when the view is permanently disposed.

Use this composition for pages that need the mission-map behavior. Add a focused presenter only
when a map has genuinely different behavior; still reuse the basemap controller, source resolver,
attribution, projection, dispatcher, and lifecycle conventions.

### Map verification

Verify at minimum:

- initial center from vehicle position, with platform-location fallback;
- map tap and pointer coordinate conversion;
- zoom, center, rotation, follow-vehicle, and fit-to-route operations;
- basemap/style switching and attribution;
- vehicle, route, marker, and planning overlay refresh;
- activation/deactivation without duplicate event handlers;
- offline/error/loading states;
- light/dark theme readability of overlays and status content.

## 10. Custom drawing and Skia

- A MAUI `SKCanvasView` cannot be copied directly.
- Derive from Avalonia `Control`.
- Replace `BindableProperty` with `StyledProperty<T>` registered through `AvaloniaProperty`.
- Register visual properties with `AffectsRender<TControl>(...)`.
- Override `Render(DrawingContext)` and use an `ICustomDrawOperation` plus
  `ISkiaSharpApiLeaseFeature` when Skia is required. Follow `HudCanvas` and `FlightGaugeView`.
- Snapshot bindable values before handing work to the draw operation.
- Draw only inside the operation bounds.
- Never clear the shared target transparently; paint an opaque bounded background when the control
  requires one.
- Detach any owned events and dispose native resources.

## 11. Lists, DataGrid, and virtualization

Choose the control according to interaction and expected item count:

- `ItemsControl` for small, display-only collections;
- `ListBox` for selection and ordinary lists;
- Avalonia/Semi `DataGrid` for tabular editing and sorting where its behavior is verified;
- a virtualizing solution for large or high-frequency collections.

Current migration boundary:

- Do not silently replace `VirtualizedDataGrid` with a non-virtualized `ItemsControl` for large
  datasets.
- Do not assume the Avalonia `DataGrid` is adequate until row creation, scrolling, editing,
  filtering, sorting, selection, and memory behavior have been measured.
- Preserve the complete original grid definition in a clearly marked commented block only when no
  working equivalent has been selected.
- Add a specific TODO describing the missing behaviors and expected scale. Do not write only
  `TODO: migrate grid`.
- Keep surrounding filters, actions, status, empty-state, and command UI active even if the grid is
  temporarily unavailable.
- `FullParametersListView` is the final large-data validation target. Test it with a realistic full
  parameter set and measure responsiveness and memory before declaring the DataGrid migration
  strategy complete.

Suggested TODO:

```xml
<!-- TODO: Restore the parameter table after validating Avalonia/Semi DataGrid row
     virtualization, editing, sorting, filtering, selection retention, and memory use with a
     realistic FullParametersListView dataset. Keep the original column/binding inventory below. -->
```

## 12. Resources, icons, and images

- Add application images as `AvaloniaResource` when they are compiled resources.
- Use Avalonia resource URIs where required (`avares://...`).
- Prefer the installed `Material.Icons.Avalonia` control and existing icon resources instead of
  copying platform-specific glyph mechanisms.
- Replace `StaticResource`/`DynamicResource` keys only after confirming the target theme defines the
  key.
- Prefer semantic dynamic resources for colors that change with theme.

## 13. Code-behind and lifecycle

Code-behind is appropriate for framework-native UI interactions such as:

- Mapsui pointer/tap events;
- focus and selection behavior that cannot be expressed cleanly through bindings;
- view-owned custom rendering;
- activation/deactivation of a view-owned presenter or native control.

Keep commands and application behavior in the ViewModel. Every event subscription added by a view
must be removed during deactivation or disposal. Do not let page unload race background work that
can still update the visual tree.

If a generic base view already activates/deactivates its ViewModel, do not repeat that call in the
derived code-behind. Derived lifecycle overrides should manage only additional resources owned by
that view and must call `base.OnLoaded(e)` / `base.OnUnloaded(e)` in the correct order.

## 14. TODO policy

When an element cannot yet be migrated:

1. Keep as much surrounding feature content functional as possible.
2. Leave a TODO in the target at the exact omission.
3. State what is missing, why it is blocked, what behavior must be preserved, and how it will be
   verified.
4. List every remaining TODO in the task completion report.

Do not use a TODO to hide a compile error that has an established Avalonia mapping.

## 15. Verification checklist

### Content and behavior

- [ ] Every MAUI visual section exists in the Avalonia view.
- [ ] Every source binding and command has been accounted for.
- [ ] Empty, loading, error, disconnected, replay/read-only, and validation states are preserved.
- [ ] Dialogs use `IDialogService`/Ursa patterns.
- [ ] Maps use the shared Flight Data/Mission Map architecture where applicable.
- [ ] No placeholder remains in place of source content.
- [ ] Every intentional omission has a detailed TODO.

### XAML and styling

- [ ] File extension is `.axaml`; build action is Avalonia XAML.
- [ ] `x:Class`, namespace, base type, `x:TypeArguments`, and `x:DataType` agree.
- [ ] No MAUI or Uranium-only namespace, control, or property remains.
- [ ] Formatted bindings use Avalonia escaping.
- [ ] Existing shared styles and semantic resources are reused.
- [ ] Light and dark themes remain readable.

### Lifecycle and performance

- [ ] Activation/deactivation is idempotent.
- [ ] EventHub and native event subscriptions are released.
- [ ] Owned asynchronous work is cancelled safely.
- [ ] Delayed UI updates cannot run after deactivation.
- [ ] Large lists use a measured virtualization strategy or retain a detailed TODO.
- [ ] Maps do not duplicate layers or event handlers after repeated navigation.

### Build

From `src/`:

```powershell
dotnet restore .\MissionPlanner.slnx
dotnet build .\MissionPlanner.slnx --no-restore
dotnet test .\MissionPlanner.slnx --no-build
```

During incremental work, build the Avalonia project after each error group, but finish with the
full solution build. Inspect the complete output for Avalonia XAML errors and for `CS1591` and
`CS1587` documentation warnings in affected files.

## 16. Completion report

At the end of every migration task, report:

- views and ViewModels migrated;
- source sections checked for parity;
- important control/style/dialog/map choices;
- build and test commands executed and their results;
- all remaining TODOs and unsupported controls;
- manual checks still required, especially theme, map interaction, hardware, and large-data
  performance checks.
