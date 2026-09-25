# Arming setup

Arming is the second full semantic Setup consumer after Compass. The page presents a
persistent diagnostic summary, Information reports, and a Configuration tab. It does
not replace Radio, Safety, Failsafe, Compass, Battery or Full Parameters.

## Authority and evidence

`IVehicleLiveDiagnostics.GetArming` remains the sole arming diagnostic projection.
Heartbeat is authoritative for Armed/Disarmed. An accepted command ACK is not proof
of a heartbeat transition. The page presents typed command results separately from
observed state and uses the existing diagnostic request stage. It does not send force
arm values, skip checks, or implement another ACK tracker.

Only current diagnostic `Reasons` appear as current blockers. Last failure, historical
PreArm text/time, command source/time and ACK remain historical evidence. Absence of
an observed request is explained as such, never as a rejection. Readiness describes
currently enabled checks; it does not certify that every firmware check is enabled.

## Configuration

`IArmingConfigurationService` projects the existing parameter registry and metadata.
It waits for the existing parameter load to complete and never requests a second full
parameter download. Refresh reprojects the cached evidence and metadata. A one-second
visible-page refresh also catches parameter-load completion and diagnostic expiry.

| Semantic setting | Firmware evidence | Behavior |
| --- | --- | --- |
| Pre-arm checks | ARMING_CHECK | Unknown, Disabled, All, Custom. Bit zero means All; custom edits exclude it. |
| Stick arming | ARMING_RUDDER | Disabled, Arm only, Arm and disarm; editor choices come from metadata. |
| Require location | ARMING_NEED_LOC | Off/On when reported; missing is unknown/unsupported, not Off. |
| Arming requirement | ARMING_REQUIRE | Firmware metadata choices, not a Copter-specific assumption. |
| RC arm switch | RC1_OPTION through RC16_OPTION | Function 153, with explicit None/one/multiple/unknown state. |

Custom check choices come only from metadata. Unsupported choices are not invented.
Defaults and reboot requirements also come from metadata. ARMING_OPTIONS,
ARMING_MIS_ITEMS, other ARMING_* settings, pilot channel mappings and flight-mode
channel values remain advanced evidence instead of arbitrary parameter editors.

Changing a control stages local values. Current, default, pending, conflict and reboot
information remain distinct. Discard returns to the current confirmed configuration.
Apply requires explicit review, including a prominent warning when disabling all
checks. A custom subset is never labeled safe merely because some checks remain.

The review captures the connection and firmware identity. Apply revalidates current
values, metadata, RC conflicts, online/disarmed state and operation ownership. It uses
the existing `IParameterEditSession` through `IDomainFactory`, writes deterministically,
stops on the first failed verification, and retains unconfirmed edits. Readback remains
authoritative. Connection/identity/selection boundaries cancel page-owned operations
and invalidate local edits/reviews. A fresh review is required in the new session.

## Radio ownership

Arming and Radio reuse `RadioArmingConfiguration.Conflict` and the shared function-153
constant. Radio retains its RC-centric controls; its assignment path now also acquires
the shared vehicle operation gate. Confirmed registry writes appear in either page
when its existing refresh runs.

Moving a single assignment clears its old option before assigning the new channel.
Flight-mode channels, primary RCMAP channels and occupied unrelated AUX functions are
rejected. Multiple assignments are displayed intact and require explicit cleanup in
Full Parameters; simple reassignment is blocked. No implicit cleanup occurs.

The page reuses `RadioSwitchMovement` with fresh radio samples. Endpoint transitions
are bounded and describe apparent switch movement only. Because the page samples
once per second, rapid transitions between samples can be missed; hold each endpoint
for at least one refresh during bench verification. Movement is not proof of transmitter
intent or successful arming.

## Guarded GCS actions

Arm always requires a Setup-page confirmation after `IVehicleCommandPolicy` permits
it. Disarm honors the same policy and its hazardous-disarm confirmation requirement.
Policy, connection, firmware and replay state are checked again after confirmation.
The existing typed `IVehicleCommandService` owns transmission, operation gating and
ACK correlation. No command can complete its presentation into a different selected
vehicle. Replay prohibits sends and configuration writes.

## Documents and navigation

`ArmingSetupDocumentFactory` has no I/O and escapes all dynamic prose. Confirmed
parameters use fenced `NAME = value` lines; missing/partial parameter sets produce
comment-only evidence. Pending edits never appear as active assignments. Unchanged
semantic content preserves document instances and text selection.

`InformationDocumentView` owns rendering, theme behavior and native selection/copy.
The removed Copy All / Copy Markdown buttons are not restored. Configuration actions
use native controls, not Markdown links. Full Parameters uses the normal navigation
service and confirms discarding local edits. Blocker explanations refer users to the
owning Radio, Safety, Failsafe, Compass or Battery subsystem.

## Acceptance procedure

Automated tests do not establish physical vehicle safety or compatibility. Record the
firmware version, vehicle family, board, parameter metadata and connection for each run.

### ArduCopter SITL, followed by Plane/Rover smoke tests

1. Open Arming during parameter loading; confirm no duplicate full download.
2. Compare armed state, readiness and current blockers against heartbeat/SYS_STATUS.
3. Introduce and clear a reversible blocker; verify its history is not a current blocker.
4. Stage/Discard/Apply checks, stick mode and supported requirements; compare readback.
5. Stage an RC move/clear; inspect every write. Reject flight-mode, primary and occupied channels.
6. Exercise normal GCS Arm confirmation and policy-controlled Disarm. Distinguish ACK
   from heartbeat, including accepted ACK without an observed transition.
7. Disconnect/reconnect and switch vehicles during confirmation and Apply. Confirm
   old operations do not update the new page or send to the newly selected vehicle.
8. Copy a parameter block with the renderer's native copy action into Full Parameters.
9. Check unsupported settings and defaults on Plane/Rover without guessing capabilities.

### Physical bench, props removed

1. Remove all propellers and keep the vehicle stationary with the motor area clear.
2. Compare displayed values with Full Parameters before edits.
3. Verify the arm-switch assignment and low/high/low movement, holding each endpoint.
4. Assign an unused AUX channel and verify exact readback and unchanged unrelated values.
5. Verify transmitter and normal GCS arm/disarm behavior through the configured policy.
6. Introduce/restore one safe reversible blocker and verify current versus historical reports.
7. Disconnect/reconnect and verify a fresh session and parameter configuration.
8. Record results; never use force-arm to complete acceptance.

SITL and physical results for this implementation are recorded separately in
`docs/tasks/Arming/IMPLEMENTATION_REPORT.md`.
