# Codex Task 4 — Implement `MOT_SPIN_ARM` and `MOT_SPIN_MIN` Setup in Motor Test

## Objective

Complete the existing Motor Test controls:

```text
Set Motor Spin Arm
Set Motor Spin Min
```

using the vehicle parameters:

```text
MOT_SPIN_ARM
MOT_SPIN_MIN
```

The implementation must correctly distinguish UI percentages from ArduPilot's normalized parameter representation.

## Parameter representation

ArduPilot stores these as normalized values.

For example:

```text
10% -> 0.10
13% -> 0.13
```

Provide centralized conversions rather than scattering `/ 100` calculations through the ViewModel.

Conceptually:

```csharp
float PercentToNormalized(double percent);
double NormalizedToPercent(float value);
```

Use existing project utilities/value objects if they already provide this behaviour.

## `MOT_SPIN_ARM`

The purpose is the minimum output at which motors reliably spin while armed.

The workflow is:

1. Remove all propellers.
2. Use Motor Test.
3. Find the lowest throttle percentage at which all motors reliably start.
4. Set `MOT_SPIN_ARM` slightly above that threshold.

Follow the original MissionPlanner recommendation:

```text
selected/test throttle + 2 percentage points
```

where appropriate.

Example:

```text
Motor Test throttle = 8%
Recommended MOT_SPIN_ARM = 10%
Parameter value written = 0.10
```

Do not silently write the value solely because the button was pressed if the existing UI/dialog architecture supports presenting/confirming the calculated value.

## Safety validation

Do not allow clearly unsafe values.

At minimum:

```text
0% <= selected percentage < 20%
```

for this setup workflow, consistent with the safety behaviour of the existing MissionPlanner feature.

Do not blindly duplicate questionable integer conversions from legacy MissionPlanner source.

## `MOT_SPIN_MIN`

`MOT_SPIN_MIN` represents minimum motor output while flying and must be greater than `MOT_SPIN_ARM`.

Use the established recommendation:

```text
MOT_SPIN_MIN = MOT_SPIN_ARM + 3 percentage points
```

Example:

```text
MOT_SPIN_ARM = 0.10
Recommended MOT_SPIN_MIN = 13%
```

Do **not** implement legacy logic equivalent to:

```csharp
(int)normalizedValue + 3
```

because:

```text
(int)0.10 == 0
```

and that confuses normalized fractions with percentages.

Convert explicitly.

## Required invariants

Before writing:

```text
MOT_SPIN_ARM < MOT_SPIN_MIN
```

must hold when both parameters are available.

Do not allow a recommendation or write that makes them equal or reverses the relationship.

Respect parameter metadata limits if they are available.

## Missing parameters

If the connected firmware does not expose:

```text
MOT_SPIN_ARM
```

disable or hide the corresponding operation according to the existing application's UI convention.

Do the same independently for:

```text
MOT_SPIN_MIN
```

Do not throw because a firmware/vehicle type does not support these parameters.

## Write behaviour

Use the existing parameter writing abstraction.

The operation must:

1. await the parameter write;
2. update local parameter state after successful acknowledgement according to existing parameter-service behaviour;
3. report rejected/timed-out writes;
4. leave the previous state intact on failure.

Do not implement a separate MAVLink `PARAM_SET` pipeline specifically for this view.

## UI state

Where practical, expose the current values as percentages near the controls.

For example:

```text
MOT_SPIN_ARM: 10%
MOT_SPIN_MIN: 13%
```

The underlying parameter state remains normalized:

```text
0.10
0.13
```

A large UI redesign is outside the scope of this task.

## Acceptance tests

### Test 1 — Percentage conversion

Assert:

```text
5%  -> 0.05
10% -> 0.10
15% -> 0.15
```

and reverse conversion produces:

```text
0.05 -> 5%
0.10 -> 10%
0.15 -> 15%
```

Allow only appropriate floating-point tolerance.

### Test 2 — Spin Arm recommendation

Given Motor Test throttle:

```text
8%
```

assert recommended:

```text
MOT_SPIN_ARM = 10%
```

and parameter value:

```text
0.10
```

### Test 3 — Spin Min recommendation

Given:

```text
MOT_SPIN_ARM = 0.10
```

assert recommended:

```text
MOT_SPIN_MIN = 13%
```

and parameter value:

```text
0.13
```

### Test 4 — Ordering invariant

Given:

```text
MOT_SPIN_ARM = 0.12
```

attempting to write:

```text
MOT_SPIN_MIN = 0.10
```

must be rejected before sending the parameter write.

### Test 5 — Excessive test throttle

Given test throttle:

```text
20%
```

or greater, the `Set Motor Spin Arm` workflow must refuse to write the recommendation.

### Test 6 — Missing MOT_SPIN_ARM

When the parameter set does not contain:

```text
MOT_SPIN_ARM
```

assert the operation is unavailable and no write is attempted.

### Test 7 — Missing MOT_SPIN_MIN

Same independently for:

```text
MOT_SPIN_MIN
```

### Test 8 — Write failure

Simulate failed parameter acknowledgement and assert:

- local value is not falsely reported as successfully changed;
- the error is surfaced;
- the operation can be retried.

## Definition of done

- Both existing buttons perform real parameter operations.
- Percent/normalized conversion is explicit and tested.
- `MOT_SPIN_ARM < MOT_SPIN_MIN` is enforced.
- No legacy integer-cast bug is reproduced.
- Unsupported firmware is handled gracefully.
- Parameter writes use the existing parameter subsystem.
- Relevant automated tests pass.
- Build affected projects successfully.
- Do not perform unrelated refactoring.
