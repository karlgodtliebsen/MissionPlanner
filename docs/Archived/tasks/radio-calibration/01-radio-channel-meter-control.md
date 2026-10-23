# Codex Task 1 — Reusable Radio Channel Meter / Calibration Bar

## Goal

Create a reusable, high-performance Avalonia control for displaying one RC input channel as a horizontal meter.

The control will replace the current text-only representation in Radio Calibration and should be reusable later in other MissionPlanner pages that need live RC-channel visualization.

Take inspiration from modern flight-controller configurators, but create a **MissionPlanner-native visual design**. Do not copy Betaflight assets, layout, palette, dimensions, or implementation.

The control must represent ArduPilot RC semantics accurately.

---

## Inspect first

Locate the current implementation in the active branch. In the supplied source snapshot the relevant files are approximately:

```text
src/UI/AvaloniaUI/MissionPlanner.AvaloniaUI.App/Views/InitSetup/MandatoryHardware/Sections/RadioSetupView.axaml
src/UI/AvaloniaUI/MissionPlanner.AvaloniaUI.App/Views/InitSetup/MandatoryHardware/Sections/RadioSetupViewModel.cs

src/Core/MissionPlanner.Core/Setup/RadioChannelInfo.cs
src/Core/MissionPlanner.Core/Setup/RadioChannelsView.cs
src/Core/MissionPlanner.Core/Setup/RadioCalibrationSnapshot.cs
src/Core/MissionPlanner.Core/Setup/RadioChannelCapture.cs
```

Also inspect existing custom-drawing patterns in the application, for example controls based on:

```text
Avalonia custom controls
`Render(DrawingContext)` overrides
```

Use the current project conventions rather than introducing a new rendering framework.

The current screenshot is supplied with this task set:

```text
references/current-radio-calibration.png
```

It demonstrates why the visual domain must allow values outside the configured 1100–1900 range: live channels can legitimately be observed around 880, 999, 2000 and 2001 µs.

---

## Required control

Create a reusable control with a name consistent with the current project naming style, for example:

```text
RadioChannelMeterView
RadioChannelBarView
```

Prefer a lightweight Avalonia custom control if that fits the existing rendering patterns.

Do not implement the bar as many nested Grids/Boxes whose widths are rebound at RC update frequency unless measurements demonstrate that this is preferable.

### Suggested bindable/input properties

The exact API may vary, but the control needs enough information to render:

```text
Pwm
DisplayMinimum
DisplayMaximum

ConfiguredMinimum
ConfiguredMaximum
Trim

DeadZone

CapturedMinimum
CapturedMaximum

IsCapturing
IsStale
HasSignal

ChannelKind / PresentationKind
IsReversed
```

Reasonable defaults:

```text
DisplayMinimum = 800
DisplayMaximum = 2200
```

Do not use the configured `RCx_MIN` and `RCx_MAX` as the outer clipping bounds.

A real live PWM value below the configured minimum or above the configured maximum must still remain visible.

Clamp only the rendered pixel position at the extreme visual rail if the raw PWM exceeds the display domain.

---

## Visual semantics

A single channel row should support these independent layers.

### 1. Outer rail

A stable horizontal rail representing the visual PWM domain.

Conceptually:

```text
800                  1500                  2200
|----------------------|----------------------|
```

The rail should remain fixed while a user moves a stick.

Do not resize/recenter the rail based on the current PWM.

### 2. Configured minimum / maximum

Render the currently stored ArduPilot:

```text
RCx_MIN
RCx_MAX
```

as subtle rail/tick markers.

They are configuration, not the current calibration capture.

### 3. Trim marker

Render:

```text
RCx_TRIM
```

as a distinct marker.

For centered axes this will often be near 1500, but never assume exactly 1500.

The marker must use the actual parameter value.

### 4. Nominal 1500 reference

For centered-axis channels, optionally show a low-emphasis reference at 1500 µs.

This is a reference only and must not be confused with `RCx_TRIM`.

Throttle and auxiliary channels may use a different presentation where the 1500 reference is less prominent.

### 5. Current raw input

Render a clear current-value indicator from the live raw PWM.

Keep the numeric PWM visible in the row:

```text
1491 µs
```

The numeric value must remain raw/current even if visual animation is added.

### 6. Calibration-captured extrema

While calibration is active, draw:

```text
CapturedMinimum
CapturedMaximum
```

as clearly distinguishable endpoint markers.

