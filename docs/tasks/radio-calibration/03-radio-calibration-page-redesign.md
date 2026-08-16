# Codex Task 3 — Redesign MissionPlanner Radio Calibration Page

## Goal

Redesign the Radio Calibration page around:

- the reusable channel meter from Task 1;
- the corrected Capturing → Review → Writing workflow from Task 2;
- clear receiver/signal diagnostics;
- fast visual feedback for 16+ channels;
- a distinct MissionPlanner NextGen visual language.

The result may take general inspiration from good flight-controller configurators, but must not look like a Betaflight clone.

Use MissionPlanner/UraniumUI theme resources and existing design patterns.

---

## Current page

The supplied screenshot is:

```text
references/current-radio-calibration.png
```

The current page is functionally useful but mostly text:

```text
Channel 1 (Roll)     1491 us     1100/1500/1900
Channel 2 (Pitch)    1449 us     1100/1500/1900
...
```

Retain the useful raw values but make channel behavior immediately understandable visually.

---

## Proposed page structure

Design an adaptive layout approximately like:

```text
Radio calibration

[ safety warning ]

Receiver
  ● RC input live      16 channels      RSSI 83%
  Map: AETR            Vehicle: disarmed

CH 1  Roll      1491 µs   [──────●─────|────────]   1100  1500  1900
CH 2  Pitch     1449 µs   [─────●──────|────────]   1100  1500  1900
CH 3  Throttle  1100 µs   [──●─────────|────────]   1100  1500  1900
CH 4  Yaw       1508 µs   [──────|●────|────────]   1100  1500  1900
CH 5  Auxiliary  999 µs   [─●───────────────────]   LOW
...

[ state-specific instructions ]

[ Start calibration ]  [ Cancel ]
```

This sketch is conceptual only. Create a polished MissionPlanner layout rather than reproducing ASCII or another product.

---

## Receiver summary

Add a compact summary above the channel list.

Include information already available or safely derivable from the domain state:

```text
RC signal: Live / Stale / No signal
Channel count
RC RSSI, when available
Pilot channel map
Calibration state
Vehicle armed/disarmed status where appropriate
```

### RSSI semantics

Use RC receiver/input RSSI only when the data actually represents RC RSSI.

`VehicleRadioState.RssiPercent` derived from MAVLink `RC_CHANNELS.rssi` is appropriate when available.

Do **not** automatically label:

```text
LocalRssi
RemoteRssi
LocalNoise
RemoteNoise
TransmitBuffer
```

as ELRS receiver metrics unless the source MAVLink message/domain semantics prove that they are the RC link being calibrated.

Those fields may represent a telemetry radio link and would be misleading if shown as “ELRS RSSI”.

If RC RSSI is unknown (`255` / unavailable), show:

```text
RSSI —
```

or omit it, rather than displaying 0%.

---

## Channel map summary

Resolve:

```text
RCMAP_ROLL
RCMAP_PITCH
RCMAP_THROTTLE
RCMAP_YAW
```

into a concise summary.

Examples:

```text
AETR
TAER
```

only when the first four channel assignments truly form a known compact mapping.

Otherwise show the explicit mapping:

```text
Roll CH3 · Pitch CH1 · Throttle CH4 · Yaw CH2
```

If the mapping is invalid or duplicates a channel, show a warning and do not invent an AETR/TAER abbreviation.

Use ArduPilot's function assignments, not transmitter-model assumptions.

---

## Channel row

Each row should include:

```text
Channel number
Resolved function/name
Raw PWM
RadioChannelMeterView
Stored MIN / TRIM / MAX
Optional reversal indicator
Optional auxiliary state
```

The meter must consume structured values; do not generate the graphic by parsing the textual `Range` property.

Extend `RadioChannelInfo` / display ViewModel as needed so the row has direct access to:

```text
Minimum
Maximum
Trim
DeadZone
Reversed
FunctionName
Pwm
Stale/signal state
CapturedMinimum
CapturedMaximum
CandidateTrim
```

Prefer these structured properties over strings such as:

```text
"1100/1500/1900"
```

The formatted string can remain for accessibility/compact fallback if useful.

---

## Pilot channels

Make the four mapped pilot controls easy to distinguish without relying solely on channel numbers.

Bad assumption:

```text
CH1 is always Roll
CH2 is always Pitch
CH3 is always Throttle
CH4 is always Yaw
```

Correct approach:

```text
RCMAP_* determines the function
```

Use role labels/badges:

```text
ROLL
PITCH
THROTTLE
YAW
```

For Roll/Pitch/Yaw display their configured dead zone from `RCx_DZ` on the meter if available.

---

## Auxiliary channels

Auxiliary channels should remain continuous PWM bars.

Optionally provide a compact interpretation such as:

```text
LOW
MID
HIGH
```

when the PWM is near conventional switch positions.

Do not assume every auxiliary control is a three-position switch.

For values between those zones, display:

```text
Variable
```

or no discrete label.

This keeps knobs/sliders/6-position selectors honest.

Do not write any auxiliary-function parameters in this task.

---

## Calibration-state-specific UI

### NotStarted

Show:

- live channel bars;
- current stored min/trim/max;
- safety warning;
- Start calibration button.

Instruction:

```text
Turn on the transmitter, then start calibration.
```

If there is no fresh RC signal, Start should remain unavailable or produce a clear actionable message.

### Capturing

Show:

- current raw marker;
- captured minimum and maximum markers updating live;
- captured travel/range;
- strong instruction to move every required stick/control through its full range;
- Finish endpoint capture;
- Cancel.

Do not write parameters when the user selects Finish endpoint capture.

### Review

Show:

