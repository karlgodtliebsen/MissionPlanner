# Readiness and shared diagnostic navigation

## Structured readiness

Readiness replaces the Flight Data Preflight tab. `ReadinessView` uses a typed
`ReadinessCheckItem` template instead of converting `PreflightCheckResult` records
to strings. Rows explain the result, observations, units, evidence timestamp and
vehicle. Failures precede warnings and unknowns; passed checks are collapsed.
NotAvailable maps to Unknown, Stale to Unknown — stale evidence, and the explicit
NotApplicable state is distinct from success. Evidence and configuration buttons
are keyboard accessible. Battery configuration opens Optional Hardware → Battery
Monitors; other parameter checks open the Parameters Editor, and FC readiness
opens Arming. These links only navigate. An inspection of a different vehicle
cannot silently change the operational vehicle or open its configuration.

`PreflightAssessmentService` is shared by the Readiness panel, the persistent
Flight Data summary, and Inspector's Power section. FC-reported arming readiness
is a separate row from local checks; a local voltage pass is never FC approval.
The old compatibility Preflight view also has an explicit typed template.

## Battery root causes and rules

The former battery rule accepted any positive voltage when percentage was at
least 20. Thus 0.02 V / 99% could pass without checking received arming thresholds.
The handwritten BATTERY_STATUS decoder also used incorrect offsets for instance,
current and extension fields. It now follows the
[MAVLink common wire layout](https://raw.githubusercontent.com/mavlink/c_library_v2/master/common/mavlink_msg_battery_status.h),
including zero-extension of truncated MAVLink 2 scalar fields.

Battery samples are keyed by zero-based MAVLink instance ID, with an independent
timestamp for every instance. User-facing Battery 1 corresponds to ID 0. Power
rail updates and secondary battery updates cannot freshen Battery 1. Invalid new
voltage/percentage readings replace previous values instead of silently retaining
them with a new timestamp. Instance-specific BATTERY_STATUS takes precedence over
the ambiguous SYS_STATUS summary. With multiple enabled monitors, that summary
alone cannot establish Battery 1 readiness.

`BatteryReadiness` uses the received BATT[_n] monitor, ARM_VOLT, LOW_VOLT and CRT_VOLT
parameters (actual names are BATT_MONITOR / BATT2_MONITOR etc.). No fallback voltage
threshold is invented. An explicit MONITOR=0 means Not applicable, an unknown
monitor or unavailable/invalid voltage means Unknown, and voltage below a known
critical/arming threshold means Failed. Below the low threshold means Warning.
A positive arming threshold is required for a local Passed result; zero disables
that threshold and cannot establish readiness. Percentage is displayed as reported
telemetry, never as verified state of charge or a passing criterion.

Battery telemetry is fresh for 10 seconds, and requires an online connection with
a heartbeat within 5 seconds. Current same-session FC battery PreArm/Arm text takes
precedence over otherwise healthy measurements for 30 seconds. Older unresolved
text becomes **historical, recovery unconfirmed / Unknown**, not an eternal current
failure and not a Pass. Silence, dismissing messages and elapsed time do not resolve
it. A strictly later fresh armed heartbeat or enabled, present and healthy FC
pre-arm health bit resolves it; that resolution and its timestamp remain visible.
This confirms only what the FC reports for its configured checks.

Evidence is isolated by vehicle and battery number. Unnumbered battery text is a
separate unknown-instance FC check; it is never arbitrarily assigned to Battery 1.
Reconnect clears current-session battery reasons while retaining the historical
event journal. Late text from before the new session cannot recreate a current
battery blocker. Historical messages remain available in Messages/Inspector.

GPS is not universally required: Copter Stabilize, Acro and Alt Hold are explicitly
not applicable for the GPS-position prerequisite. Known position modes show GNSS
evidence as advisory because an alternative position source may be in use; other
vehicle/mode profiles remain Unknown until their requirement is established. This
follows the distinction between GPS and position information in the
[Copter flight-mode documentation](https://ardupilot.org/copter/docs/flight-modes.html).
Missing estimator evidence stays Unknown; disabled sensor bits are not universally
treated as failures. These local checks are not an exhaustive flight-release checklist.

## Shared host and scope

Labelled Readiness, Messages, Inspector and Logs actions are available in the
topbar. Narrow windows use the Diagnostics menu. The first three share one panel;
Logs opens the existing recording/playback workspace. The panel's width control
is in expanded technical details; its width is clamped to the host on small windows.
Flight Data retains a clickable active-vehicle readiness summary and its unrelated
specialised tools. Messages, Preflight and detailed Status no longer occupy its tabs.

The panel follows the active vehicle unless explicitly pinned. Selecting another
vehicle pins it; both inspected and active identities are then shown. Follow active
vehicle removes the pin. Evidence links retain their originating vehicle. Switching
vehicles clears old displayed samples, including paused Raw data. Inspecting never
changes operational command targeting. The normal header uses available domain
identity (currently a derived type/SysID fallback; no aircraft nickname is fabricated),
endpoint, connection, mode and FC arming summary. Firmware, exact timestamps and
export scope live in an expander. Pausing freezes displayed diagnostics, not telemetry
acquisition or recording. Connection loss and replay remain explicitly labelled.

Readiness's badge counts current failed checks for the active operational vehicle;
FC and local checks may describe overlapping causes. Unknown/stale checks are not
counted as failures. Messages' badge counts retained unread FC Warning-or-higher
messages for the inspected vehicle (active vehicle when following). **Mark current
warnings read** acknowledges that retained range only; it never clears arming evidence.

Messages reuses the existing severity/search filters, selection, copy and exports,
and includes the diagnostic mode/command event timeline. Stable rows avoid replacing
the whole list on every append. Exports include selected vehicle, capture timestamp
and filter scope; the JSON message export is now a versioned object with a `messages`
array. Inspector retains markers, event copy, raw filtering and payloads. Status is
the concise technical summary; Decoded hosts the existing searchable promoted-field
table and its JSON snapshot. Raw provides retained packet payloads. This does not
add decoding for protocol fields that are not already promoted by the application.

Views stay mounted when switching destinations, preserving view state. Messages and
Status use one session-owned presentation each, with guarded activation and disposed
subscriptions. Inspector owns the existing coalesced timer; badge assessments update
once per second even while the panel is closed. There is no second readiness engine
or additional packet subscription per destination. Display times include local UTC
offsets; exports retain full DateTimeOffset timestamps.

## Validation and boundaries

Focused tests cover wire offsets/truncation, 0.02 V with 99%, instance isolation,
missing/invalid/stale/disabled evidence, positive recovery, reconnects, rail-update
freshness, vehicle switching, pinning, badges, export isolation and panel lifecycle.
Compiled UI tests exercise mixed results at 360 and 700 pixels with production light
and dark themes, plus all shared destinations and technical subsections. Offline
screenshots are generated when MISSIONPLANNER_VISUAL_TEST_OUTPUT is set.

No live FC was connected or commanded for validation. Physical battery accuracy,
actual hardware recovery, OS-window dragging and browser runtime behaviour require
follow-up manual validation. No flight-controller settings or transmitter settings
are changed by this implementation work.

Final offline validation: the affected application build completed successfully;
46 focused Core tests and 8 UI/navigation/rendering tests passed. `git diff --check`
reported no whitespace errors. Existing compiler/analyzer warnings remain; the
full solution and hardware integration suites were not run.
