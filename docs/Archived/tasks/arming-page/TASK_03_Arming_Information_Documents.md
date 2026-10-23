# TASK 03 — Arming Information and Diagnostic Documents

## Goal
Use the established application-document pattern for Arming.

```text
Core produces evidence.
Application produces explanation.
UI renders UserDocument through InformationDocumentView.
```

Do not use LiveMarkdown directly from `ArmingPage` or `ArmingViewModel`.

## Add `IArmingSetupDocumentFactory`
Suggested API:
```csharp
UserDocument CreateStatus(ArmingDocumentContext context);
UserDocument CreateDiagnostics(ArmingDocumentContext context);
```
No I/O in the factory.

## Status document
Immediately answer:
- Is the vehicle armed?
- Is it ready to arm?
- If not, why?
- Was an arm request observed?
- What happened to the last request?
- How is arming configured?

Use `VehicleArmingDiagnostic` as evidence.

Example:
```markdown
## Arming status

**DISARMED / READY**

All currently enabled pre-arm checks are passing.

### Arming methods
- RC Arm/Disarm switch: `RC8`
- Stick arming: **Arm and disarm**
- GCS arm command: available

### Last arming activity
No recent arm request was observed.
```

## Current vs historical
Only current `Reasons`/readiness belong under current blockers.

Keep these visibly historical:
```text
LastArmFailure
LastPreArmReason
LastPreArmReasonAt
LastArmAttemptAt
LastArmCommandSource
LastArmAck
```

Never recycle a stale `PreArm:` message as a current blocker.

## ACK vs heartbeat
An ACCEPTED command ACK does not mean Armed. If heartbeat has not confirmed it, say:
```text
Arm acknowledged; waiting for armed heartbeat.
```

## No-request case
Preserve the existing distinction:
```text
No recent arm request was observed.
Check Arm/Disarm RC assignment, transmitter mapping, or stick arming.
```
Do not call this rejection.

## Configuration evidence
Summarize semantic values:
```text
Pre-arm checks: All
Stick arming: Arm and disarm
Require location: Off
Arm/Disarm RC: RC8
```

## Copyable parameter evidence
Follow `docs/MarkdownDocuments.md`:
```text
ARMING_CHECK = 1
ARMING_RUDDER = 2
ARMING_NEED_LOC = 0
RC8_OPTION = 153
FLTMODE_CH = 5
```
Only current FC values become assignments. Missing/non-parameter evidence is a comment.

## Diagnostic document
Include detailed evidence:
- diagnostic stage;
- readiness;
- current blockers;
- request source/time;
- ACK;
- historical PreArm evidence;
- RC movement evidence;
- relevant metadata/source notes.

## Pending values
If there are unapplied edits, explicitly state that they are pending and not active.

## Tests
Golden/snapshot cases:
disconnected, unknown, ready, not-ready, multiple blockers, no request, RC request,
MAVLink request, rejected, ACK accepted awaiting heartbeat, armed, disarm requested,
historical PreArm only, pending edits, multiple RC assignments, escaping.

## Acceptance
The Information tab answers "Why isn't it armed?" and is useful when copied into an LLM
or issue report.
