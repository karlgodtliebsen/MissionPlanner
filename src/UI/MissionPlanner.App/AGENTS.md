# UI conventions

## Dialog and drawer close buttons

For an X button that closes a dialog or drawer, use Ursa's close-button theme:

```xml
<Button Command="{Binding CloseCommand}"
        ToolTip.Tip="Close Inspector"
        AutomationProperties.Name="Close Inspector"
        Name="{x:Static u:DrawerControlBase.PART_CloseButton}"
        Theme="{DynamicResource OverlayCloseButton}"
        Width="32" Height="32" Padding="4">
    <mdi:MaterialIcon Kind="Close" Width="24" Height="24" />
</Button>
```

Use `xmlns:u="https://irihi.tech/ursa"` and the existing Material Icons namespace.
Adapt the command, accessible name, tooltip, and layout to the containing view.
The user has verified this pattern in `LiveTelemetryInspectorView.axaml`.
`DrawerControlBase.PART_CloseButton` is the accepted naming convention for now.

Let `OverlayCloseButton` manage the icon colors and interaction states. Do not
bind the icon foreground to a nearby title, hard-code its color, or substitute
the generic `BorderlessButton` theme. Do not add `ToolbarButton` to these X
close buttons.

## Copyable parameter listings in Markdown

When a document lists parameter names and values, use a fenced code block with
one assignment per line: `NAME = value` or `NAME = value // comment`.
This is a permanent user preference so selected/copied text can be pasted into
the Full Parameters List text editor. Do not use Markdown tables, bullets, or
separate inline-code cells for parameter assignments. Keep names and numeric
values literal; put annotations after `//`. Missing/unknown values and diagnostic
notes must be comment-only lines (`// ...`), never fabricated assignments.
Preserve safe code fences and normalize dynamic comments to one line. Compose
these documents in the presentation factory, not in the ViewModel or Core.
