# Codex Task 3 — Add Derived Motor ↔ Physical Servo Output Mapping

## Objective

Introduce a small domain/service abstraction capable of answering:

> Which physical FC output currently drives a particular logical motor?

This relationship must be derived from `SERVOx_FUNCTION`.

Do **not** add this responsibility to `MotorLayoutResolver`.

## Architectural separation

Keep these concepts separate:

```text
MotorLayoutResolver
    FRAME_CLASS / FRAME_TYPE
        ->
    logical MotorNumber
    TestOrder
    Rotation
    frame geometry
```

versus:

```text
Servo Output mapping
    SERVOx_FUNCTION
        ->
    physical FC output -> logical function
```

The new resolver joins them only when explicitly requested.

A suitable concept might be:

```csharp
IMotorOutputResolver
```

or another name fitting the existing project.

Do not create a new service merely for naming consistency if an appropriate existing service already owns this responsibility.

## Required resolution

Given:

```text
SERVO1_FUNCTION = Motor2
SERVO2_FUNCTION = Motor3
SERVO3_FUNCTION = Motor4
SERVO4_FUNCTION = Motor1
```

the resolver must produce:

```text
Motor1 -> Output 4
Motor2 -> Output 1
Motor3 -> Output 2
Motor4 -> Output 3
```

## Important rules

The physical output number comes from:

```text
SERVO{n}_FUNCTION
```

It does not come from:

- Motor Test `TestOrder`;
- frame geometry;
- position A/B/C/D;
- logical MotorNumber itself.

The implementation must support non-sequential output assignments.

## Duplicate and missing assignments

Do not silently choose arbitrary outputs.

If no physical output is configured for a requested motor, return an explicit unresolved result, `null`, or suitable result type according to existing project conventions.

If multiple outputs unexpectedly resolve to the same logical motor function, do not silently hide the ambiguity.

Prefer an explicit result capable of representing:

```text
Resolved
NotAssigned
Ambiguous
```

if this fits the existing architecture.

## Integration with Motor Test

Do not change the semantics of the Motor Test command.

Motor Test still uses:

```text
TestOrder
```

for `MAV_CMD_DO_MOTOR_TEST`.

The derived physical output is informational/domain data.

For the Quad X example with the above Servo mapping:

```text
Test A -> Motor1 -> physical Output 4
Test B -> Motor4 -> physical Output 3
Test C -> Motor2 -> physical Output 1
Test D -> Motor3 -> physical Output 2
```

This information may be exposed by the Motor Test ViewModel if useful, but no UI redesign is required by this task.

## Dynamic changes

If the user edits and successfully writes:

```text
SERVOx_FUNCTION
```

the derived mapping must reflect the new parameter state.

Do not cache stale mappings across parameter changes unless proper invalidation already exists.

## Acceptance tests

### Test 1 — Standard supplied mapping

Given:

```text
SERVO1_FUNCTION = Motor2
SERVO2_FUNCTION = Motor3
SERVO3_FUNCTION = Motor4
SERVO4_FUNCTION = Motor1
```

assert:

```text
Motor1 -> Output 4
Motor2 -> Output 1
Motor3 -> Output 2
Motor4 -> Output 3
```

### Test 2 — Join with Quad X TestOrder

Given the above Servo mapping and Quad X layout, assert:

```text
A -> Motor1 -> Output4
B -> Motor4 -> Output3
C -> Motor2 -> Output1
D -> Motor3 -> Output2
```

### Test 3 — Unassigned motor

Given no `SERVOx_FUNCTION` corresponding to Motor3, resolving Motor3 must return an explicit unresolved result.

It must not infer:

```text
Motor3 -> Output3
```

### Test 4 — Non-motor functions

Given:

```text
SERVO5_FUNCTION = Disabled
SERVO6_FUNCTION = RCIN1
```

these must not appear as motor assignments.

### Test 5 — Changed assignment

Initially:

```text
SERVO4_FUNCTION = Motor1
```

then change configuration so:

```text
SERVO4_FUNCTION = Disabled
SERVO6_FUNCTION = Motor1
```

assert subsequent resolution returns:

```text
Motor1 -> Output6
```

### Test 6 — Ambiguous assignment

If two Servo outputs both claim the same logical motor function, assert the resolver does not silently choose one.

## Definition of done

- Physical output ↔ logical motor mapping has one clear owner.
- `MotorLayoutResolver` remains concerned only with frame/motor layout.
- Non-sequential `SERVOx_FUNCTION` mappings work.
- Missing/ambiguous assignments are handled explicitly.
- Motor Test command behaviour remains based on `TestOrder`.
- Tests pass.
- Build affected projects successfully.
- Do not add unnecessary coupling between Servo Output and Motor Test UI classes.
