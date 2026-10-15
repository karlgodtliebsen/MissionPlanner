# Mission Orange appearance

Implemented 16 October 2026 for the [six-task brief](tasks/20261015/MissionPlanner-NextGen-Mission-Orange-Codex-Tasks.md).

In **Preferences > Units and appearance**, select **Color scheme (live preview)**:
Original or Mission Orange. Save preferences to retain the choice. Theme remains an
independent System / Light / Dark selector, with the four existing Semi high-contrast
variants also available. Selecting a high-contrast variant temporarily bypasses Mission
Orange; returning to Light, Dark, or System restores the chosen scheme.

## Installed source audit

Versions come from `src/Directory.Packages.props`; source revisions were verified against
the installed NuGet `.nuspec` repository metadata. Neither packages nor providers changed.

| Package | Version |
| --- | --- |
| Avalonia, Desktop, Headless and core controls | 12.1.3 |
| Semi.Avalonia, DataGrid and ColorPicker extensions | 12.1.0.1 |
| Semi.Avalonia.AvaloniaEdit | 12.0.0 |
| Semi.Avalonia.Dock | 12.0.0.2 |
| Irihi.Ursa and Irihi.Ursa.Themes.Semi | 2.2.0 |
| Material.Icons.Avalonia | 3.0.2 |
| LiveMarkdown.Avalonia | 2.4.3 |

Matching sources:

