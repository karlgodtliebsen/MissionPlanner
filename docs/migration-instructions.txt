# Avalonia UI implementation and migration guide

This is the working guide for implementing views in
`src/UI/AvaloniaUI/MissionPlanner.AvaloniaUI.App`. When restoring a feature from the
legacy application, treat that source only as a behavioral and visual specification.

## Required workflow

1. Read the complete legacy view, code-behind, and ViewModel before editing.
2. Inventory every visible section, command, binding, converter, dialog, map overlay, and
   lifecycle action.
3. Find the nearest completed Avalonia production view with the same role.
4. Reuse current Core services and UI adapters; do not create a parallel implementation.
5. Implement the complete AXAML tree and ViewModel behavior.
6. Build after each coherent view or workflow and fix binding/compiler errors immediately.
7. Compare the rendered result and behavior with the source inventory.
8. If an issue genuinely cannot be implemented, comment out only the affected block and add
   a precise `TODO` describing the missing behavior and dependency.

A successful build is not proof of parity. Empty panels, omitted commands, auto-generated
columns, or placeholder collections are incomplete migrations.

## Files and formatting

Use `.axaml`, `.axaml.cs`, and `ViewModel.cs`. Copy the namespace ordering and root
shape from a nearby target view. Use UTF-8 and Windows CRLF line endings. Keep all markup
multiline and reviewable.

Select `NavigationViewBase<T>`, `UserControlViewBase<T>`, or `TabItemViewBase<T>`
according to the view role. These bases own DI resolution and loaded/unloaded activation.
Do not resolve a second ViewModel in code-behind.

## Control translation

Translate behavior, not syntax:

| Legacy concept | Avalonia/Ursa implementation |
|---|---|
| Vertical or horizontal stack | `StackPanel` with `Orientation` |
| Page scroll container | `ScrollViewer` |
| Bounded repeated content | `ItemsControl` or `ListBox` |
| Large tabular data | `VirtualizedItemsGrid` |
| Expanding section | Avalonia `Expander` |
| Popup, prompt, or alert | `IDialogService` and Ursa dialog |
| Busy spinner | Indeterminate `ProgressBar` with `ProgressRing` theme |
| Visibility converter | `IsVisible` binding, compiled converter, or ViewModel property |
| Theme-specific color | Semantic `DynamicResource` or shared class |
| Platform dispatcher | `IUiDispatcher` / `Dispatcher.UIThread` |
| Platform file picker | Shared Avalonia storage-provider adapter |

Use `Background` and `Foreground`, not alternate-XAML color properties. Warning text
uses the `Warning` class. Escape composite binding formats, for example
`StringFormat={}{0:F1}`.

## Navigation and lifecycle

Use the Ursa drawer/navigation system described in
[UI_VIEW_LIFECYCLE_AND_NAVIGATION.md](UI_VIEW_LIFECYCLE_AND_NAVIGATION.md). Define routes in
`MissionPlannerRoutes`, create pages through `INavigationPageFactory`, and navigate through
`INavigationService`.

Each activation gets a fresh cancellation source. Deactivation cancels and disposes only
that activation's source and is safe when repeated. Vehicle-dependent pages must handle
connection changes and wait for the corresponding workspace data before publishing groups
or rows.

## Dialogs and files

Use Ursa dialogs where possible. Keep owner-window lookup, clipboard, notifications, and
storage-provider calls behind UI services. Open/save dialogs use the persisted last-directory
service. Domain services receive neutral values, paths, or streams.

## Mapsui views

Do not copy map-control code mechanically. Use Flight Data's map as the reference for:

- the shared source resolver and stable basemap layer;
- activation-scoped asynchronous source loading;
- UI-thread layer mutation;
- generation/cancellation checks that prevent stale commits;
- current-position initialization and navigator zoom;
- preservation of operational overlays during basemap changes;
- attribution, failure handling, and disposal.

Flight Planner and GeoFence maps should share these policies while retaining their own
mission/fence overlays and interaction logic.

## Skia and custom drawing

Replace platform canvas controls with an Avalonia custom control, Avalonia rendering, or the
existing Skia integration. Give the control an explicit background when transparency is not
intended. Invalidate rendering on the UI thread and detach timers/events on unload.

## Large data sets

Use `VirtualizedItemsGrid` for large parameter, firmware, or diagnostic collections.
`FullParametersListTabView` is the production reference; `DataGridPage` demonstrates row
selection. Read `Views/Samples/VirtualizedItemsGrid/README.md` before adding columns.
Do not use an auto-generated or unvirtualized grid for thousands of records.

## Completion checklist

- Every source section and interaction is represented.
- AXAML uses target-native controls, properties, bindings, and styles.
- ViewModels contain no window/control lookup or platform dialog calls.
- Activation, cancellation, connection changes, and collection threading are safe.
- Maps use the shared source and navigation policy.
- Large collections are virtualized.
- Theme selection, warning styles, and progress indicators use shared resources.
- File pickers remember the last directory.
- No placeholder, silent omission, or unresolved binding remains.
- The solution builds and relevant tests pass.