These represent the extrema discovered during the current calibration session, not the stored `RCx_MIN/MAX`.

The markers should remain at their discovered positions as the current value moves.

### 7. Deadband

For Roll, Pitch and Yaw, support a deadband highlight derived from:

```text
RCx_DZ
```

centered around the effective trim/reference as appropriate to ArduPilot semantics.

Do not display a deadband merely because a channel number is 1–4. Use resolved channel function/mapping.

### 8. Reversed state

Expose channel reversal clearly, but do not silently mirror the raw PWM scale unless the underlying UI requirement explicitly calls for a logical-command view.

For calibration the physical/raw PWM is the primary truth.

A compact `REV` marker/icon or accessible label is preferable.

---

## Analog vs auxiliary/switch presentation

Support at least these presentation categories:

```text
CenteredAxis
Throttle
Auxiliary
```

Do not infer “switch” solely because a channel number is above 4.

Auxiliary channels may be switches, sliders, knobs, rotary selectors, etc.

For Auxiliary channels, the control/page may optionally overlay useful Low/Mid/High state zones:

```text
LOW       MID       HIGH
```

but must continue to show the continuous raw PWM position.

If the value is not near a defined stepped state, present it as a variable/intermediate value rather than forcing it to one switch state.

Keep the model extensible enough that a later task can provide explicit switch thresholds/functions.

---

## Animation / smoothing

A small amount of visual interpolation is acceptable to improve legibility, but:

- calibration capture must always use raw incoming values;
- the numeric PWM must always show the raw incoming value;
- smoothing must affect only the visual current marker/fill;
- latency must remain very low;
- a large stick movement must not visibly “lag” behind the transmitter.

Prefer approximately one display-frame interpolation rather than a long animation.

Do not spawn a new long-running animation for every RC packet.

If the simpler non-animated implementation is smoother and more reliable across Avalonia targets, prefer it.

---

## Stale and no-signal states

The bar must remain useful when RC input disappears.

### Fresh signal

Normal current marker and value.

### Stale

Keep the last known value visible but visually muted and label the state as stale.

### No signal / no channel data

Show the rail, configured values and labels, but remove/disable the live-current indicator.

Do not collapse the row and do not show a misleading zero PWM.

The UI must communicate signal state using text/iconography in addition to color.

---

## Theme and accessibility

Use MissionPlanner theme resources / Ursa conventions.

Requirements:

- work in light and dark themes;
- sufficient contrast;
- no meaning communicated only through red/green color;
- semantic/accessibility description such as:
  `Channel 1 Roll, 1491 microseconds, trim 1500`;
- allow platform text scaling without destroying the row;
- avoid hard-coded Betaflight-like colors.

---

## Performance

RC channels are live telemetry and may update several times per second.

The page can display 16 or more rows.

Requirements:

- do not recreate the channel collection on every RC packet;
- do not recreate the custom drawable for every value change;
- update existing channel row ViewModels in place;
- invalidate only the affected custom control when possible;
- avoid per-frame allocations in `Draw`;
- cache pens/geometry/state where practical;
- no Task/Timer per channel solely for ordinary rendering.

The current `RadioSetupViewModel.RefreshLiveChannels()` behavior that reuses row ViewModels when channel identity/order has not changed is desirable and should be preserved.

---

## Geometry extraction and tests

Extract the PWM-to-position math into a small testable helper if it keeps the control clean.

Cover at least:

```text
800 -> left rail
1500 -> center of an 800..2200 rail
2200 -> right rail
values below/above domain -> clipped position
configured min/max positions
trim position
captured min/max positions
dead-zone boundaries
```

Also verify that:

```text
Pwm = 880
ConfiguredMinimum = 1100
```

still renders the current marker to the left of the configured-minimum marker rather than clipping both to the same place.

Do not build pixel-perfect screenshot tests unless the repository already has an established mechanism.

---

## Acceptance criteria

Complete when:

- a reusable MissionPlanner radio meter control exists;
- raw PWM, configured min/max, trim and current calibration extrema are visually distinct;
- display supports approximately 800–2200 µs by default;
- current values outside configured min/max remain visible;
- centered-axis deadband can be shown;
- stale/no-signal states are explicit;
- the control works with dark/light theme;
- RC updates do not cause collection churn or obvious UI latency;
- geometry is covered by focused tests;
- the existing Radio Calibration page can consume the control in Task 3 without additional rendering infrastructure.
