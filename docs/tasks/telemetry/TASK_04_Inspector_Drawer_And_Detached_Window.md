# TASK 04 — Live Telemetry Inspector Drawer and Detached Window

## Goal
Create one reusable Inspector content control that can be shown while the user remains on Setup/Flight Data pages.

## Shared content
Create:

```text
LiveTelemetryInspectorView
LiveTelemetryInspectorViewModel
```

The content must not know whether it is hosted in a Drawer or Window.

## Drawer
Use Ursa/Avalonia patterns to host a right-side Drawer.

Requirements:
- current page remains visible/usable;
- resizable if practical;
- sensible min/max width;
- session remembers width/open state/selected panel;
- remains usable while navigating;
- vehicle selector when more than one vehicle exists.

Internal panels:

```text
Status | RC | Outputs | Power | Sensors | Raw
```

Header always shows:

```text
Vehicle
Connection
Mode
Arming state
Live/Frozen state
```

Commands:

```text
Live/Freeze
Clear Events
Add Marker
Detach
Close
```

## Detached Window
Desktop can detach the same Inspector content into an independent Window.

Do not create another diagnostic service or another telemetry subscription graph.

Browser/WASM:
- Drawer works fully;
- Detach is hidden/disabled unless supported.

## Tests
Cover:
- open/close;
- panel selection;
- selected vehicle;
- navigation while open;
- service remains active while closed;
- Browser composition without native Window.

## Acceptance
A reusable Inspector opens as a partial right-side overlay and can optionally detach on desktop.
