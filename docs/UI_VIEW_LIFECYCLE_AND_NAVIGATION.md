# Avalonia view lifecycle and navigation

This document defines the current view, lifecycle, and navigation conventions for
`MissionPlanner.AvaloniaUI.App`.

## File structure

Use `ExampleView.axaml`, `ExampleView.axaml.cs`, and `ExampleViewModel.cs`. Keep AXAML and
code-behind readable, UTF-8, and CRLF. Code-behind should contain only initialization and
visual integration; state and commands belong in the ViewModel.

Use the namespace and root-element organization of a nearby production view:

```xml
<utilities:NavigationViewBase
    xmlns="https://github.com/avaloniaui"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
    xmlns:utilities="clr-namespace:MissionPlanner.AvaloniaUI.App.Utilities"
    xmlns:local="clr-namespace:MissionPlanner.AvaloniaUI.App.Views.Example"
    x:TypeArguments="local:ExampleViewModel"
    x:Class="MissionPlanner.AvaloniaUI.App.Views.Example.ExampleView"
    x:DataType="local:ExampleViewModel"
    x:CompileBindings="True"
    mc:Ignorable="d">
    <ContentPage Header="Example"  Classes="SectionCard" >
        <!-- complete view content -->
    </ContentPage>
</utilities:NavigationViewBase>
```

## Base classes and lifecycle

| Role | Base class |
|---|---|
| Navigable Ursa page | `NavigationViewBase<TViewModel>` |
| Reusable content | `UserControlViewBase<TViewModel>` |
| Tab content | `TabItemViewBase<TViewModel>` |
| Dialog content | `DialogViewModelBase` plus an AXAML view |
| Top-level window | `WindowBase<TViewModel>` or `UrsaWindow` |

The generic view bases resolve their ViewModel from DI, assign `DataContext`, install dialog
and notification managers, and call `ActivateAsync`/`DeactivateAsync` from Avalonia's
loaded/unloaded lifecycle. Keep view constructors parameterless.

Activation and deactivation can repeat. Each activation owns a fresh cancellation source;
deactivation atomically detaches, cancels, and disposes it. Never reactivate with a cancelled
token. Dispose EventHub subscriptions and detach ordinary events at the same ownership
boundary. After each `await`, verify cancellation, activation generation, and current vehicle
or workspace identity before publishing state. Observable UI collections are changed through
`IUiDispatcher` or Avalonia `Dispatcher.UIThread`.

## Navigation

`Views/Navigation/MainShellView.axaml` uses an Ursa `DrawerPage` and `NavMenu`. The selected
page is exposed by `MainShellViewModel.Content` and bound to `DrawerPage.Content`.

- Define stable route names in `MissionPlannerRoutes`.
- Define menu entries with `NavigationMenuItemViewModel`.
- Create destinations through `INavigationPageFactory`/`NavigationPageFactory`.
- Navigate from ViewModels through `INavigationService`; never locate shell controls from a
  feature ViewModel.
- Keep destination content on `DrawerPage.Content`, outside the drawer panel.
- Store menu icons as `Avalonia.Media.Imaging.Bitmap`; load packaged assets with
  `AssetLoader`. A path string cannot bind directly to `Image.Source`.
- Application exit invokes the window-close path and is not a navigation destination.

Ursa `NavigationPage` and `ContentPage` provide page chrome and breadcrumb behavior. The
drawer button remains the primary way to reveal application navigation.

## Dialogs, files, and notifications

Use `IDialogService` and `Utilities/Dialogs`, preferring Ursa dialogs and notifications.
Feature ViewModels do not create windows or call platform UI APIs. Resolve an owner through
`IWindowProvider`. Open/save dialogs use the shared persisted-directory service so the most
recent folder survives application restarts. Core services accept paths, streams, or neutral
request objects and never open dialogs themselves.

Progress is presented by DisplayProgressCancellableAsync through
ShowOverlayDialogAsync with centered OverlayDialogOptions, FullScreen=false,
no resize, no light-dismiss, and no automatic dialog buttons. It does not create a
native window. The progress content determines its compact size. The method returns
an IDisposable handle immediately; disposing it cancels the overlay lifetime and closes
the overlay. The message timer is disposed when the overlay finishes.

