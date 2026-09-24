# TASK 02 — Pending Changes, Apply/Discard and RC Arm-Switch Assignment

## Goal
Give Arming the Compass-style current/pending/review/apply workflow, including RC
Arm/Disarm assignment.

## Pending edits
Do not write on control changes. Track:
```text
current
pending
default
dirty
validation
reboot requirement
```
`ArmingChangeSet` must show every parameter change before Apply.

## ARMING_CHECK
Support All / Disabled / Custom. Disabling all checks is safety-significant: attach an
explicit warning requiring review/confirmation before writing. Never label a custom subset
safe merely because some checks remain enabled.

## Other semantic settings
Use metadata-backed values for `ARMING_RUDDER`, `ARMING_NEED_LOC`, and `ARMING_REQUIRE`
when present. Unknown defaults remain Unknown.

## RC assignment
Reuse/refactor `RadioArmingConfiguration` conflict logic; do not duplicate it.

Represent zero, one, or multiple current `RCx_OPTION=153` assignments accurately.

Changing RC8 to RC10 should stage an explicit plan such as:
```text
RC8_OPTION  153 -> 0
RC10_OPTION   0 -> 153
```
Nothing writes until reviewed Apply.

Never overwrite:
- `FLTMODE_CH`
- primary mapped roll/pitch/throttle/yaw channels
- a non-zero unrelated `RCx_OPTION`

Validate option 153 against firmware metadata when metadata exists.

Multiple current Arm/Disarm assignments must not be collapsed to one. Support explicit
cleanup if practical; otherwise block simple reassignment with a clear explanation.

## Live movement
Reuse `RadioSwitchMovement`:
```text
RC8: 999 -> 2000 -> 999
Appears to be a two-position switch.
```
Movement proves input only, not transmitter intent or successful arming.

## Apply
Use existing parameter edit/readback + vehicle operation gate:
1. revalidate vehicle/connection/firmware/current values;
2. require online and disarmed;
3. recompute conflicts;
4. apply deterministic write plan;
5. stop on first unverified failure;
6. verify readback;
7. leave unconfirmed edits pending;
8. aggregate reboot requirement.

Reconnect must reread and detect stale reviews before Apply. Vehicle/identity changes
invalidate the pending plan.

## Tests
Cover Apply/Discard/default, RC move/clear, multiple assignments, occupied destination,
flight-mode/primary-channel conflicts, armed Apply denial, partial failure, readback
mismatch and reconnect conflict.
