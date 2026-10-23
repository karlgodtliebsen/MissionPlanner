# Codex Task — Refine CompassSetupView button placement and button styles

## Goal

Keep the current `CompassSetupView` structure:

```text
Persistent summary
Information | Configuration
```

but improve action hierarchy. The present Configuration toolbar mixes page-level, calibration, pending-change and reboot actions. Move actions closer to the content they affect, while preserving all existing commands and behavior.

## 1. Add explicit style classes

Add/use these classes in the shared UI styles:

```xml
<Style Selector="Button.ToolbarIconButton">
    <Setter Property="Width" Value="100" />
    <Setter Property="Theme" Value="{DynamicResource SolidIconButton}" />
</Style>

<Style Selector="Button.EmbeddedIconButton">
    <Setter Property="Width" Value="50" />
</Style>

<Style Selector="Button.EmbeddedTextButton">
    <Setter Property="MinWidth" Value="100" />
</Style>
```

`ToolbarIconButton` replaces `IsToolbarButton` for Compass.

Before removing/renaming `IsToolbarButton` globally, search the solution. If other views use it, keep backward compatibility and update Compass only in this task.

For icon dropdown buttons prefer explicit classes rather than a broad selector affecting every `IconDropDownButton`:

```xml
<Style Selector="u|IconDropDownButton.ToolbarIconDropDownButton">
    <Setter Property="Width" Value="100" />
    <Setter Property="Theme" Value="{DynamicResource SolidIconButton}" />
</Style>

<Style Selector="u|IconDropDownButton.EmbeddedIconDropDownButton">
    <Setter Property="Width" Value="50" />
</Style>

<Style Selector="u|IconDropDownButton.EmbeddedTextDropDownButton">
    <Setter Property="MinWidth" Value="100" />
</Style>
```

Keep the exact numeric values easy to change later; Compass is still the UX experiment.

## 2. Keep only page-level actions in the Configuration toolbar

Keep:

- Refresh
- Open Full Parameters

Use `ToolbarIconButton`.

Move these out of the top toolbar without changing their commands:

- Start calibration
- Cancel calibration
- Reset / retry
- Accept calibration
- Discard
- Apply
- Reboot vehicle

## 3. Move calibration actions into `Calibration / Actions`

Relocate:

```text
StartCalibrationCommand
CancelCalibrationCommand
ResetCommand
AcceptCalibrationCommand
```

into the existing Calibration / Actions `SectionCard`.

Use `EmbeddedTextButton` where visible text improves clarity.

Desired presentation by state, using existing ViewModel state/CanExecute information only:

```text
Not started     [Start Calibration]
In progress     [Cancel]
Completed       [Accept Results] [Retry]
Failed          [Retry]
```

Do not redesign the calibration state machine. If existing state properties do not support exact visibility, preserve current enable/disable behavior.

## 4. Move Apply / Discard into Pending Changes

Put:

```text
DiscardCommand
ApplyCommand
```

inside or immediately adjacent to the pending-changes area.

Use `EmbeddedTextButton`.

Preferred visual relationship:

```text
Pending changes
<change list>

                         [Discard] [Apply]
```

The controls must clearly belong to the pending changes rather than to the whole page.

## 5. Move Reboot next to reboot-required feedback

Remove Reboot from the toolbar.

When `RequiresReboot` is true, display:

```text
Changes applied. Reboot required.

[Reboot Vehicle]
```

Use `EmbeddedTextButton`, preserving `RebootCommand`.

Never reboot automatically.

## 6. Per-setting Reset to default

The per-setting restore action is a local secondary action.

Change it from the toolbar class to:

```xml
Classes="EmbeddedIconButton"
```

Preserve:

```text
ResetDefaultCommand
CanReset
ToolTip.Tip
AutomationProperties.Name
```

## 7. Icon + text composition

For embedded text actions use icon + visible text, for example:

```xml
<Button Classes="EmbeddedTextButton"
        Command="{Binding StartCalibrationCommand}">
    <StackPanel Orientation="Horizontal" Spacing="6">
        <mdi:MaterialIcon Kind="CompassOutline" Classes="ActionIcon" />
        <TextBlock Text="Start Calibration" />
    </StackPanel>
</Button>
```

Adapt to existing MissionPlanner templates if there is already a cleaner common pattern.

## 8. IconDropDownButton rule

An embedded dropdown may be used when several related **secondary** actions otherwise clutter one local section.

Do not hide:

- the current primary action;
- frequent actions;
- safety-relevant actions;

inside a dropdown merely to reduce visual count.

Do not force a dropdown into Compass if the relocated controls are already clear.

## 9. Preserve InformationDocumentView

Do not redesign the Markdown/Copy actions owned by `InformationDocumentView` in this task.

## 10. Preserve functionality

Do not change:

- Compass parameter mapping;
- Compass configuration service;
- parameter write semantics;
- calibration protocol;
- pending-change behavior;
- reboot implementation;
- Information/Configuration tab architecture;
- LiveMarkdown document generation.

This is a focused UI refinement.

## 11. Accessibility

Every icon-only button retains a tooltip and `AutomationProperties.Name`.

Text buttons also need useful automation names.

## 12. Validation

Build Desktop and Browser/WASM if they are part of normal solution validation.

Run existing Compass tests.

Manual check:

1. Information tab unchanged.
2. Configuration toolbar contains only Refresh and Full Parameters.
3. Calibration commands live inside Calibration / Actions.
4. Per-setting reset controls use the smaller embedded style.
5. Apply/Discard visually belong to pending changes.
6. Reboot appears beside reboot-required feedback.
7. All commands and enable/disable behavior still work.
8. Scrolling remains correct.
9. No style rename breaks unrelated pages.

## Acceptance criteria

The visual meaning should become:

```text
ToolbarIconButton
    page/subsystem-level action

EmbeddedTextButton
    important action affecting its local section

EmbeddedIconButton
    small secondary action affecting nearby content

IconDropDownButton
    optional grouping of related secondary actions
```

The page should retain desktop efficiency while making action scope immediately understandable.
