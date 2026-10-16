# MissionPlanner Next Gen — Dark Orange and Light Orange themes

Updated: 16 October 2026

## Replacement brief

This document replaces the previous Mission Orange tasks. Implement the work on the user's new branch, respecting repository instructions and existing changes. Inspect the branch state first: it may contain the earlier implementation, or it may start before that implementation. Reuse suitable work when present; do not assume it exists or reintroduce the previous independent color-scheme architecture.

**Target:** two named Avalonia theme variants, **Dark Orange** and **Light Orange**, implemented in the existing **MissionPlanner.Themes** project and selected through the application's existing theme selector.

Each variant still has a palette. The architectural change is to register the palettes and required control resources under named theme variants instead of applying an independent global orange overlay.

This is an implementation specification, not a claim that the current implementation has been reviewed or that GitHub issues have been created.

## Architecture decisions

1. Keep SemiTheme and UrsaSemiTheme as the base control theme providers.
2. Add one MissionPlannerTheme provider in MissionPlanner.Themes.
3. Declare DarkOrange with ThemeVariant.Dark as its fallback, and LightOrange with ThemeVariant.Light as its fallback.
4. Register orange-specific resources under those exact variant objects in ResourceDictionary.ThemeDictionaries.
5. Keep original Light, Dark, System, Aquatic, Desert, Dusk and Night Sky behavior available.
6. Add Dark Orange and Light Orange to the existing theme selector. Retire the redundant Original / Mission Orange color-scheme selector after handling existing settings.
7. Keep theme definitions, palettes and control adapters in MissionPlanner.Themes. Keep preference persistence and operating-system theme coordination in the application layer.
8. Preserve existing control layouts, dimensions, typography and application behavior.

Semi's Aquatic, Desert, Dusk and Night Sky variants use its high-contrast control dictionaries. Follow their named-variant registration pattern, but use ordinary Dark and Light fallbacks for the orange variants. Do not adopt the high-contrast control dictionaries as the orange base.

### Variant definitions

```csharp
using Avalonia.Styling;

namespace MissionPlanner.Themes;

public static class MissionPlannerThemeVariants
{
    public static ThemeVariant DarkOrange { get; } =
        new("DarkOrange", ThemeVariant.Dark);

    public static ThemeVariant LightOrange { get; } =
        new("LightOrange", ThemeVariant.Light);
}
```

Use stable identifiers in persisted settings; keep display labels separate. Follow existing repository naming conventions where necessary without changing the intended semantics.

### Suggested project organization

| File in MissionPlanner.Themes | Responsibility |
| --- | --- |
| MissionPlannerThemeVariants.cs | Stable named variants and fallback relationships |
| MissionPlannerTheme.axaml and code-behind | Resource/style composition and XAML initialization |
| Tokens/Palette/DarkOrange.axaml | Dark colors and semantic brushes |
| Tokens/Palette/LightOrange.axaml | Light colors and semantic brushes |
| Themes/DarkOrange.axaml | Dark palette composition and necessary control-resource adapters |
| Themes/LightOrange.axaml | Light palette composition and necessary control-resource adapters |
| Themes/Shared.axaml | Shared application styling, only if required |

Treat these paths as a proposed organization. Match the actual assembly name, resource build configuration and project conventions.

### Registration pattern

Within the MissionPlanner theme provider, register resources using the variant objects:

```xml
<ResourceDictionary.ThemeDictionaries>
    <ResourceInclude
        x:Key="{x:Static local:MissionPlannerThemeVariants.DarkOrange}"
        Source="/Themes/DarkOrange.axaml" />
    <ResourceInclude
        x:Key="{x:Static local:MissionPlannerThemeVariants.LightOrange}"
        Source="/Themes/LightOrange.axaml" />
</ResourceDictionary.ThemeDictionaries>
```

This is a structural example, not a complete file. Supply correct namespace declarations and resource URIs, and verify loading from the theme assembly.

The application should load the providers once:

```xml
<semi:SemiTheme Locale="en-US" />
<semi:UrsaSemiTheme Locale="en-US" />
<mpThemes:MissionPlannerTheme />
```

Select the appropriate variant through the existing service that controls Application.RequestedThemeVariant. Do not introduce a competing theme service.

## Resource rules and implementation risks

- Keep orange overrides scoped to DarkOrange and LightOrange. Do not overwrite ordinary Dark/Light dictionaries or place orange semantic overrides in unconditional shared resources.
- Variant inheritance supplies fallback resources; it does not guarantee that every upstream StaticResource alias will bind to an overridden semantic brush.
- Trace the actual lookup chain from template to control-resource key to semantic brush. Add narrowly scoped adapter resources where necessary.
- Do not copy entire Semi/Ursa control-template collections or fork either library to implement a color scheme.
- Resources such as SemiColorPrimary are brushes. Resources such as SemiOrange5Color are Color values. Preserve expected resource types.
- Defining SemiOrange0Color through SemiOrange9Color does not automatically make primary controls orange. Primary semantic brushes must reference the intended orange colors.
- Do not globally replace SemiBlue colors: the same blue palette also supports informational messages.
- Keep warning, information, success and danger resources semantically independent.
- Preserve the sampled visual direction below. Reusing upstream orange tokens is an implementation option only when it produces the intended result; do not silently substitute a different palette.
- Base all implementation details on installed package versions. Upstream main may differ.