When DialogOptions.RequestCancellation is supplied, the content's Cancel operation
button requests cancellation without closing the overlay. The operation owner retains
the handle until work can safely end. Without that callback, cancelling the caller's
token also closes the overlay. CloseAsync targets the latest progress overlay and
respects the same deferred-cancellation behavior.

ProgressOverlayTests cover immediate handle return, opening failures, pre-cancelled calls,
deferred cancellation, and close/cleanup behavior. The UI suite passed all 145 tests on
2026-09-18. Interactive overlay appearance has not been manually verified.

## Collections and large grids

Use `ItemsControl` or `ListBox` only for bounded lists. Use `VirtualizedItemsGrid` for large
or unbounded row sets. `FullParametersListTabView` is the production reference and
`Views/Samples/DataGridPage` demonstrates row selection. See
`Views/Samples/VirtualizedItemsGrid/README.md` for the control contract.

Keep sources stable when practical, update them on the UI dispatcher, cancel stale loads on
deactivation, and coalesce refreshes. Do not render thousands of rows through an unvirtualized
panel.

## AXAML rules

- Use Avalonia properties: `Text`, `Foreground`, `Background`, `HorizontalAlignment`,
  `IsVisible`, and `Classes`.
- Prefer compiled bindings with `x:DataType` and `x:CompileBindings="True"`.
- Escape composite formats, for example `StringFormat={}{0:F1}`.
- Use semantic dynamic resources and shared classes. Warning text uses `Classes="Warning"`;
  append `Warning` when other classes exist.
- An indeterminate `ProgressBar` uses `Theme="{DynamicResource ProgressRing}"`.
- Merge reusable styles in `App.axaml` or the appropriate local resource dictionary.

## Review checklist

- The visual tree contains every required section and command.
- The base class matches the view's role and the constructor is XAML-constructible.
- DI, activation, cancellation, and event ownership are explicit.
- Navigation uses routes, the page factory, and `INavigationService`.
- Dialogs, clipboard, notifications, and file pickers remain UI adapters.
- Large lists use `VirtualizedItemsGrid` and preserve required selection behavior.
- Bindings compile and UI state is updated on the Avalonia dispatcher.
- Colors and states use shared styles and semantic resources.
- The solution builds and relevant tests and runtime navigation paths are exercised.
# Visual Studio preview host

`MissionPlanner.App` is a shared library, so adding a `Main` method there does not
give its assembly an executable entry point. Select `MissionPlanner.Desktop` as
the Avalonia preview host (or Visual Studio startup project), build it, and reopen
the AXAML editor. The Browser executable is not the Windows designer host.

The Desktop entry-point class exposes a parameterless `BuildAvaloniaApp()`.
In `Design.IsDesignMode` it uses the shared resource-only builder, including the
normal Semi/Ursa application styles, and does not build or start runtime services.
Shared view bases skip dependency injection, activation and notification-manager
creation in design mode. Supply `Design.DataContext` for sample data if desired;
runtime ViewModels are deliberately not activated by the designer. View-specific
loaded handlers that call their ViewModel must also guard design mode.

Verified with Avalonia 12.1.1's standalone designer host: compiled `HelpView`,
`FirmwareCatalogueView`, `InstallFirmwarePage`, `MainView`, `FlightDataPage`,
`FlightPlannerPage`, `GeoFenceMapView`, `SimulationLocationMapView`, and
`IntroductionPage` loaded through temporary AXAML wrappers with no
`UpdateXamlResultMessage` error, including unload/reload. This checks designer
startup and view construction; Visual Studio extension selection remains an IDE
setting. See the [Avalonia previewer protocol](https://github.com/AvaloniaUI/Avalonia/wiki/XAML-previewer-protocol).

Verification (2026-09-07): `dotnet build` for `src/MissionPlanner.slnx`,
`src/Platforms/MissionPlanner.Desktop/MissionPlanner.Desktop.csproj`, and
`src/Platforms/MissionPlanner.Browser/MissionPlanner.Browser.csproj` with
`--no-restore -p:UsedAvaloniaProducts= -v quiet` all passed. Running
`src/Tests/Run-AllTests.ps1` passed 930 .NET and 7 JavaScript tests; the 29 existing
skips were unchanged. The isolated `DesignPreviewTests` regression exercises all
view-base families without a runtime service provider.
