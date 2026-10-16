# Orange theme resource composition

Updated 16 October 2026 after the full-control resource review.

## What changed

Dark Orange and Light Orange now load `Themes/DarkOrange/_index.axaml` and `Themes/LightOrange/_index.axaml` directly. Each index composes one complete palette and 83 control resource dictionaries: the user's Ursa copies, Semi control resources, and the Semi DataGrid resources. They replace the earlier small selection of control aliases. Installed Semi/Ursa templates remain unchanged.

The Preferences information banner was blue because its Ursa control aliases were never composed into the orange variant. Changing `SemiColorPrimary` alone does not rebind an upstream `StaticResource` alias. The current composition keeps the palette and control aliases together using compile-time `MergeResourceInclude`, so they resolve within the same named variant regardless of which theme is active when resources first load.

Aquatic and the other Semi named themes use separate HighContrast control dictionaries. Orange deliberately uses ordinary Dark/Light control resources with the orange mappings below. Neutral panels remain neutral; selecting Orange does not turn every background orange.

## Files and lookup chain

| Location in MissionPlanner.Themes | Responsibility |
| --- | --- |
| `MissionPlannerThemeVariants.cs` | Stable variant objects; ordinary Dark/Light fallbacks |
| `MissionPlannerTheme.axaml` | Registers the exact variants and their folder indexes |
| `Tokens/Palette/DarkOrange.axaml`, `LightOrange.axaml` | Complete Semi color scales and semantic brushes |
| `Themes/DarkOrange/`, `LightOrange/` | Ursa control resource aliases |
| Each variant's `Semi/` directory | Semi and DataGrid control aliases |
| Each variant's `_index.axaml` | Explicit compile-time composition, palette first |
| `MissionPlannerTheme.axaml.cs` | Scoped foreground adapters based on installed templates |
| `ThemeItem.cs` | Existing theme catalog, moved here by the user |
| `Licenses/` | Licenses retained for the adapted upstream resource files |

For example: the Ursa Banner template resolves `BannerInformationBackground`, which now resolves the variant-local `SemiColorPrimaryLight`; its border and information glyph resolve `SemiColorPrimary`. Loading indicators, notifications, navigation, badges, tabs, calendars, inputs, menus and selections use the same semantic mapping approach. The theme provider is loaded once after Semi and Ursa; selection never swaps global palettes.

Semi and Ursa define some identical notification keys. These are defined once, in Ursa's `NotificationShared.axaml`, matching the application's existing provider precedence. The Semi notification dictionary retains its non-overlapping keys. The complete merged sets contain 1,361/1,360 resources and 757/756 static control aliases for Dark/Light Orange respectively.

## Palette choices

Primary, secondary and information families are orange, as requested. Success stays green, warning retains its warning palette, and danger stays red. Neutral surfaces, disabled text, explicit named-color choices (for example Blue avatars or Purple badges), and operational instrument colors retain their meanings. No global substitution of every blue/green/red token is performed.

The full palettes are adapted from the user's local Semi `Tokens/Palette/Dark.axaml` and `Light.axaml`. Active accent roles reference their `SemiOrange*Color` scales rather than independent literal colors:

| Role | Dark Orange | Light Orange |
| --- | --- | --- |
| Normal primary/secondary/information | Orange3, #D56F0F | Orange7, #A84A00 |
| Hover | Orange4, #FF9214 | Orange8, #7E3100 |
| Pressed | Orange2, #AA500A | Orange9, #541D00 |
| Subtle selection | Orange0, #551F03 | Orange0, #FFF8EA |
| Strong selection | Orange1, #803506 | Orange1, #FEEECC |
| Focus / link / text accent | Orange6, #FFC772 | Orange8, #7E3100 |

These source-palette selections supersede the earlier screenshot-sampled primary hex values. Dark solid buttons use dark text normally/on hover and white when pressed. Light solid buttons use white. Scoped adapters now cover both primary and secondary buttons while leaving success/warning/danger foregrounds unchanged. Checkbox/radio/toggle theme lookups remain dynamic across switching.

Badge primary/secondary fills and selection indicators no longer use raw blue scales. TextBox and SelectableTextBlock selection colors and disabled slider borders no longer use literal blue. Selected calendar text uses the appropriate on-primary brush. Existing pressed borders use the focus brush where needed for contrast; control geometry is unchanged.

Preferences cards still resolve `SectionCard` to `SemiColorBackground2`, #35363C in Dark Orange and the light neutral in Light Orange. This is intentional. `InformationDocumentView` follows the variant's dark/light ancestry and refreshes cached resource brushes on theme changes.

## Sources and dependencies

Resources were read from `C:/Projects/Semi.Avalonia/src/Semi.Avalonia/Themes`, its `Tokens/Palette` directory, `Semi.Avalonia.DataGrid`, and `C:/Projects/Ursa.Avalonia/src/Ursa.Themes.Semi/Themes`. Only color/resource dictionaries were adapted; no upstream control templates or source-project references were added. Their licenses are included in the theme project.

Installed dependencies remain Avalonia 12.1.3, Semi.Avalonia/DataGrid/ColorPicker 12.1.0.1, Semi.Avalonia.AvaloniaEdit 12.0.0, Semi.Avalonia.Dock 12.0.0.2, and Irihi.Ursa/Irihi.Ursa.Themes.Semi 2.2.0. No dependency versions were upgraded. Compatibility with installed templates is checked by loading every control alias and rendering production controls.

## Validation and review

`OrangeThemeRenderingTests` checks all static control aliases against their semantic targets in both variants, including resolving Light Orange while Dark Orange is active. It exercises all nine catalog selections in both directions, multiple windows, flyouts/tooltips, simultaneous different variants, rendered primary foreground/border contrast, status preservation, input focus contrast, document brush refresh, and the actual Preferences page including its banner border/background/icon.

Other focused checks cover settings persistence, Preflight, Quick Editor layout, topbar commands and document rendering. Use the built xUnit executable with `-class '*OrangeThemeRenderingTests' -noLogo`; render fixtures should run in separate processes because existing fixtures mix Skia and headless glyph caches. Set `MISSIONPLANNER_VISUAL_TEST_OUTPUT` to save screenshots.

Current screenshots are in [orange-control-previews](tasks/20261015/orange-control-previews/). Native OS appearance notifications, platform window chrome, assistive technology and connected-vehicle flows are not covered by headless tests. There is no claim of application-wide accessibility certification.

The focused validation run on 16 October 2026 passed all 19 tests across six fixtures. The complete UI test project built with zero errors (46 existing warnings). Review the actual Preferences page in [Dark Orange](tasks/20261015/orange-control-previews/preferences-dark-orange.png) and [Light Orange](tasks/20261015/orange-control-previews/preferences-light-orange.png), and the [Dark Orange control states](tasks/20261015/orange-control-previews/controls-dark-orange.png) and [Light Orange control states](tasks/20261015/orange-control-previews/controls-light-orange.png).

The Desktop application also built successfully into `artifacts/orange-complete/desktop` with zero errors (42 existing warnings). The normal output directory was locked by the running MissionPlanner instance, which was left open. Close it, rebuild, and relaunch to use the updated resources from the normal output directory.