- [Semi dc75ad45](https://github.com/irihitech/Semi.Avalonia/tree/dc75ad45e3ab2c9cf99eefc23352a6767aeb47c6/src):
  `Semi.Avalonia/Tokens/Palette/{Dark,Light}.axaml`, `Tokens/_index.axaml`,
  `Themes/{Dark,Light}/*.axaml`, `Controls/*.axaml`, and `Semi.Avalonia.DataGrid/{Dark,Light}.axaml`.
- [Ursa 00fef728](https://github.com/irihitech/Ursa.Avalonia/tree/00fef728ce87cc8cbc9085f3162867daa1d57490/src/Ursa.Themes.Semi):
  `UrsaSemiTheme.axaml`, `Themes/{Dark,Light}/*.axaml`, `Controls/IconButton.axaml`,
  `IconDropDownButton.axaml`, and `NavMenu.axaml`.

`App.axaml` still installs SemiTheme, DataGridSemiTheme, UrsaSemiTheme, popup animations,
MaterialIconStyles, and the existing shared styles. The latter retain all toolbar widths,
padding, corner radii, icon dimensions, and templates.

## Resource integration

The existing `Views/Navigation/AvaloniaThemeCatalog` owns both selections.
`MissionOrangePalette` is its resource-overlay implementation, not another theme service.
It installs one removable application dictionary and a small style collection, after the
provider resources. The application dictionary takes precedence over provider resources.
Removal exposes the original provider values again; provider brushes are never mutated.

The two `Resources/Themes/MissionOrange{Dark,Light}.axaml` files hold all palette brushes
and the audited control aliases. These are **brushes**, not `Color` resources. Alias entries
resolve to brushes in the same application-owned palette. They are replaced together on
scheme or effective appearance changes; the installed templates consume them dynamically.
Neutral surfaces and the raw `SemiBlue*`, information, success, warning, danger, secondary,
and tertiary families are not replaced.

| Consumer | Installed resource chain | App treatment |
| --- | --- | --- |
| Semi solid button | Dynamic `ButtonSolidPrimaryBackground` → static `SemiColorPrimary` | Override both semantic brush and control alias |
| Ursa solid icon/dropdown | BaseSolidIconButton → same ButtonSolid aliases; separate background Border and content presenter | Same aliases; derived theme adds primary-only foreground rules |
| Ordinary buttons, links, tabs | Primary foreground / link aliases | Readable accent-text or link brush, separate from solid fill |
| Check/radio glyphs | Primary background aliases; white glyph | Dark glyph normally, white when pressed in dark mode |
| Toggle buttons | Checked state uses ButtonSolid aliases | Primary checked foreground styles; semantic classes excluded |
| Toggle switches | Installed checked state uses **success**, unchecked Accent uses danger | Preserve green/red operational meaning; do not globally recolor success |
| Slider/progress | Track, thumb and indicator aliases | Orange aliases; existing dimensions retained |
| Text input | Focus alias; hardcoded upstream blue text-selection brush | Focus brush plus opaque scheme-specific text selection |
| Lists, navigation, grid | ListBoxItem / DataGridRow selection aliases | Brown or light-orange fills; leading selection marker on list items; grid focus brush |
| Focus adorner | Static primary-light alias | Explicit high-contrast focus brush rather than the brown fill |

The original shared `ButtonSolidForeground` remains untouched. Derived SolidButton,
SolidIconButton and SolidIconDropDownButton themes inherit installed templates and add
foreground setters excluding Secondary, Tertiary, Success, Warning, Danger, Colorful and
disabled controls. They target ContentControl because existing app styles also apply
SolidIconButton to regular Buttons and dropdowns. No library template is copied.
The installed button templates have no brush interpolation requiring a new transition;
foreground and fill change together. Popup animations remain installed.

## Palette and contrast

Dark normal/hover/pressed fills are exactly `#D27917`, `#FC962A`, `#AB6314`.
Selections are opaque `#442707` / `#663A0B`. Focus and normal links use `#FC962A`.
Disabled primary is muted `#665442`. Hover/visited links use `#FFB96F`, active links
`#FFD3A3`. Accent text uses `#FFB96F` so it also passes on the stronger brown selection.

Light fills are independently chosen: `#984B00`, `#854000`, `#713500`, with white
foregrounds and pale selections `#FFF0DF`, `#FFE1BD`, `#FFD3A3`. Accent/link text
uses `#853E00`; hover and active links are darker. Disabled primary is `#C9B7A5`.

| Pair | Dark contrast | Light contrast |
| --- | ---: | ---: |
| Normal solid primary text | 5.57:1 | 6.28:1 |
| Hover solid primary text | 8.19:1 | 7.69:1 |
| Pressed solid primary text | 4.64:1 | 9.50:1 |
| Accent text on strong selection | 5.71:1 | 6.24:1 |

Tests calculate contrast from loaded brushes and rendered content-presenter states,
including Ursa and checked toggles. Focus is checked against all three neutral surfaces
at 3:1. Disabled controls retain the provider's disabled presentation and are not treated
as normal interactive text for contrast assertions. Existing warning/status styling is
intentionally retained; this is not a contrast certification of the whole third-party library.

## Shared views and custom rendering audit

Parameters, Radio Setup, Flight Modes Setup, Motor Test, Firmware Installation, Inspector,
Messages and Preflight use shared toolbar styles, ordinary provider controls, semantic
status resources and SectionCard surfaces. They inherit the palette without layout edits.
Ursa navigation consumes ListBoxItem selection aliases. Topbar and statusbar neutral
surfaces stay unchanged; primary actions receive the scheme. Information banners and
DialogInfoIconForeground keep their provider information colors.

No hardcoded decorative blue accent was found in application XAML. The hardcoded blue
text-selection brush was in the installed Semi TextBox resources and is explicitly overridden.
Operational drawing is intentionally unchanged: HUD attitude/flight cues, radio channel
colors/meters/history, OSD preview, proximity radar, FFT curves, Mapsui and telemetry plots.
These renderers do not cache a theme-primary brush. The translucent radio-meter label
background and Introduction markdown background are neutral, not primary accents.

`InformationDocumentView` does cache renderer resource values. It now subscribes to the
catalog's palette-change event while attached and unsubscribes when detached, updating
its quote brush even when the light/dark variant itself has not changed. Its existing
theme-variant handler and safe Markdown/link behavior remain unchanged.

## Persistence and lifecycle

`PlannerAppearanceSettings.ColorSchemeId` defaults to `original`. It is an additive field
in the existing JSON schema: omitted old fields deserialize to Original without changing
ThemeId. The settings service validates the two allowed IDs. Import/export, reset and Save
use the existing infrastructure. Topbar theme-only saves use `with`, preserving the scheme.
Preferences selectors preview immediately and remain synchronized across active instances.

`App.Initialize` applies the loaded settings after loading styles and before constructing
the first window. Platform startup already initializes the settings service. The catalog
listens to `Application.ActualThemeVariantChanged`, leaving RequestedThemeVariant at Default
in System mode. Native light/dark changes therefore rebuild the appropriate palette without
changing the scheme. All four named Semi accessibility variants bypass the overlay entirely.

## Verification and previews

The Windows desktop project builds with dependencies (`--no-restore -m:1 -nr:false`), with
32 existing warnings and zero errors. The affected UI test project builds. All 18 targeted
tests pass (17 rendering/theme/layout cases and the separate document case). Targeted tests
cover preference round-trip, missing-field compatibility, invalid scheme rejection, startup
before the first window, existing Semi/Ursa controls, an open secondary window, an open
flyout, cached document brushes, Original restoration, System selection, all four
high-contrast variants, interaction contrast, topbar/menu behavior, firmware scrolling,
Quick Editor layouts, and Preflight at 360/700-pixel widths in both schemes and appearances.
The topbar tests were updated to accommodate its existing separator and seventh action.

Reproduce the visual tests after building:

```powershell
$env:MISSIONPLANNER_VISUAL_TEST_OUTPUT = 'C:\Projects\MissionPlanner\artifacts\theme-audit\screenshots'
dotnet src/Tests/MissionPlanner.AvaloniaUI.Tests/bin/Debug/net10.0-windows10.0.19041.0/MissionPlanner.AvaloniaUI.Tests.dll -class '*MissionOrange*' -class '*ReadinessViewTests' -class '*AvaloniaThemeTests' -class '*DiagnosticMenuAndFirmwareLayoutTests' -class '*QuickEditorLayoutTests' -noLogo
```

Run CompassInformationDocumentTests in a separate process: its non-Skia headless renderer
cannot share cached glyphs with the screenshot fixtures. This is a test-host limitation.

| Preview | Original | Mission Orange |
| --- | --- | --- |
| Shared topbar/panel and controls, dark | [Before](tasks/20261015/mission-orange-previews/controls-original-Dark.png) | [After](tasks/20261015/mission-orange-previews/controls-orange-Dark.png) |
| Shared topbar/panel and controls, light | [Before](tasks/20261015/mission-orange-previews/controls-original-Light.png) | [After](tasks/20261015/mission-orange-previews/controls-orange-Light.png) |
| Preflight, dark | [Before](tasks/20261015/mission-orange-previews/readiness-original-Dark-700.png) | [After](tasks/20261015/mission-orange-previews/readiness-mission-orange-Dark-700.png) |
| Preflight, light | [Before](tasks/20261015/mission-orange-previews/readiness-original-Light-700.png) | [After](tasks/20261015/mission-orange-previews/readiness-mission-orange-Light-700.png) |

These are offline, deterministic production-control previews, not connected-flight evidence.
Native Windows/macOS/Linux OS appearance-change notifications, native window decorations,
OS-native dialogs, assistive technologies, and every third-party control permutation have
not been manually exercised. System selection and effective light/dark transitions are
covered separately; an actual OS setting toggle remains a platform smoke check. The full
multi-platform solution and hardware tests were not run for this UI-only change.

To adjust colors, edit the semantic brushes at the start of the two palette dictionaries;
leave the control alias section pointing to those brushes. On a Semi/Ursa upgrade, re-audit
the pinned resource chains and rerun previews, runtime restoration and contrast tests.