## Visual specification

### Dark Orange palette

These colors were sampled from flat regions of the supplied VLC design screenshot. They are screenshot pixel values, not verified original design tokens. The roles below are the proposed application mapping.

| Role | Value |
| --- | --- |
| Primary background | #D27917 |
| Primary hover | #FC962A |
| Primary pressed | #AB6314 |
| Strong selected background | #663A0B |
| Subtle selected background | #442707 |
| Main neutral background | #16161A |
| Panels/navigation | #232429 |
| Raised surface | #35363C |
| Foreground on primary normal/hover | #16161A |
| Foreground on primary pressed | #FFFFFF |

Initial semantic mappings:

| Brush key | Value |
| --- | --- |
| SemiColorPrimary | #D27917 |
| SemiColorPrimaryPointerover | #FC962A |
| SemiColorPrimaryActive | #AB6314 |
| SemiColorPrimaryLight | #442707 |
| SemiColorPrimaryLightPointerover | #663A0B |
| SemiColorPrimaryLightActive | #663A0B |
| SemiColorFocusBorder | #FC962A |
| SemiColorLink | #FC962A |

Use opaque brown selection brushes initially. Additional opacity changes the sampled appearance. The shared hover/active brown is an initial choice; add another indicator where interaction states need differentiation.

Define suitable primary-disabled and link hover/active/visited resources as well. The screenshot does not prescribe these. Do not use the brown selection palette automatically for disabled controls.

### Light Orange palette

Create a separate light palette using neutral light surfaces, readable dark-orange accents and appropriate light selection fills. Validate contrast independently. Do not apply dark-mode brown surfaces universally to Light Orange.

### Foreground contrast

Measured contrast for the opaque sampled backgrounds:

| Background | White text | Text #16161A |
| --- | --- | --- |
| #D27917 | 3.24:1 | 5.57:1 |
| #FC962A | 2.20:1 | 8.19:1 |
| #AB6314 | 4.64:1 | 3.89:1 |

For solid primary controls retaining these exact backgrounds, use dark text/icons for normal and hover, and white for pressed. Scope the foreground change to primary controls; changing a globally shared ButtonSolidForeground resource could also alter success, warning and danger buttons.

Target at least 4.5:1 for normal text and 3:1 for meaningful control boundaries and focus indicators against adjacent surfaces. Verify actual composited backgrounds and transition states. Text-only primary actions may require a brighter foreground than the primary solid-background color.

Use readable light text on brown selected surfaces and a visible selection indicator where appropriate. Preserve warning icons and labels because warnings and the primary accent both use orange-family colors.

## Task 1 — Audit the new branch and establish the theme integration

**Goal:** Ground the work in the actual repository and installed dependencies.

**Dependencies:** None.

### Work

- Inspect repository instructions, branch status and current appearance implementation without discarding unrelated changes.
- Inspect the existing MissionPlanner.Themes project, target framework, package references, assembly name and Avalonia resource build configuration.
- Identify App.axaml, theme-selection service, preference model and existing theme list.
- Record installed Avalonia, Semi, Ursa and optional grid/theme package versions.
- If the earlier orange implementation exists, inventory reusable palette resources, control adapters, tests and settings.
- Trace at least one Semi button, one Ursa control and one application-owned panel through their resource lookup chains.
- Identify global overrides, dictionary swapping, hardcoded colors and comparisons that assume ActualThemeVariant is exactly Dark or Light.
- Document the intended project dependency direction: application references MissionPlanner.Themes; the theme project must not reference the application.

### Acceptance criteria

- [x] Actual branch state and installed versions are recorded.
- [x] Existing project and appearance infrastructure are identified.
- [x] Reusable earlier work and obsolete overlay behavior are distinguished.
- [x] Resource lookup and fallback risks are documented before implementation.

## Task 2 — Implement the named variants and theme provider

**Goal:** Load two correctly scoped variants from MissionPlanner.Themes.

**Dependencies:** Task 1.

### Work

- Add stable DarkOrange and LightOrange variant declarations with ordinary Dark and Light fallbacks.
- Implement MissionPlannerTheme and ensure its AXAML resources initialize correctly using the repository's conventions.
- Register each variant's dictionary using the same variant object exposed by the variant declarations.
- Add the theme provider after SemiTheme and UrsaSemiTheme in application composition, verifying actual precedence.
- Keep the providers loaded once; avoid rebuilding all style providers or changing global palettes on each selection.
- Use valid assembly-qualified resource URIs where required for cross-assembly references.
- Keep orange-only styles/resources isolated from all original variants.

