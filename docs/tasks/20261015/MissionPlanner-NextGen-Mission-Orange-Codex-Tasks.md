# MissionPlanner Next Gen — Mission Orange theme

Updated: 16 October 2026

## Objective

Add a selectable **Mission Orange** color scheme to the existing Avalonia Semi/Ursa UI, using the orange palette sampled from Karl's screenshot of the VLC design video. Retain the existing theme providers and shared control styling.

This document consolidates all six tasks and supersedes the initial orange palette proposal. The implementation and verification record is linked below; no GitHub issues were created.

## Implementation status — 16 October 2026

All six implementation tasks are delivered. See [Mission Orange implementation and source audit](../../MISSION_ORANGE.md)
for installed versions, resource chains, palette/contrast decisions, persistence, and before/after screenshots.
The checklist records implementation and targeted headless verification, not a certification of every platform
or third-party control. Native OS appearance-change notifications and native dialogs remain manual platform
smoke checks; operational colors and existing provider status styling are deliberately preserved.

## Evidence and scope

The upstream Semi and Ursa sources were inspected. MissionPlanner's current repository and installed package versions were not inspected during preparation; Task 1 must establish these before implementation.

Reference video: https://www.youtube.com/watch?v=ERFeOgqk324&t=725s

The video could not be retrieved directly. Palette values were sampled from flat interior regions of the supplied screenshot, named `dbef544c-1c11-463c-a715-92ae10dc046d.png`. They are screenshot pixel values, not verified original design tokens. Video compression and capture processing may affect them. The role assignments below are proposed interpretations; the screenshot does not label the swatches.

Existing App.axaml providers:

```xml
<semi:SemiTheme Locale="en-US" />
<semi:UrsaSemiTheme Locale="en-US" />
```

Keep both providers. Locale controls localization, not the accent color.

## Source map

Use the matching source versions for the application's installed packages. These links point to upstream main and may change.

