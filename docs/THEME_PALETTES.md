# Next Gen theme palettes

`src/UI/MissionPlanner.Themes` supplies palette variants for the Next Gen Avalonia app.
The app loads `MissionPlannerTheme` after the installed Semi and Ursa providers. Those
packages continue to supply the control templates. No local upstream project references
or copied control templates are needed.

## Resource layers

| Location in MissionPlanner.Themes | Responsibility | Shared by |
| --- | --- | --- |
| `Tokens/Base/Dark.axaml`, `Light.axaml` | Raw Semi color scales, neutral surfaces, text, shadows, status colors | All palettes of that brightness |
| `Tokens/Accent.axaml` | Maps palette inputs to Semi primary, secondary, information, link and focus roles | All palettes |
| `Themes/Dark/`, `Themes/Light/` | Maps semantic roles to Semi and Ursa control states | All palettes of that brightness |
| `Palettes/Orange/`, `Palettes/Purple/` | Selects accent shades and readable foreground colors; composes the shared layers | One palette, with Dark and Light files |
| `MissionPlannerTheme.axaml` | Registers the composed dictionaries under named Avalonia variants | Application |

The former `Themes/DarkOrange` and `Themes/LightOrange` directories are now the shared
`Themes/Dark` and `Themes/Light` directories. The former full Orange token dictionaries
were split into shared base tokens and small palette mappings. Each palette file has
twelve color inputs and three shared includes. A new palette does not copy any control
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
adapters automatically. These adapters preserve readable on-accent foregrounds without
changing the success/warning/danger templates.

## Adding a palette

1. Create `Palettes/<Name>/Dark.axaml` and `Light.axaml` using Orange or Purple as examples.
   Keep the three shared merge includes. Select colors from the appropriate base's named
   Semi scales, or supply explicit `Color` values for the twelve inputs.
2. Define stable named variants in `MissionPlannerThemeVariants`, inheriting ordinary
   `ThemeVariant.Dark` and `ThemeVariant.Light`.
3. Register the two palette files in `MissionPlannerTheme.axaml` under those variants.
4. Add stable persisted IDs and display names to `AvaloniaThemeCatalog`. Settings already
   accept lowercase, hyphenated theme identifiers; no Core palette list is required.
5. Extend `PaletteThemeRenderingTests` with the new variants and expected colors. Check
   normal, hover, pressed, selected, disabled, focus and text contrast in both appearances.

The inputs are `PaletteNormalColor`, `PaletteHoverColor`, `PalettePressedColor`,
`PaletteDisabledColor`, `PaletteSubtleColor`, `PaletteSubtleHoverColor`,
`PaletteSubtlePressedColor`, `PaletteTextColor`, `PaletteOnAccentColor`,
`PaletteOnAccentHoverColor`, `PaletteOnAccentPressedColor`, and
`PaletteSelectionTextColor`. `Tokens/Accent.axaml` maps these to semantic brushes.
Dark and Light need independently chosen shades and foregrounds; equal scale indices
do not imply equivalent contrast.

Orange IDs remain `dark-orange` and `light-orange`. Purple provides a second working
example using `dark-purple` and `light-purple`. Default, Light, Dark and the existing
Semi high-contrast choices retain their original providers. Aquatic, Desert, Dusk and
Night Sky are independent of this composition; adding normal palettes does not require
any high-contrast dictionaries or changes to those choices.

## Verification

`PaletteThemeRenderingTests` loads the real application styles. It resolves all shared
control aliases for inactive palettes before switching, compares status/neutral brushes
with their base appearances, checks accent text contrast, switches the complete catalog
in both directions, and verifies real Semi buttons and Ursa banners in two windows.
One window retains Purple while the other changes, including Orange at the same brightness.
Set `MISSIONPLANNER_VISUAL_TEST_OUTPUT` to export the four control previews.

Run rendering fixtures in separate processes from fixtures using a different headless
drawing configuration. For example, after building the UI test project:

```powershell
dotnet src/Tests/MissionPlanner.AvaloniaUI.Tests/bin/Debug/net10.0-windows10.0.19041.0/MissionPlanner.AvaloniaUI.Tests.dll -class '*PaletteThemeRenderingTests' -noLogo
```

These checks cover compiled resources and headless rendering, not native window chrome,
OS appearance notifications, assistive technology, or connected-vehicle behavior.
Upstream resource licenses remain in `MissionPlanner.Themes/Licenses`.

Verified on 16 October 2026: the theme library built with zero warnings/errors; the full
UI test project built with zero errors and 56 existing warnings. Nine tests passed across
the palette rendering, catalog, readiness rendering and compass information document
fixtures. The palette fixture checks over 700 control aliases per variant, all eleven
catalog choices in both directions, and simultaneous windows with independent palettes.

Reviewed previews: [Dark Orange](images/theme-palettes/palette-dark-orange.png),
[Light Orange](images/theme-palettes/palette-light-orange.png),
[Dark Purple](images/theme-palettes/palette-dark-purple.png), and
[Light Purple](images/theme-palettes/palette-light-purple.png).