### Acceptance criteria

- [x] Both variants can be selected programmatically and render controls.
- [x] Unspecified resources fall back to the intended Dark/Light base.
- [x] Original and high-contrast variants do not acquire orange overrides.
- [x] No resource-loading exceptions or circular project dependencies occur.

## Task 3 — Implement both palettes and required control adapters

**Goal:** Deliver the sampled orange appearance through complete semantic resources.

**Dependencies:** Task 2.

### Work

- Implement Dark Orange using the palette and foreground specification above.
- Implement and validate a corresponding Light Orange palette.
- Define all primary states, subtle selections, focus and link states, plus dedicated disabled resources.
- Preserve information, success, warning and danger semantics.
- Verify StaticResource alias behavior using actual controls, including runtime switching.
- Add only the required control-level resource adapters and scoped primary-foreground styles.
- Inspect buttons, checkboxes, radio buttons, switches, sliders, progress, text-input focus, tabs and list/grid selection.
- Produce a representative control preview in both variants, including interaction states.

### Acceptance criteria

- [x] Dark Orange matches the sampled visual direction.
- [x] Light Orange is independently readable.
- [x] Foreground contrast is correct in normal, hover and pressed states.
- [x] Selection, disabled and warning states remain distinguishable.
- [x] No wholesale upstream template duplication is introduced.

## Task 4 — Integrate theme selection and handle existing preferences

**Goal:** Use one coherent theme selector and preserve existing settings where applicable.

**Dependencies:** Tasks 2–3.

### Work

- Add display entries Dark Orange and Light Orange alongside the existing theme entries.
- Persist stable theme identifiers and resolve them to the corresponding ThemeVariant objects.
- Retain System, Light, Dark, Aquatic, Desert, Dusk and Night Sky.
- Retire the separate Original / Mission Orange selector once migration is handled.
- If previous color-scheme settings exist, migrate Dark + Mission Orange to DarkOrange and Light + Mission Orange to LightOrange.
- For Original, preserve the previous theme selection.
- If System + Mission Orange was previously supported, preserve its intent with a System Orange preference that resolves operating-system appearance to DarkOrange or LightOrange. This is an application selection policy, not a third resource variant inheriting from Default.
- Preserve existing high-contrast selections. Do not overlay orange onto them during migration; audit how the earlier implementation treated such combinations.
- If starting from a branch with no previous scheme settings, avoid introducing speculative migration infrastructure for settings that never existed in that baseline; document any user-profile compatibility actually required.
- Apply saved selection before initial window rendering where practical.
- Update open windows, dialogs and popups during runtime selection changes.

### Acceptance criteria

- [x] The main selector includes the two orange variants.
- [x] No redundant color-scheme selector remains.
- [x] Preferences survive restart and original selections remain available.
- [x] Existing relevant settings migrate without silently losing System behavior.
- [x] Runtime switching works without restarting the application.

## Task 5 — Integrate application-owned UI and remove obsolete overlay code

**Goal:** Make the application consistently theme-aware while preserving operational colors.

**Dependencies:** Tasks 3–4.

### Work

- Apply shared resources to navigation, topbar, statusbar, panels, dialogs and notifications.
- Preserve common topbar button sizing, spacing, templates and interaction conventions.
- Audit Parameters, Radio Calibration, Flight Modes, Motor Test, Firmware Installation, Inspector, Messages and Preflight where present.
- Trace the preferences card background shown in the screenshots; determine whether its cross-theme behavior is intentional or a resource defect.
- Replace decorative hardcoded colors with appropriate resources.
- Audit custom drawing and cached brushes for theme-change invalidation.
- Update dark/light detection that relies on direct equality with built-in variants so custom variants retain correct base appearance behavior.
- Preserve deliberate HUD, telemetry, map, chart, connection and failure color semantics. A blue informational banner can remain blue under Dark Orange.
- Remove obsolete scheme-overlay services, global mutations and duplicate resources only after their responsibilities are covered by the new architecture.

### Acceptance criteria

- [x] Both orange variants cover application-owned surfaces and standard controls.
- [x] Topbar controls retain their existing consistent layout.
- [x] Switching back to an original theme leaves no stale orange resources.
- [x] Operational/status colors keep their intended meaning.
- [x] No competing appearance application mechanisms remain.

## Task 6 — Verify, document and deliver the refactor

**Goal:** Demonstrate that the named-theme implementation works across existing and new selections.

**Dependencies:** Tasks 2–5.

### Work

