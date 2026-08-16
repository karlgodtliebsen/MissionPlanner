# Codex Task 2 — Correct Radio Calibration Review / Trim Workflow

## Goal

Strengthen the radio calibration workflow so endpoint discovery and trim capture are two distinct stages.

The current implementation already defines:

```text
RadioCalibrationState.Review
```

but the state is not used.

Use it.

The workflow should be:

```text
NotStarted
    ↓
Capturing        discover channel minimum/maximum
    ↓
Review           user returns controls to correct neutral positions
    ↓
Writing          write and verify RCx_MIN/MAX/TRIM
    ↓
Success
```

with existing failure, cancel and disconnect paths retained.

This task is functional/domain work, not primarily a visual redesign.

---

## Why this matters

During endpoint capture the user deliberately moves sticks and switches to their extremes.

The channel's `Current` value at the instant the user clicks Finish may therefore be an arbitrary extreme.

Do not use that arbitrary last endpoint value as the final trim.

The calibration flow must explicitly stop endpoint capture, instruct the user to place the transmitter in the correct neutral configuration, obtain a fresh RC sample, review/validate it, and only then write the parameters.

---

## Inspect first

Current supplied-snapshot files:

```text
src/Core/MissionPlanner.Core/Setup/IRadioCalibrationService.cs
src/Core/MissionPlanner.Core/Setup/RadioCalibrationService.cs
src/Core/MissionPlanner.Core/Setup/RadioCalibrationSnapshot.cs
src/Core/MissionPlanner.Core/Setup/RadioCalibrationState.cs
src/Core/MissionPlanner.Core/Setup/RadioChannelCapture.cs
src/Core/MissionPlanner.Core/Setup/RadioChannelInfo.cs

src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/Sections/RadioSetupViewModel.cs
src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/Sections/RadioSetupView.xaml

src/Tests/MissionPlanner.Core.Tests/RadioSetupTests.cs
```

Historical reference only:

```text
src-v.1.38/GCSViews/ConfigurationView/ConfigRadioInput.cs
```

Do not edit `src-v.1.38`.

Review the current ArduPilot radio-calibration documentation before changing write semantics.

---

## Required workflow API

Refactor the service API so “finish moving endpoints” does **not** immediately erase the capture state and write parameters.

Choose clear names that fit the repository, for example:

```csharp
Task FinishCaptureAsync(...);
Task ConfirmAndWriteAsync(...);
```

or:

```csharp
Task EnterReviewAsync(...);
Task CompleteAsync(...);
```

The exact naming is less important than preserving this semantic boundary:

```text
Capturing -> Review
```

must not write parameters.

```text
Review -> Writing
```

is the point at which the final trim sample is taken and the write begins.

Keep UI-specific dialog logic out of the core service.

---

## Capturing stage

While `Capturing`:

- continue tracking the minimum and maximum raw PWM observed per active channel;
- keep capture extrema stable as the current PWM changes;
- update the snapshot for live UI;
- retain current range validation.

When the user selects “Finish endpoint capture”:

1. stop extending the endpoint extrema;
2. validate captured travel;
3. transition to `Review`;
4. do **not** write `RCx_*` parameters yet.

If travel validation fails, keep the user in a state where they can retry rather than silently writing partial calibration.

---

## Review stage

The snapshot must contain enough data for the UI to show:

```text
Channel
Captured minimum
Current live value
Candidate trim
Captured maximum
Function
Validation issue
```

While in Review, endpoint min/max must no longer expand.

The live current value should still update so the user can center controls visually.

### Instruction semantics

For normal centered pilot axes:

```text
Roll
Pitch
Yaw
```

instruct the user to center the sticks and transmitter trims.

For conventional throttle, instruct the user according to ArduPilot's expected calibration procedure and supported vehicle semantics.

Do not hard-code a one-size-fits-all assumption for every possible reversible-throttle vehicle if the current application supports those vehicle types.

If vehicle type/capability information is not sufficient for a fully specialized instruction, make the common Copter/Plane behavior explicit while keeping the service API extensible.

---

## Fresh trim sampling

Immediately before writing:

- vehicle must still be connected/online;
- vehicle must still be disarmed;
- RC data must be fresh;
- capture must still correspond to the active vehicle;
- sample the current live channel values as trim candidates.

Do not reuse a stale `capture.Current` value that happened to be recorded during endpoint movement.

Validate each candidate trim against the captured range.

For centered channels, warn/fail if a trim is implausibly close to an endpoint.

