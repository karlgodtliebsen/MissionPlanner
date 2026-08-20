# Application theming

MissionPlanner uses an application-level semantic theme system. Views describe the
purpose of a color and never choose a concrete palette or branch on light versus dark.

## Semantic resource rule

Use dynamic semantic resources in Views and application styles:

```xml
<Border BackgroundColor="{DynamicResource Surface}">
    <Label TextColor="{DynamicResource OnSurface}" />
</Border>
```

Do not use `AppThemeBinding` for application colors, concrete theme names, or parallel
keys such as `SurfaceDark`. The authoritative required color roles are defined by
`ThemeResourceKeys`. Operational state uses `Success`, `Warning`, `Info`, and `Error`;
`Primary` remains the normal application/action accent.

When a control property requires a brush, use a semantic brush such as
`PrimaryBrush`. These brushes bind dynamically to their corresponding color role and
therefore update with the active palette.

## Resource flow

The persisted `ThemeId` is passed to the singleton `IThemeManager`. The manager resolves
the selection through `IThemeCatalog`, loads and validates the complete concrete
palette, and atomically overwrites semantic colors in the named `AppColors` dictionary.
Styles, UraniumUI overrides, and Views observe those values through `DynamicResource`.
The application and Shell are not recreated.

`ThemeManager` is the only runtime theme authority. ViewModels may request application
or preview through it, but must not assign `Application.UserAppTheme` themselves.

## Built-in selections

- `system` — selection policy;
- `mission-light` — Mission Light palette;
- `mission-dark` — Mission Dark palette;
- `mission-blue` — Mission Blue palette.

Theme IDs are stable strings rather than a serialized enum. This allows an installed
extension theme to use a future identifier without changing Core or invalidating an
existing settings document. Core validates the identifier's structure; the UI catalog
determines availability.

## System mode

System is not a palette. It resolves OS Light to Mission Light and OS Dark to Mission
Dark. While `system` is selected, requested-appearance changes reapply the corresponding
palette and the native `UserAppTheme` remains unspecified. An explicit theme such as
Mission Blue is not replaced when the OS appearance changes.

## Base appearance

Every concrete `ThemeDescriptor` declares a `ThemeBaseAppearance` of Light or Dark.
This value is metadata for MAUI native controls and UraniumUI fallback behavior only.
For example, Mission Blue has a Light base appearance, but all MissionPlanner colors
still come from `MissionBlue.xaml`; it is not translated into Mission Light.

## Adding a theme

1. Add a ResourceDictionary under `Resources/Themes` containing every color in
   `ThemeResourceKeys.RequiredColorKeys`. Use the same unsuffixed semantic keys as the
   other palettes.
2. Add one concrete `ThemeDescriptor` to `ThemeCatalog` with a stable lowercase ID,
   friendly display name, base appearance, and resource path. Add a `ThemeOption` when
   users should be able to select it.
3. Run `ThemeContractTests`, `ThemeManagerTests`, and `ThemeArchitectureTests`.
4. Verify runtime switching on the supported platform heads.

No ordinary View or application style changes are required when adding a theme.

## Validation and failure behavior

Before changing `AppColors`, the manager loads the entire palette and verifies that
every required resource exists and is a MAUI `Color`. Unknown, missing, or malformed
themes fail without partially replacing the active dictionary. A successful operation
raises one `ThemeChanged` event after all semantic values and native base appearance
have been applied on the UI dispatcher.

Schema-four settings are migrated to schema five by mapping the old System, Light, and
Dark names to stable IDs. That migration remains supported for existing installations.