- Build the application and run relevant repository-required checks.
- Verify Dark Orange and Light Orange at startup and during repeated switching to and from original themes.
- Include original Light/Dark, System and all available high-contrast variants in regression review.
- Check dialogs, popup menus, tooltips, keyboard focus, selected/disabled controls and custom-rendered views.
- Verify persistence and applicable settings migration, including System Orange only if required.
- Add focused tests for variant mapping, fallback/resource resolution and preference migration where existing test infrastructure supports them.
- Validate rendered control colors as well as token lookup; token presence alone does not prove upstream aliases resolve correctly.
- Document project structure, variant identifiers, resource composition, package versions and justified adapters.
- Provide before/after screenshots of a shared topbar/panel view, Preferences and Preflight or another status-rich page.
- Report remaining platform or contrast limitations explicitly.

### Acceptance criteria

- [x] Build and relevant checks pass.
- [x] No missing resources, stale brushes or theme-switch exceptions remain.
- [x] Primary foreground and focus contrast requirements are met.
- [x] Existing high-contrast and operational status behavior is preserved.
- [x] Documentation and screenshots support review of the final result.

## Delivery sequence

1. Audit the branch and establish the theme provider (Tasks 1–2).
2. Complete the palettes and provide a representative preview (Task 3).
3. Integrate preferences, application surfaces and cleanup (Tasks 4–5).
4. Complete regression verification and documentation (Task 6).

Reuse the previous implementation selectively if present. This task is a theme architecture refactor, not a redesign of the application or a mandatory dependency upgrade.

## Source references

Sources were inspected upstream; implementation must confirm the installed versions.

- [Semi theme variants](https://github.com/irihitech/Semi.Avalonia/blob/main/src/Semi.Avalonia/SemiTheme.axaml.cs)
- [Semi resource registration](https://github.com/irihitech/Semi.Avalonia/blob/main/src/Semi.Avalonia/SemiTheme.axaml)
- [Semi palette registration](https://github.com/irihitech/Semi.Avalonia/blob/main/src/Semi.Avalonia/Tokens/_index.axaml)
- [Semi dark palette](https://github.com/irihitech/Semi.Avalonia/blob/main/src/Semi.Avalonia/Tokens/Palette/Dark.axaml)
- [Semi light palette](https://github.com/irihitech/Semi.Avalonia/blob/main/src/Semi.Avalonia/Tokens/Palette/Light.axaml)
- [Semi button resource aliases](https://github.com/irihitech/Semi.Avalonia/blob/main/src/Semi.Avalonia/Themes/Dark/Button.axaml)
- [Ursa theme registration](https://github.com/irihitech/Ursa.Avalonia/blob/main/src/Ursa.Themes.Semi/UrsaSemiTheme.axaml)
- [Avalonia ThemeVariant and fallback semantics](https://github.com/AvaloniaUI/Avalonia/blob/main/src/Avalonia.Base/Styling/ThemeVariant.cs)
- [Visual reference video](https://www.youtube.com/watch?v=ERFeOgqk324&t=725s): palette derived from the user's screenshot, not direct video extraction.

## Delivery — 16 October 2026

Implemented as a new named-variant feature on the user's branch, without comparing or reusing the removed implementation. Migration/overlay-removal items are not applicable: this baseline has only Appearance.ThemeId and one selector. System Orange was therefore not added.

See [implementation and verification notes](../../ORANGE_THEME_VARIANTS.md) for the dependency audit, resource chains, foreground adapters, Preferences surface explanation, and fresh original/orange review screenshots.

Application/UI-test build and Windows desktop build passed. The six focused fixture classes passed 19 tests in total: named rendering/switching (1), theme catalog/settings reload (10), Preflight rendering (1), Quick Editor layout (3), diagnostic menu/firmware layout (3), and document rendering (1). Existing unrelated compiler warnings remain; no new missing-XML-documentation warnings were introduced. The full solution and hardware integration suites were not run.

Automated coverage uses offline headless Skia and the installed control templates. Native OS theme-change notifications, platform window chrome, screen-reader behavior and device-connected workflows remain manual verification items. This does not change existing System theme behavior. Contrast assertions cover primary solid states, their borders, and focus against composited input surfaces; this is not a claim of an application-wide accessibility certification.
## Full-control resource follow-up — 16 October 2026

The later user clarification supersedes the earlier information-blue and screenshot-sampled palette choices: primary, secondary and information accents now use Semi's orange scales; success/warning/danger meanings remain intact. Both named variants load their folder indexes directly, composing the complete Semi/Ursa control resource sets with the corresponding full palette. The previous small root alias files were removed.

The full Preferences page, including the information banner's rendered background, border and glyph, is now part of regression coverage. All static control aliases are checked against their semantic targets. Updated screenshots and composition details are in [ORANGE_THEME_VARIANTS.md](../../ORANGE_THEME_VARIANTS.md).
