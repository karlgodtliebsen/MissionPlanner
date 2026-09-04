# Application theming

MissionPlanner uses Avalonia theme variants, Semi, Ursa's Semi integration, and shared
semantic styles. `App.axaml` is the composition root for global resources.

## Theme composition

```xml
<Application RequestedThemeVariant="Default">
    <Application.Styles>
        <semi:SemiTheme Locale="en-US" />
        <semi:UrsaSemiTheme Locale="en-US" />
        <semi:SemiPopupAnimations />
        <materialIcons:MaterialIconStyles />
        <StyleInclude Source="/Resources/Styles/SharedStyles.axaml" />
        <StyleInclude Source="/Resources/Styles/VirtualizedItemsGridStyles.axaml" />
    </Application.Styles>
</Application>
```

Do not create a second palette manager or copy global theme resources into views.

## Persisted selection

`AvaloniaThemeCatalog` supplies `TopBarView` and maps the persisted Planner setting to a
`ThemeVariant`. Stable identifiers are `system`, `light`, `dark`, `aquatic`, `desert`, `dusk`,
and `night-sky`. Legacy identifiers are accepted only for settings compatibility. New code
stores the Avalonia identifiers.

Applying a theme assigns `Application.Current.RequestedThemeVariant`. Feature ViewModels must
not assign that property or translate stored theme names themselves.

## View styling

Prefer semantic dynamic resources and reusable classes:

```xml
<Border Background="{DynamicResource Surface}" Classes="Rounded Elevation1">
    <TextBlock Text="{Binding Warning}" Classes="Body Warning" />
</Border>
```

- Use Avalonia brush properties such as `Background` and `Foreground`.
- Use `Classes` for shared states such as `Warning`; append to existing classes.
- Use `DynamicResource` when a value must respond to theme changes.
- Put reusable selectors in `Resources/Styles/SharedStyles.axaml`.
- Put specialized templates beside their control, as with `VirtualizedItemsGrid`.
- Avoid hard-coded colors except for intrinsic media or documented domain visualization.

Indeterminate activity indicators use Ursa's ring template:

```xml
<ProgressBar
    IsIndeterminate="True"
    Theme="{DynamicResource ProgressRing}"
    IsVisible="{Binding IsBusy}" />
```

## Verification

When adding or changing a theme:

1. Update `AvaloniaThemeCatalog` and keep the persisted identifier stable.
2. Expose it through `TopBarViewModel` and Planner settings.
3. Verify switching does not recreate the shell.
4. Exercise Ursa dialogs, `NavMenu`, title-bar controls, maps, warning states, and the
   virtualized grid in light and dark variants.
5. Build the solution and run theme and settings tests.
