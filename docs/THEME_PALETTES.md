# Next Gen theme palettes

`src/UI/MissionPlanner.Themes` supplies palette variants for the Next Gen Avalonia app.
The app loads `MissionPlannerTheme` after the installed Semi and Ursa providers. Those
packages continue to supply the control templates. No local upstream project references
or copied control templates are needed.

## Resource layers

| Location in MissionPlanner.Themes | Responsibility | Shared by |
| --- | --- | --- |
| `Tokens/Base/Dark.axaml`, `Light.axaml` | Raw Semi color scales, neutral surfaces, text, shadows, status colors | All palettes of that brightness |
| `Tokens/Accent.axaml` | Maps palette inputs to Semi primary, secondary, information, link and focus roles; defines shared button foreground states | All palettes |
| `Themes/Dark/`, `Themes/Light/` | Maps semantic roles to Semi and Ursa control states | All palettes of that brightness |
| `Palettes/<Name>/` | Selects accent shades, link and selection text colors; composes the shared layers | One palette, with Dark and Light files |
| `MissionPlannerTheme.axaml` | Registers the composed dictionaries under named Avalonia variants | Application |

The former `Themes/DarkOrange` and `Themes/LightOrange` directories are now the shared
`Themes/Dark` and `Themes/Light` directories. The former full Orange token dictionaries
were split into shared base tokens and small palette mappings. Each palette file has
nine color inputs and three shared includes. A new palette does not copy any control
resource files, raw color scales, status definitions, or templates.

Primary, secondary, and information accents follow the selected palette. Success,
warning, danger, neutral surfaces, and explicit named-color choices (such as a Blue avatar)
retain their meaning. Instrument and operational colors are not recolored by this layer.

## Why compile-time composition matters

Semi's templates dynamically look up control keys, but most control keys are static aliases
to semantic brushes. Replacing a semantic brush after its aliases have resolved does not
retarget those aliases.

Each palette therefore uses `MergeResourceInclude` to compile its base tokens, semantic
mapping, and shared control resources into one dictionary scope. Nested control includes
use absolute `avares://MissionPlanner.Themes/...` URIs because the compiler flattens them
into the consuming palette. Do not replace these merges with ordinary `ResourceInclude`.
Only the final palette registration uses `ResourceInclude` in `ThemeDictionaries`.

Control definitions are maintained once per brightness. Compiled resource instances remain
separate per variant so two windows can use different palettes simultaneously. This is
intentional runtime isolation, not duplicated source maintenance. Switching variants selects
another complete dictionary; it does not mutate a global brush collection or reload styles.

`MissionPlannerTheme` adds small foreground/glyph adapters based on installed control themes.
It discovers the registered palette dictionary keys, so new palettes receive the same
adapters automatically. All accent palettes use white button text/icons normally and on
hover, and black while pressed. The shared accent layer defines these states, including
toolbar and dropdown buttons; individual palettes cannot reverse them. Disabled states
and success/warning/danger templates retain their existing behavior.

Map overlay buttons are an explicit exception: `MapIconButton` keeps a black foreground
over the map. Its shared style binds the template's `PART_ContentPresenter.Foreground`
back to `Button.Foreground`, so the icon inherits the button's chosen color in every
theme and interaction state. Setting only the button property does not override a
foreground assigned directly to the presenter by a control theme.
Both `GeoFenceMapView` and `MissionMapView` use this class for their four navigation
buttons. The latter supplies the maps in Flight Data and Flight Planner. Do not use
`ToolbarButton` on these overlays: its solid accent theme supplies different icon states.
`MapOverlayThemeTests` loads all three production views and checks their actual icons
through the complete theme catalog and normal, hover, pressed and disabled states.

## Adding a palette

1. Create `Palettes/<Name>/Dark.axaml` and `Light.axaml` using Orange or Purple as examples.
   Keep the three shared merge includes. Select colors from the appropriate base's named
   Semi scales, or supply explicit `Color` values for the nine inputs.
2. Define stable named variants in `MissionPlannerThemeVariants`, inheriting ordinary
   `ThemeVariant.Dark` and `ThemeVariant.Light`.
3. Register the two palette files in `MissionPlannerTheme.axaml` under those variants.
4. Add stable persisted IDs and display names to `AvaloniaThemeCatalog`. Settings already
   accept lowercase, hyphenated theme identifiers; no Core palette list is required.
5. Extend `PaletteThemeRenderingTests` with the new variants and expected colors. Check
   normal, hover, pressed, selected, disabled, focus and text contrast in both appearances.

