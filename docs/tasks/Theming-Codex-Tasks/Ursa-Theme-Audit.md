# Ursa theme audit

Ursa is integrated through `UrsaSemiTheme` in `App.axaml`. MissionPlanner views use the
control themes supplied by Semi/Ursa together with application styles in
`Resources/Styles/SharedStyles.axaml`.

| Area | Current integration |
|---|---|
| Navigation | Ursa `DrawerPage`, `NavMenu`, `NavigationPage`, and `ContentPage` |
| Dialogs | `IDialogService`, Ursa dialogs/notifications, and `ViewDialogWindow` |
| Theme selection | `AvaloniaThemeCatalog` maps persisted IDs to Avalonia/Semi variants |
| Busy state | Indeterminate `ProgressBar` uses the `ProgressRing` control theme |
| Shared state colors | Avalonia classes and semantic `DynamicResource` brushes |
| Large grids | MissionPlanner `VirtualizedItemsGrid` plus shared control styles |

Audit rules:

- Do not copy control templates into feature views merely to change a color.
- Do not branch on light/dark mode inside a feature ViewModel.
- Do not bind a string path to `Image.Source`; expose a loaded bitmap.
- Append semantic classes such as `Warning` to existing classes.
- Verify popup, hover, focus, disabled, selection, and title-bar states in every supported
  variant.

See [Theming.md](../../Theming.md) for the authoritative implementation contract.
