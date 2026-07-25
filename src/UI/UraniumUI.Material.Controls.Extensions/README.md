# UraniumUI aligned editor and numeric up/down controls

This source package contains:

```text
AlignedEditorField
NumericUpDownField
NumericUpDownButtonOrientation
NumericValueChangedEventArgs
```

The controls target the current `develop` structure of UraniumUI.

## Why `NumericUpDownField` derives from `TextField`

A numeric editor is a single-line input. UraniumUI's `TextField` already provides:

- Material outlined/floating-title presentation;
- validation;
- keyboard configuration;
- font and color properties;
- horizontal text alignment;
- attachment support;
- focus and accessibility handling.

`NumericUpDownField` adds vertical text alignment and places its stepper in the
existing `InputField.Attachments` area. Because attachments are inside
`InputField`'s `Border`, the outline surrounds the editor and both buttons.

This avoids composing an `EditorField` with two unrelated external buttons.

## Files to add

For an application-local implementation, copy:

```text
src/UraniumUI.Material/Controls/AlignedEditorField.cs
src/UraniumUI.Material/Controls/NumericUpDownField.cs
src/UraniumUI.Material/Controls/NumericUpDownField.BindableProperties.cs
src/UraniumUI.Material/Controls/NumericUpDownButtonOrientation.cs
src/UraniumUI.Material/Controls/NumericValueChangedEventArgs.cs
```

The namespace is:

```csharp
UraniumUI.Material.Controls
```

When compiled into another assembly, map it explicitly:

```xml
xmlns:extended=
  "clr-namespace:UraniumUI.Material.Controls;assembly=YOUR_ASSEMBLY"
```

## AlignedEditorField

```xml
<extended:AlignedEditorField
    Title="Notes"
    Text="{Binding Notes}"
    HorizontalTextAlignment="Center"
    VerticalTextAlignment="Center" />
```

It intentionally changes no other `EditorField` behavior.

## NumericUpDownField

### String-backed ViewModel

This matches the current MissionPlanner parameter editor:

```xml
<extended:NumericUpDownField
    Title="Number"
    Text="{Binding Value, Mode=TwoWay}"
    Min="{Binding Minimum}"
    Max="{Binding Maximum}"
    StepSize="{Binding StepSize}"
    HorizontalTextAlignment="Center"
    VerticalTextAlignment="Center" />
```

### Double-backed ViewModel

```xml
<extended:NumericUpDownField
    Value="{Binding Altitude, Mode=TwoWay}"
    Min="0"
    Max="10000"
    StepSize="0.5"
    DecimalPlaces="1" />
```

Bind either `Text` or `Value` as the primary application property. Binding both
is normally unnecessary.

## Numeric behavior

Properties:

```text
Value
Min
Max
StepSize
DecimalPlaces
NumberFormat
CultureName
IsWrapEnabled
ClampOnCommit
IsTextValid
```

Commands:

```text
IncrementCommand
DecrementCommand
```

Manual text is parsed using `CultureName`, or the current UI culture when it is
empty. Invariant parsing is also attempted as a fallback because ArduPilot
parameter files commonly use a decimal point.

Incomplete or invalid text is allowed while typing. It is normalized when the
field loses focus or editing completes.

## Stepper visuals

Default horizontal layout:

```text
┌────────────────────────────────┐
│ value                       − ＋│
└────────────────────────────────┘
```

Vertical layout:

```text
┌──────────────────────────────┐
│ value                      ＋ │
│                            − │
└──────────────────────────────┘
```

Configure it with:

```text
ShowStepperButtons
ButtonOrientation
IncrementText
DecrementText
StepButtonWidth
StepButtonHeight
StepButtonFontSize
StepButtonBackgroundColor
```

The defaults use the Unicode mathematical minus `−` and full-width plus `＋`.
These have more balanced visual weight than a hyphen and ASCII plus in many
system fonts and do not require a Material Icons font registration.

For an upstream UraniumUI version, icon templates or `ImageSource` properties
could be added later. The character-based implementation is deliberately
dependency-free and works across Windows, Android, iOS, and Mac Catalyst.

## VirtualizedDataGrid guidance

When used inside a recycled grid row:

- bind `Text` or `Value` directly to the row ViewModel;
- keep validation and modified state in the ViewModel;
- do not retain pending values only in the native Entry;
- use a fixed row height where practical.

This ensures recycled rows restore the correct value and state.

## Suggested upstream PR split

A clean UraniumUI contribution would be two pull requests:

1. Add horizontal and vertical alignment directly to `EditorField`.
2. Add `NumericUpDownField` as a new single-line Material input control.

Keeping them separate makes review and regression testing easier.

## Tests

Starter tests are supplied as `.cs.txt` files. Rename them to `.cs` after copying
them into the UraniumUI test project.

The package was prepared against the current source shape but could not be
compiled in this environment because the .NET SDK is unavailable.

## 1.1 responsiveness correction

The first draft overrode `OnPropertyChanged` and re-checked the public
`Attachments` property from that override. In UraniumUI, the public getter can
return the rendered `EndIconsContainer.Children` after the control template is
applied, while the bindable `AttachmentsProperty` still owns the source
collection. Mixing those collections could create repeated attachment and
visual-tree updates.

Version 1.1:

- removes the `OnPropertyChanged` override;
- observes only `IsEnabled`, `IsReadOnly`, and `Attachments` through a filtered
  `PropertyChanged` handler;
- updates the bindable attachment source through
  `GetValue(InputField.AttachmentsProperty)`;
- makes stepper installation idempotent;
- re-checks the stepper once when the handler is attached;
- relies on `ICommand.CanExecuteChanged` rather than assigning button
  `IsEnabled` repeatedly.
