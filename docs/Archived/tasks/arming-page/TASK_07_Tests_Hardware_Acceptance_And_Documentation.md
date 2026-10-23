# TASK 07 — Tests, Hardware Acceptance and Documentation

## Goal
Complete Arming with enough automated, SITL and physical-bench validation that it can be
trusted before the next MissionPlanner completeness/issue audit.

## Core configuration tests
Cover at minimum:
```text
ARMING_CHECK = disabled
ARMING_CHECK = all
ARMING_CHECK = custom metadata-backed mask
ARMING_RUDDER modes
ARMING_NEED_LOC present / absent
ARMING_REQUIRE present / absent
metadata enum labels/defaults/reboot flags
zero RC Arm/Disarm assignments
one RC Arm/Disarm assignment
multiple RC Arm/Disarm assignments
RC assignment move plan
RC assignment removal
FLTMODE_CH conflict
primary RCMAP conflict
occupied AUX conflict
armed Apply denial
connection boundary during Apply
partial parameter-write failure
readback mismatch
reboot aggregation
```

## Diagnostic regression tests
Preserve and extend existing tests for:
```text
DISARMED / READY
DISARMED / NOT READY
ArmRequested
ArmRejected
Armed
DisarmRequested
stale blocker expiry
historical PreArm separation
current multiple blockers
configured RC-switch inference
rudder/stick inference
no-request guidance
ACK accepted != Armed
heartbeat authoritative
multi-vehicle isolation
reconnect request-context reset
```

Do not weaken existing `ArmingRequestEvidenceTests`,
`VehicleArmingDiagnosticTests`, or telemetry-arming coverage.

## Document snapshot tests
Create deterministic status/diagnostic documents for:
```text
disconnected
unknown
ready
not ready — one blocker
not ready — multiple blockers
no recent request
RC switch request
MAVLink request
rejected arm request
ACK accepted / waiting for heartbeat
armed
disarm requested
historical PreArm only
pending configuration
multiple RC=153 assignments
unsupported/missing parameter capability
```

Verify escaping and the permanent copy convention:
```text
NAME = value
// comment
```

## ViewModel tests
Cover:
- Activate / Deactivate;
- initial disconnected state;
- parameter load completion;
- vehicle selection change;
- reconnect;
- current/pending values;
- Discard;
- metadata default reset;
- Apply;
- custom pre-arm bit choices;
- live RC switch movement;
- command availability;
- Arm command lifecycle;
- Disarm command lifecycle;
- operation cancellation;
- document regeneration only on semantic changes;
- no stale document after vehicle switch.

## Build/regression validation
At minimum run:
```text
Desktop build
Browser/WASM build
new Arming Core/UI tests
existing Compass Core/UI/document tests
git diff --check
```

Arming must not regress the shared Markdown or button styles established by Compass.

# SITL acceptance

Use ArduCopter SITL first.

Validate:

1. Navigate to Arming; no duplicate parameter download is started.
2. Current Information state matches heartbeat/SYS_STATUS.
3. DISARMED / READY is shown when current checks are healthy.
4. A real pre-arm blocker appears as current evidence.
5. `ARMING_CHECK` UI edits remain pending until Apply.
6. Discard restores the confirmed FC value.
7. Apply writes and verifies `ARMING_CHECK`.
8. `ARMING_RUDDER` round-trips semantic labels correctly.
9. RC Arm/Disarm assignment detects movement on a selected AUX channel.
10. `FLTMODE_CH` cannot be silently overwritten.
11. An AUX channel with another function cannot be silently overwritten.
12. GCS Arm requires confirmation, receives ACK, and waits for heartbeat confirmation.
13. GCS Disarm uses the existing command policy.
14. An ACK without heartbeat transition is not presented as final Armed/Disarmed.
15. Copy All / Copy Markdown work.
16. Disconnect/reconnect clears stale request context and restores current configuration.
17. Pending configuration is never presented as active FC state.

# Physical bench acceptance

**Props removed for all bench arming tests.**

On at least one real ArduPilot multirotor:

1. Compare all displayed Arming parameter values with Full Parameters.
2. Verify the current RC Arm/Disarm assignment.
3. Move the physical transmitter arm switch and verify the observed movement trace.
4. Assign/change an unused AUX channel and verify FC readback.
5. Verify the transmitter switch can arm/disarm according to the configured policy.
6. Verify GCS Arm and GCS Disarm from the new page.
7. Create one safe, reversible pre-arm blocker and verify the Information page explains it.
8. Restore the condition and verify the current blocker disappears rather than remaining stale.
9. Disconnect/reconnect and verify state is rebuilt from the new session.
10. Confirm no unrelated parameter changed during configuration writes.

Do not use force-arm during acceptance.

## Cross-family smoke tests
Use SITL where practical for:
```text
ArduCopter
ArduPlane
Rover
```

Confirm unsupported parameters are hidden/explained rather than represented as false/zero,
and metadata-derived choices match the connected firmware.

## Documentation
Create:
```text
docs/ARMING.md
```

Update:
```text
docs/SetupUxPattern.md
```

Update `docs/MarkdownDocuments.md` only if Arming introduces a genuinely reusable document
convention.

`docs/ARMING.md` should document:
- heartbeat authority for Armed/Disarmed;
- ACK vs observed state;
- current vs historical blockers;
- semantic mapping of edited parameters;
- `RCx_OPTION=153` assignment behavior;
- relation to Radio/Safety/Failsafe/Compass;
- normal GCS Arm/Disarm path;
- explicit exclusion of force-arm/bypass UX;
- hardware acceptance procedure.

## Implementation report
Add:
```text
docs/tasks/Arming/IMPLEMENTATION_REPORT.md
```

Include:
```text
files changed
architecture decisions
tests/builds actually run
SITL result
physical hardware result if actually performed
known limitations / deferred work
```

Do not claim hardware validation if Codex did not operate the hardware.

## Acceptance criteria
The Arming page is functionally coherent, test-covered, hardware-testable, and ready for
the user's subsequent application-wide list of incomplete functionality and discovered
issues.
