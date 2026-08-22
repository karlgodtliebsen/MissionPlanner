# Codex Task 1 — Correct `MotorLayoutResolver` and Motor Test Logical/Physical Mapping

## Objective

Correct the Motor Test implementation so that it distinguishes:

- ArduPilot logical motor number: `Motor1`, `Motor2`, …
- Motor test order: `A`, `B`, `C`, …
- Physical motor position on the configured frame
- Motor rotation: `CW`, `CCW`, or unknown

The current implementation incorrectly assumes that Motor Test order corresponds directly to logical motor number.

For a standard Quad X:

```text
FRAME_CLASS = 1   // Quad
FRAME_TYPE  = 1   // X
```

the required mapping is:

| Test order | Test letter | Logical motor | Position | Rotation |
|---:|---|---:|---|---|
| 1 | A | Motor 1 | Front Right | CCW |
| 2 | B | Motor 4 | Rear Right | CW |
| 3 | C | Motor 2 | Rear Left | CCW |
| 4 | D | Motor 3 | Front Left | CW |

## Required investigation

Before modifying code:

1. Locate the existing `MotorLayoutResolver`.
2. Locate all models returned by it.
3. Locate all Motor Test ViewModels/services/views consuming it.
4. Locate the code issuing `MAV_CMD_DO_MOTOR_TEST`.
5. Locate existing motor-layout data or hard-coded layout definitions.
6. Compare the implementation with the original MissionPlanner `APMotorLayout.json` semantics:
   - `Number`
   - `TestOrder`
   - `Rotation`
   - `Roll`
   - `Pitch`

Do not create a parallel motor-layout implementation if an existing model/service can be corrected.

## Required domain model

The resolved layout must preserve at least:

```csharp
MotorNumber
TestOrder
Rotation
Roll
Pitch
```

Prefer a strongly typed model such as:

```csharp
public sealed record MotorLayoutEntry(
    int MotorNumber,
    int TestOrder,
    MotorRotation Rotation,
    double Roll,
    double Pitch);
```

Adapt naming to the existing project conventions.

A convenience property for the display letter is acceptable:

```text
TestOrder 1 -> A
TestOrder 2 -> B
...
```

but `TestOrder` itself must remain available as an integer.

## Motor Test behaviour

The Motor Test UI must display motors sorted by:

```csharp
TestOrder
```

not by:

```csharp
MotorNumber
```

For Quad X the UI therefore becomes:

```text
Test A — Motor 1 — CCW
Test B — Motor 4 — CW
Test C — Motor 2 — CCW
Test D — Motor 3 — CW
```

If physical-position information is already available or can reliably be derived from `Roll/Pitch`, preserve it in the model. Do not invent unreliable position names merely for presentation.

## MAVLink command requirement

This distinction is critical.

For:

```text
Test B — Motor 4
```

the test command must use:

```text
MAV_CMD_DO_MOTOR_TEST
param1 = 2
```

because `2` represents Motor Test position/order `B`.

It must **not** send:

```text
param1 = 4
```

simply because the logical motor shown to the user is Motor 4.

The command model/service should make this semantic distinction obvious. Avoid ambiguous parameter/property names such as simply `Motor`.

Prefer concepts such as:

```csharp
TestOrder
MotorNumber
```

where appropriate.

## General requirements

The resolver must continue supporting frame classes/types already supported by the application. Do not special-case Quad X in the ViewModel.

The mapping must be resolved centrally by `MotorLayoutResolver` or its existing equivalent.

Unknown/unsupported layouts must fail gracefully and must not silently substitute the sequence:

```text
Motor1, Motor2, Motor3, Motor4...
```

unless such a sequence is actually correct for that frame definition.

## Acceptance tests

### Test 1 — Quad X resolver

Given:

```text
FRAME_CLASS = 1
FRAME_TYPE  = 1
```

assert the resolved entries contain:

```text
Motor 1 -> TestOrder 1 -> CCW
Motor 2 -> TestOrder 3 -> CCW
Motor 3 -> TestOrder 4 -> CW
Motor 4 -> TestOrder 2 -> CW
```

### Test 2 — Quad X presentation order

When sorted for Motor Test presentation, assert:

```text
A -> Motor 1
B -> Motor 4
C -> Motor 2
D -> Motor 3
```

### Test 3 — Test B MAVLink command

Given the user activates:

```text
Test B — Motor 4
```

assert `MAV_CMD_DO_MOTOR_TEST` is issued with:

```text
param1 = 2
```

not `4`.

### Test 4 — Test D MAVLink command

Given:

```text
Test D — Motor 3
```

assert:

```text
param1 = 4
```

### Test 5 — Existing layout regression

Existing tests for non-Quad-X frame layouts must continue passing. Add at least one non-Quad-X resolver test if none exists.

## Definition of done

- Quad X displays A→1, B→4, C→2, D→3.
- Command execution uses `TestOrder`.
- Logical motor number remains available independently.
- No Quad-X-specific mapping exists in the View/ViewModel.
- Relevant unit tests pass.
- Build the affected solution/projects and resolve all compilation errors.
- Do not perform unrelated refactoring.
