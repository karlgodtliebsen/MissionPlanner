# MissionPlanner Avalonia theming tasks

This task record reflects the implemented Avalonia/Semi/Ursa theme architecture. The
authoritative developer guidance is [Theming.md](../../Theming.md).

## Current contract

- `App.axaml` loads `SemiTheme`, `UrsaSemiTheme`, popup animations, Material icons,
  `SharedStyles.axaml`, and virtualized-grid styles.
- `AvaloniaThemeCatalog` owns the selectable variants and stable persisted identifiers.
- `TopBarViewModel` exposes the choices and applies them through
  `Application.Current.RequestedThemeVariant`.
- Planner settings persist Avalonia identifiers. Compatibility aliases are read only to
  migrate existing settings.
- Views use Avalonia brush properties, semantic `DynamicResource` values, and shared
  `Classes`.
- Warning text appends the `Warning` class.
- Indeterminate progress bars use `Theme="{DynamicResource ProgressRing}"`.

## Remaining verification

- Switch every theme from the top bar and restart the application.
- Verify Ursa navigation, dialogs, title-bar controls, maps, warnings, and virtualized grids.
- Audit new AXAML for hard-coded presentation colors and duplicate local styles.
- Run the solution build and theme/settings tests after catalog or persistence changes.