- captured min/max fixed;
- live current value;
- candidate trim/current marker;
- explicit instruction to center Roll/Pitch/Yaw and place throttle according to the policy from Task 2;
- per-channel validation state;
- Write calibration / Confirm and write;
- Restart endpoint capture if appropriate;
- Cancel.

This stage should make it visually obvious if a centered axis is still far from center.

### Writing

Disable controls that could conflict with the operation.

Show progress/status but continue displaying the captured values.

### Success

Show a concise summary such as:

```text
Calibration written and verified.
16 channels observed; 8 calibrated.
```

Use the actual implementation semantics for which channels were written.

Optionally offer:

```text
Recalibrate
```

### Failed / Disconnected / Cancelled

Keep the captured context visible when useful.

Show the cause and clear recovery action.

Do not replace the page with an empty error state.

---

## Safety message

Retain a prominent safety message similar to:

```text
Remove propellers and keep the vehicle disarmed.
Turn on the transmitter before calibrating.
```

Use the project's warning component/theme conventions.

Do not rely only on orange/red text.

If armed state changes to armed while calibration is active, the domain/service must already prevent writing; the page should also make the condition obvious.

---

## Live update performance

Preserve the existing efficient behavior where channel display ViewModels are updated in place.

Do not:

- clear/re-add all channel rows per MAVLink packet;
- recreate GraphicsViews every 200 ms;
- re-run parameter reads for each visual frame.

The channel list should be driven by the current RC telemetry already provided by the domain service.

Parameter values such as `RCx_MIN/MAX/TRIM/DZ` should come from the parameter cache/read model, not repetitive live `PARAM_REQUEST_READ` traffic unless the current architecture requires a targeted read.

---

## Responsive/adaptive layout

Desktop/tablet:

- readable channel name + PWM + meter + limits on one row;
- 16 channels should fit in a practical scroll area;
- align meter rails vertically across rows.

Phone/narrow width:

- allow each row to use two lines, e.g.:

```text
CH1 Roll                         1491 µs
[────────────────●──────────────]
1100               1500      1900
```

Do not force desktop column widths onto a phone.

Use existing MissionPlanner adaptive-layout conventions.

---

## Theme / distinctive visual identity

Create a MissionPlanner look.

Suggestions:

- rounded but restrained rails;
- subtle central reference;
- current-value indicator visually stronger than stored/captured markers;
- captured endpoint ticks shaped differently from configured ticks;
- role badges rather than Betaflight-style row labels;
- MissionPlanner typography and spacing.

Avoid:

- copied Betaflight colors;
- copied Betaflight exact bar geometry;
- copied icons;
- copied arrangement.

The semantic ideas are generic; the final UI should be original.

---

## Accessibility

Each row should have a meaningful semantic description, e.g.:

```text
Channel 1, Roll, current 1491 microseconds,
minimum 1100, trim 1500, maximum 1900.
```

During calibration include captured endpoints where practical.

Ensure keyboard/focus navigation works on desktop.

Do not require color perception to understand:

```text
current
minimum
maximum
trim
warning
no signal
```

---

## Tests

Add focused ViewModel/model tests where practical.

Cover at least:

1. AETR map summary.
2. TAER map summary.
3. nonstandard map falls back to explicit labels.
4. duplicate mapping shows an issue.
5. pilot roles follow RCMAP rather than channel number.
6. DeadZone maps to the correct pilot channel.
7. current range data is structured and does not depend on parsing `Range`.
8. fresh/stale/no-signal state maps correctly to the UI model.
9. RC RSSI unknown is not rendered as 0%.
10. Capturing exposes captured min/max.
11. Review exposes fixed extrema and current/candidate trim.
12. channel ViewModels are reused when only values change.
13. a changed channel count/order safely rebuilds the collection.
14. auxiliary stepped label does not force intermediate values into Low/Mid/High.

Avoid fragile pixel/screenshot tests unless already supported.

---

## Hardware acceptance test

Use a real CRSF/ELRS receiver and transmitter.

Suggested test:

1. Connect MissionPlanner to the FC.
2. Confirm fresh 16-channel RC input.
3. Move Roll, Pitch, Throttle and Yaw and verify the correct **mapped functions** move.
4. Operate several switches and one proportional/slider channel if available.
5. Start calibration.
6. Move every intended control to both extremes.
7. Confirm captured endpoint ticks remain visible.
8. Enter Review.
9. Center Roll/Pitch/Yaw; place throttle as instructed.
10. Confirm visual candidate trims.
11. Write calibration.
12. Read back `RCx_MIN/MAX/TRIM`.
13. Disconnect/turn off transmitter and verify the UI becomes Stale/No signal without jumping to fake zero values.
14. Reconnect transmitter and confirm bars recover without reopening the page.

Document the observed RadioMaster Pocket / ELRS behavior in the task completion note, but do not hard-code RadioMaster/ELRS assumptions into the generic calibration domain.

---

## Acceptance criteria

Complete when:

- all live channels have responsive visual meters;
- current raw PWM remains numerically visible;
- configured min/max/trim and captured endpoints are distinct;
- pilot channel roles follow RCMAP;
- channel-map summary is useful and honest;
- deadband can be shown for Roll/Pitch/Yaw;
- auxiliary channels can show optional stepped interpretation without pretending all AUX channels are switches;
- RC signal state and RSSI are semantically correct;
- no-signal/stale mode remains readable;
- Capturing and Review stages are visually distinct;
- UI remains smooth with 16 channels;
- the page is adaptive and theme-aware;
- the design is recognizably MissionPlanner rather than a Betaflight copy;
- automated tests and the real-ELRS acceptance pass.