The inputs are `PaletteNormalColor`, `PaletteHoverColor`, `PalettePressedColor`,
`PaletteDisabledColor`, `PaletteSubtleColor`, `PaletteSubtleHoverColor`,
`PaletteSubtlePressedColor`, `PaletteTextColor`, and
`PaletteSelectionTextColor`. `Tokens/Accent.axaml` maps these to semantic brushes.
Dark and Light need independently chosen shades; equal scale indices
do not imply equivalent contrast.

The selector offers Dark and Light appearances for Orange, Purple, Red, Pink, Violet,
Indigo, Yellow, Lime, Light Green, Light Blue, Teal, Cyan, Green, Grey, and AI Purple.
These are thirty palette variants, backed by the same shared control resources.
Each family uses its corresponding `Semi<Name><Shade>Color` tokens for normal, hover
and pressed fills. White/black button foregrounds are a consistent interaction convention,
not a per-fill contrast calculation. Link text and selection text still use palette-specific
contrast checks.

The top bar and Preferences share `ThemeMenuFlyout`. Default selects the normal system
appearance. Dark and Light are parent menus, each containing Blue (the built-in palette)
and the fifteen additional colors. A High contrast submenu keeps the existing Semi choices
available. Radio marks follow the selected theme across submenus and stay synchronized
when another selector or a settings reload changes it. The menu derives its choices from
the catalog and binds to the existing ViewModels, preserving live preview and persistence.

Orange IDs remain `dark-orange` and `light-orange`. All other palette IDs follow the
same pattern, including `dark-light-green`, `light-light-blue`, and `dark-ai-purple`.
Default, Light, Dark and the existing
Semi high-contrast choices retain their original providers. Aquatic, Desert, Dusk and
Night Sky are independent of this composition; adding normal palettes does not require
any high-contrast dictionaries or changes to those choices.

## Verification

`PaletteThemeRenderingTests` loads the real application styles. It resolves all shared
control aliases for inactive palettes before switching, compares status/neutral brushes
with their base appearances, checks link/selection contrast and button text/icon states, switches the complete catalog
in both directions, and verifies real Semi buttons and Ursa banners in two windows.
One window retains Purple while the other changes, including Orange at the same brightness.
`ThemeMenuTests` covers catalog completeness, nested keyboard selection, flyout dismissal,
radio exclusivity, and two-way selection synchronization. Set `MISSIONPLANNER_VISUAL_TEST_OUTPUT`
to export the thirty control previews.

Run rendering fixtures in separate processes from fixtures using a different headless
drawing configuration. For example, after building the UI test project:

```powershell
dotnet src/Tests/MissionPlanner.AvaloniaUI.Tests/bin/Debug/net10.0-windows10.0.19041.0/MissionPlanner.AvaloniaUI.Tests.dll -class '*PaletteThemeRenderingTests' -noLogo
```

These checks cover compiled resources and headless rendering, not native window chrome,
OS appearance notifications, assistive technology, or connected-vehicle behavior.
Upstream resource licenses remain in `MissionPlanner.Themes/Licenses`.

Initial refactor verified on 16 October 2026: the theme library built with zero warnings/errors; the full
UI test project built with zero errors and 56 existing warnings. Nine tests passed across
the palette rendering, catalog, readiness rendering and compass information document
fixtures. The palette fixture checks over 700 control aliases per variant, all eleven
catalog choices in both directions, and simultaneous windows with independent palettes.

Reviewed previews: [Dark Orange](images/theme-palettes/palette-dark-orange.png),
[Light Orange](images/theme-palettes/palette-light-orange.png),
[Dark Purple](images/theme-palettes/palette-dark-purple.png), and
[Light Purple](images/theme-palettes/palette-light-purple.png).

The expanded palette set was verified on 16 October 2026 with a successful full UI test
project build (zero errors, 56 existing warnings) and 23 passing tests across the catalog,
palette rendering, and diagnostic-menu/layout fixtures. All thirty palettes rendered,
all thirty-seven selector choices switched in both directions, and palette foregrounds
passed the existing 4.5:1 text contrast checks. The top-bar selector is wider to accommodate
the longer family names. Its tests now account for the existing Parameters Editor action
and skip menu separators when exercising actions.

Additional reviewed previews: [Light Yellow](images/theme-palettes/palette-light-yellow.png),
[Light Green](images/theme-palettes/palette-light-green.png),
[Dark Grey](images/theme-palettes/palette-dark-grey.png), and
[Dark AI Purple](images/theme-palettes/palette-dark-ai-purple.png).