Avoid inventing arbitrary thresholds if ArduPilot already supplies appropriate constraints/parameter metadata. Where a heuristic is necessary, make it explicit, conservative and tested.

---

## Throttle trim semantics

The supplied implementation currently leaves throttle `RCx_TRIM` untouched while writing trim for other channels.

Re-evaluate this behavior against:

- current ArduPilot documentation;
- the vehicle types supported by NextGen;
- historical Mission Planner behavior.

Do not retain or remove the special case merely because it exists.

The final implementation must have a documented policy and tests.

At minimum:

- conventional throttle must not accidentally get a trim equal to the last high-throttle endpoint;
- the chosen behavior must be visible in review/readback;
- reversible/centered throttle must not be damaged by a conventional-throttle assumption.

If this requires a small typed policy such as:

```text
Centered
LowTrim
PreserveExisting
```

derive it from vehicle/function semantics rather than UI guesses.

---

## Readback / atomicity behavior

Retain the current important safety behavior:

- operation gate;
- cancellation policy;
- disarmed requirement;
- parameter write validation;
- readback confirmation;
- disconnect handling;
- clear error state.

When possible, build the intended parameter write set before sending the first `PARAM_SET`.

This lets validation fail before making a partial change.

If a mid-write failure still occurs, preserve diagnostics showing which channels/parameters were confirmed and which were not.

---

## Channel-map correctness issue

Inspect current pilot-function resolution.

The current implementation builds a dictionary keyed by channel and may concatenate function names when more than one `RCMAP_*` function points to the same channel.

If duplicate-map validation later groups that dictionary by its already-unique key, it cannot discover the original duplicate assignment.

Fix duplicate pilot-function detection at the source data level.

Examples to detect:

```text
RCMAP_ROLL = 1
RCMAP_PITCH = 1
```

or any other duplicate assignment among:

```text
RCMAP_ROLL
RCMAP_PITCH
RCMAP_THROTTLE
RCMAP_YAW
```

Expose a clear static issue to the UI.

Do not automatically rewrite the user's mapping in this task.

---

## Snapshot/model improvements

Extend the radio calibration/channel model only as needed to make the state explicit.

Prefer immutable records/value objects for captured/calibration values.

Potential concepts:

```text
CapturedMinimum
CapturedMaximum
CandidateTrim
StoredMinimum
StoredMaximum
StoredTrim
CalibrationIssue
```

Avoid overloading one `Current` property with several meanings.

The UI should not need to reconstruct domain state by parsing strings such as `CaptureSummary`.

Keep human-readable summary/instruction text if useful, but expose structured data for the page.

---

## Tests

Add/adjust tests covering at least:

1. `StartAsync` enters `Capturing`.
2. moving a channel updates captured min/max.
3. ending endpoint capture enters `Review`.
4. entering Review writes **no parameters**.
5. endpoint min/max stop changing once Review begins.
6. live current values can continue updating in Review.
7. write cannot begin with stale RC data.
8. write cannot begin when vehicle is disconnected.
9. write cannot begin when vehicle becomes armed.
10. candidate trim is sampled from the fresh Review-state input, not the final endpoint value.
11. candidate trim outside captured min/max is rejected.
12. centered pilot-axis trim validation is sensible.
13. chosen throttle-trim policy is explicitly tested.
14. MIN/MAX/TRIM write/readback succeeds.
15. failed readback reports a useful failure.
16. cancellation behaves correctly before destructive/write stage.
17. disconnect during calibration transitions appropriately.
18. duplicate RCMAP assignments are detected.
19. normal AETR mapping does not produce a duplicate warning.

Use deterministic test telemetry rather than sleeps.

---

## Documentation

Add/update the relevant setup documentation with the user-visible sequence:

```text
1. Turn transmitter on.
2. Start calibration.
3. Move every required stick/control through its full travel.
4. Finish endpoint capture.
5. Return centered controls to center and set conventional throttle as instructed.
6. Review captured values.
7. Write and verify calibration.
```

Document the exact throttle policy chosen by the implementation.

---

## Acceptance criteria

Complete when:

- `RadioCalibrationState.Review` is used;
- endpoint capture and trim capture are separate stages;
- no parameters are written simply because endpoint capture ended;
- final trim comes from a fresh explicit Review-stage sample;
- MIN/MAX/TRIM policy is documented and tested;
- stale/armed/disconnected states cannot write;
- duplicate pilot-channel mapping is correctly detected;
- operation gating and readback verification remain intact;
- all radio setup/calibration tests pass.