| Source | Purpose |
| --- | --- |
| [Semi dark palette](https://github.com/irihitech/Semi.Avalonia/blob/main/src/Semi.Avalonia/Tokens/Palette/Dark.axaml) | Raw colors and semantic brushes for dark mode |
| [Semi light palette](https://github.com/irihitech/Semi.Avalonia/blob/main/src/Semi.Avalonia/Tokens/Palette/Light.axaml) | Corresponding light-mode definitions |
| [Semi token index](https://github.com/irihitech/Semi.Avalonia/blob/main/src/Semi.Avalonia/Tokens/_index.axaml) | Theme-specific palette selection |
| [Semi theme entry point](https://github.com/irihitech/Semi.Avalonia/blob/main/src/Semi.Avalonia/SemiTheme.axaml) | Resource and style composition |
| [Semi dark button resources](https://github.com/irihitech/Semi.Avalonia/blob/main/src/Semi.Avalonia/Themes/Dark/Button.axaml) | Semantic-to-control brush aliases |
| [Semi button templates](https://github.com/irihitech/Semi.Avalonia/blob/main/src/Semi.Avalonia/Controls/Button.axaml) | Button classes, templates and interaction states |
| [Ursa theme entry point](https://github.com/irihitech/Ursa.Avalonia/blob/main/src/Ursa.Themes.Semi/UrsaSemiTheme.axaml) | Ursa theme resource composition |
| [Ursa Semi resources](https://github.com/irihitech/Ursa.Avalonia/tree/main/src/Ursa.Themes.Semi) | Ursa-specific controls and styles |

### Resource constraints

- `SemiColorPrimary` and related `SemiColor...` entries are brushes, despite their names. Entries such as `SemiOrange5Color` are Color values. Preserve expected resource types.
- Override primary-action semantics rather than globally replacing `SemiBlue*`: informational messages also use the blue palette.
- Trace complete resource chains. Semi uses StaticResource aliases from semantic brushes to control-specific resources. Verify actual rendered results at startup and after switching; do not assume overriding one token updates every control.
- Use application-owned resource dictionaries and narrowly scoped styles. Avoid forking the theme libraries or copying complete control templates unless a demonstrated limitation requires it.
- Preserve existing spacing, typography, layout, button dimensions and behavior.

## Palette specification

### Sampled swatches and intended dark-mode roles

| Screenshot position | Color | Intended role |
| --- | --- | --- |
| Large left | `#D27917` | Primary action background |
| Upper middle | `#FC962A` | Hover, focus indicator and accent text on dark surfaces |
| Lower middle | `#AB6314` | Pressed primary action background |
| Upper right | `#663A0B` | Strong selection background |
| Lower right | `#442707` | Subtle selection background |

Retain Semi's neutral dark surfaces initially:

| Role | Color |
| --- | --- |
| Main background | `#16161A` |
| Panels/navigation | `#232429` |
| Raised surfaces | `#35363C` |

### Proposed dark-mode brush mappings

| Resource key | Color |
| --- | --- |
| `SemiColorPrimary` | `#D27917` |
| `SemiColorPrimaryPointerover` | `#FC962A` |
| `SemiColorPrimaryActive` | `#AB6314` |
| `SemiColorPrimaryLight` | `#442707` |
| `SemiColorPrimaryLightPointerover` | `#663A0B` |
| `SemiColorPrimaryLightActive` | `#663A0B` |
| `SemiColorFocusBorder` | `#FC962A` |
| `SemiColorLink` | `#FC962A` |

Treat the two brown background brushes as opaque initially. Applying additional opacity would change the sampled appearance. The shared brown hover/active value is intentional as a starting point; use another indicator where pressed/selected states need differentiation.

Also define and validate `SemiColorPrimaryDisabled`, `SemiColorLinkPointerover`, `SemiColorLinkActive` and `SemiColorLinkVisited`. These values are not prescribed by the screenshot. Disabled resources should be muted and distinct from selected states. Link interaction states must remain readable; do not automatically reuse the darker pressed-button color for text on dark surfaces.

Light mode requires independently validated orange values. Do not apply dark-mode brown surfaces indiscriminately to light mode.

### Foreground and contrast requirements

Calculated contrast ratios for opaque sampled backgrounds:

| Background | White foreground | Foreground #16161A |
| --- | --- | --- |
| Primary #D27917 | 3.24:1 | 5.57:1 |
| Hover #FC962A | 2.20:1 | 8.19:1 |
| Pressed #AB6314 | 4.64:1 | 3.89:1 |

For solid primary controls retaining these exact colors, use dark text/icons (`#16161A`) in normal and hover states and white in the pressed state. Define application-owned foreground-on-accent resources for these states. Verify that animation transitions do not create prolonged unreadable combinations.

Scope these foreground overrides to primary controls. Semi's `ButtonSolidForeground` is shared across multiple semantic button types; changing it globally can affect success, warning and danger controls. The inspected source also includes `ButtonSolidPrimaryPressedForeground`; verify its use in the installed version.

Target at least 4.5:1 for normal text and 3:1 for meaningful control boundaries and focus indicators against adjacent surfaces. Check actual rendered/composited colors. Do not rely on color alone to communicate selection, warning or failure.

Warning resources already use orange in Semi. Keep them semantically independent and retain warning icons, labels and presentation. Preserve information, success and danger meanings.

## Task 1 — Audit theme resources and establish the integration approach

**Goal:** Identify the smallest maintainable integration compatible with the installed packages.

**Dependencies:** None.

### Implementation

- Inspect repository instructions, App.axaml, package references, merged dictionaries, theme services and persisted appearance settings.
- Record installed Avalonia, Semi, Ursa and any grid/theme-extension package versions.
- Check matching upstream sources rather than assuming main matches the application.
- Locate hardcoded decorative accent colors in XAML, converters and custom drawing code.
- Trace representative Semi and Ursa controls from template resource keys through aliases to semantic brushes.
- Identify resource precedence and a suitable application-owned palette-loading location.
- Demonstrate the proposed override using one Semi button and one Ursa control, including a runtime change.
- Document the resource map and any targeted control-level overrides required.

### Acceptance criteria

- [x] Installed versions and relevant existing files are documented.
- [x] The override demonstrably reaches both representative controls.
- [x] Resource lookup and static-alias limitations are understood.
- [x] The implementation extends existing infrastructure without introducing a second competing theme service.

## Task 2 — Implement the Mission Orange palette

**Goal:** Provide a complete, centrally maintained orange scheme based on the sampled screenshot.

**Dependencies:** Task 1.

### Implementation

- Add application-owned palette resources following the repository's conventions.
- Keep both existing Semi and Ursa theme providers.
- Implement the dark-mode mappings and neutral surfaces specified above.
- Complete all primary, disabled, focus and link states.
- Add the foreground-on-accent resources required for solid primary controls.
- Define a light-mode counterpart with readable orange text and appropriate selection surfaces.
- Preserve independent information, success, warning and danger resource families.
- Use dynamic resource references where runtime changes are required, with targeted control-resource overrides where upstream aliases prevent propagation.
- Preserve any existing high-contrast behavior independently of the orange scheme.

### Acceptance criteria

- [x] Dark-mode primary colors match the sampled palette.
- [x] Light mode uses a separately validated palette.
- [x] Resource types match their consumers and no resources are unresolved.
- [x] Both Semi and Ursa controls receive the intended brushes.
- [x] Semantic status colors and existing high-contrast behavior are preserved.

## Task 3 — Correct foregrounds and interaction states

**Goal:** Make the palette readable and consistent across interaction states.

**Dependencies:** Task 2.

### Implementation

- Apply dark primary-button foregrounds for normal/hover and white for pressed, as specified by the contrast table.
- Scope changes to solid primary controls; preserve other semantic button foregrounds.
- Inspect normal, hover, pressed, focused, selected, checked and disabled states.
- Cover buttons, checkboxes, radio buttons, switches, sliders, progress indicators, text-input focus borders, tabs and list/grid selections.
- Use readable light text on brown selection surfaces, with a visible orange indicator or border where needed.
- Keep disabled presentation distinct from brown selected states.
- Check accent text separately from solid button backgrounds; introduce scoped foreground tokens where the shared primary brush is insufficiently readable.
- Verify contrast on actual backgrounds, including opacity and animation effects.
- Produce a representative control preview using the sampled palette.

### Acceptance criteria

- [x] Normal text meets the 4.5:1 target and meaningful focus/control indicators meet 3:1.
- [x] Solid primary foregrounds follow the state-specific contrast requirements.
- [x] Selection, focus and disabled states are distinguishable.
- [x] Warning and danger controls retain their independent appearance.
- [x] Screenshots show representative controls and states in dark and light modes.

## Task 4 — Apply the scheme to MissionPlanner's shared UI

**Goal:** Integrate the palette throughout the application while preserving operational color meanings.

**Dependencies:** Tasks 2–3.

### Implementation

- Apply shared resources to navigation/flyout, topbar, statusbar, shared panels, dialogs and notifications.
- Reuse existing topbar button styles, retaining consistent sizing, spacing and state behavior across all topbar actions.
- Replace hardcoded decorative accent colors with shared theme resources.
- Review Parameters, Radio Calibration, Flight Modes, Motor Test, Firmware Installation, Inspector, Messages and Preflight views where present.
- Audit custom-rendered controls separately for cached colors and theme-change invalidation.
- Preserve intentional HUD, telemetry, map and chart color semantics.
- Keep failures, warnings, connection states and destructive actions distinguishable using labels/icons as well as color.

### Acceptance criteria

- [x] Active navigation and ordinary primary actions use Mission Orange consistently.
- [x] Shared topbar controls retain a consistent layout and interaction model.
- [x] Operational and safety-related colors retain their intended meanings.
- [x] Dialogs, popups and custom-rendered elements are included in the review.
- [x] No unrelated layout or behavioral changes are introduced.

## Task 5 — Add appearance selection and persistence

**Goal:** Make the new scheme selectable and persistent through the existing settings infrastructure.

**Dependencies:** Task 2; coordinate with Task 4 for application-wide propagation.

### Implementation

- Add a color-scheme choice: Original / Mission Orange.
- Keep color scheme separate from System / Light / Dark appearance mode.
- Extend the existing settings model, settings UI and theme service.
- Preserve existing users' appearance choice during migration; do not silently force Mission Orange.
- Apply selection changes to already-open windows, dialogs and popups.
- Restore saved preferences at startup, avoiding a visible incorrect-palette flash where practical.
- Confirm that System mode follows operating-system appearance changes while retaining the selected color scheme.
- Restore original resources fully when switching back to Original.

### Acceptance criteria

- [x] Scheme and appearance mode are independently selectable.
- [x] Preferences survive restart and old settings migrate safely.
- [x] Runtime switching updates the entire UI without reopening it.
- [x] Original restores the previous palette without leftover orange overrides.
- [x] System-mode transitions preserve the scheme choice.

## Task 6 — Verify and document the finished theme

**Goal:** Deliver a coherent, maintainable and verified implementation.

**Dependencies:** Tasks 3–5.

### Implementation

- Build the application and run the repository's relevant required checks.
- Inspect representative pages under Original and Mission Orange, each in light and dark mode.
- Check startup restoration, runtime switching, System-mode transitions, popup/dialog coverage and keyboard focus.
- Verify status semantics and existing high-contrast behavior.
- Add focused preference-persistence and resource-resolution tests where supported by the existing infrastructure; avoid tests that merely duplicate literal palette definitions.
- Document palette resources, integration points, package-specific overrides and how to adjust colors later.
- Attach before/after screenshots, including a shared topbar/panel view and a Preflight view.
- Record any remaining contrast or platform limitations explicitly.

### Acceptance criteria

- [x] Build and relevant checks pass.
- [x] No missing resources or stale colors after switching remain.
- [x] Primary controls are readable in their interaction states.
- [x] Status and high-contrast behavior remain intact.
- [x] Documentation and screenshots make the final implementation reviewable.

## Delivery order

1. Complete Task 1 and establish the resource integration.
2. Complete Tasks 2–3 and provide the first representative visual preview using the sampled palette.
3. Apply the established design across the UI through Task 4 and complete preference integration through Task 5.
4. Complete Task 6 with final verification and screenshots.

The first preview is a useful visual review milestone. No GitHub issue creation, package upgrade or theme-library fork is required by this brief.

