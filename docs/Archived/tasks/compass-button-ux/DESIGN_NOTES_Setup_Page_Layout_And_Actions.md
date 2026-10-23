# Design notes — preserve for later Setup UX tasks

These are intentionally broader than the current Compass button task.

## Setup page structure

Preferred pattern for substantial Setup pages:

```text
Persistent summary/header

Information | Configuration
```

The mental model is:

```text
Persistent summary -> What is important right now?
Information        -> What does it mean?
Configuration      -> What can I change?
```

Likely future candidates include Arming, Battery, GPS, Failsafe, Radio, Serial Ports and Firmware, after Compass is validated.

## Action locality

Prefer actions beside the state/content they affect:

```text
Calibration section -> Start / Cancel / Retry / Accept
Pending changes      -> Discard / Apply
Reboot warning       -> Reboot
Individual setting   -> Reset to default
```

The toolbar remains appropriate for page/subsystem-level commands such as Refresh and Open Full Parameters.

## Button taxonomy

Proposed semantic classes:

```text
ToolbarIconButton
EmbeddedIconButton
EmbeddedTextButton

ToolbarIconDropDownButton
EmbeddedIconDropDownButton
EmbeddedTextDropDownButton
```

The visual values are deliberately not final. Centralized classes make later experimentation easy.

## Dropdowns

Use dropdowns for related secondary actions. Do not hide the current primary action just to make the UI sparse.

## Configuration density

Current setting cards are clear but vertically expensive.

Possible later refinement:

```text
one Configuration SectionCard
    setting row
    divider
    setting row
    divider
    setting row
```

instead of a nested SectionCard for every setting.

Do not combine that experiment with the current button task unless necessary.

## Information diagnostics

Preferred structure:

```text
Human-readable Markdown report

Technical details / diagnostics [collapsed]
```

Curated Markdown is for users; raw registry/metadata evidence is expert/developer material.

## Centered content layout

Important correction: Avalonia does not use CSS-style `MinMax(...)` in the `ColumnDefinitions` shorthand.

Use explicit definitions when min/max constraints are required:

```xml
<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*" />
        <ColumnDefinition Width="*" MinWidth="600" MaxWidth="900" />
        <ColumnDefinition Width="*" />
    </Grid.ColumnDefinitions>

    <Grid Grid.Column="1">
        ...
    </Grid>
</Grid>
```

For narrow/responsive layouts, reconsider a hard `MinWidth="600"`; a max-width-centered content region with normal page margins may be preferable.

If this layout becomes common, consider a MissionPlanner-owned wrapper such as:

```text
SetupPageLayout
CenteredContentLayout
MainContentLayout
```

A small `ContentControl`/templated control that owns the internal centering grid is likely cleaner than repeating runtime `Bounds.Width` bindings or requiring callers to know an internal grid column.

Do not implement that abstraction in the current Compass button task. First confirm the pattern across multiple pages.

## General principle

Prefer declarative layout constraints over:

```text
Width="{Binding #SomeControl.Bounds.Width}"
```

once the final page pattern is known.

## Preserve desktop character

The goal is not to remove desktop toolbars or make MissionPlanner look like a mobile application.

The target is:

```text
desktop efficiency
+ clear action hierarchy
+ context-local actions
+ consistent visual semantics
```
