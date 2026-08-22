# Codex Task 3 — Frame-aware Motor Test

## Goal

Move the Motor Test user experience into Optional Hardware and make it frame-aware.

When the connected vehicle is a Quad/X, the page must expose four individual motor-test buttons. Hexa must expose six, Octa eight, etc., according to the **actual active frame layout**, not a manually entered motor count.

The page must distinguish:

- motor output/number;
- ArduPilot motor test order;
- frame class/type.

Do not treat these as interchangeable.

---

## Existing NextGen code to reuse

```text
src/Core/MissionPlanner.Core/Setup/
    Abstractions/IActuatorTestService.cs
    ActuatorTestService.cs
    MotorTestRequest.cs
    MotorTestSnapshot.cs
    MotorTestState.cs

src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/Sections/
    EscMotorSetupView.xaml
    EscMotorSetupViewModel.cs

src/Tests/MissionPlanner.Core.Tests/ActuatorSetupTests.cs
```

Classic behavioral/layout reference:

```text
src-v.1.38/GCSViews/ConfigurationView/ConfigMotorTest.cs
src-v.1.38/APMotorLayout.json
```

The classic page reads `FRAME_CLASS/FRAME_TYPE` or `Q_FRAME_CLASS/Q_FRAME_TYPE` and maps test-order letters to actual motor numbers.

Do not blindly copy the old JSON as a permanent source of truth.

---

## 1. Split Motor Test from ESC Calibration

`EscMotorSetupViewModel` currently combines ESC calibration guidance and motor testing.

Refactor so:

- Mandatory Hardware keeps **ESC Calibration** guidance/workflow;
- Optional Hardware gets **Motor Test**;
- both reuse `IActuatorTestService` or appropriately separated services;
- there is no duplicate implementation of `MAV_CMD_DO_MOTOR_TEST`.

Do not leave two independently evolving motor-test UIs.

---

## 2. Introduce a motor-layout projection

Create a testable domain projection, e.g.:

```text
MotorLayout
MotorLayoutMotor
IMotorLayoutResolver
```

Useful fields:

```text
FirmwareFamily
FrameClass
FrameType
FrameDisplayName
MotorCount
Motor Number / Output Index
TestOrder
Position/angle if known
Rotation if known
IsSupportedForIndividualTest
```

Resolve from the live vehicle/parameters:

```text
FRAME_CLASS / FRAME_TYPE
Q_FRAME_CLASS / Q_FRAME_TYPE
```

and vehicle type only as a fallback.

Do not guess a default of eight motors when frame information is missing.

If the layout cannot be resolved safely, disable individual/sequence testing and explain why.

---

## 3. Frame coverage

At minimum handle the standard matrix frame classes that can be resolved reliably:

```text
Quad
Hexa
Octa
OctaQuad
Y6
Tri
DodecaHexa
Deca
```

Explicitly review:

```text
Heli
Dual heli
Heli quad
Single
Coax
TailSitter
Scripting/Dynamic scripting frames
6DOF scripting
```

Do not fabricate a motor button count for unsupported/non-matrix layouts.

For scripting/custom frame layouts, present an unavailable/advanced explanation until a trustworthy actuator map can be discovered.

---

## 4. Use current ArduPilot motor-order semantics

Motor-test order changes with frame layout/type.

The current ArduPilot AP_Motors implementation defines frame classes/types and per-motor test order.

Use the current upstream AP_Motors semantics as the reference when creating/validating the resolver.

Do not assume Quad/X motor numbers are simply clockwise 1,2,3,4.

If an embedded/generated motor-layout resource is introduced:

- document its upstream/version provenance;
- make regeneration repeatable;
- test representative layouts;
- do not silently let it become stale.

---

## 5. Individual motor buttons

Build the button collection from the resolved layout.

Example concept for Quad/X:

```text
Motor A — Motor 1
Motor B — Motor 4
Motor C — Motor 2
Motor D — Motor 3
```

The exact mapping must come from the actual frame layout/test order, not this example.

Each button should clearly show:

```text
Test A
Motor/output N
rotation/position if known
```

A simple frame diagram is desirable if layout coordinates are available, but it is secondary to correct test semantics.

---

## 6. Correct MAV_CMD_DO_MOTOR_TEST ordering

Review the current `ActuatorTestService` use of motor-test order.

The service currently distinguishes `Board` versus `Sequence` for some operations.

Make the API explicit enough that a single test can target either:

```text
physical motor/output number
test-order position
```

without ambiguous integer arguments.

Avoid APIs such as:

```csharp
TestMotorAsync(vehicleId, 3, ...)
```

when the meaning of `3` is unclear.

Use a typed target/order concept.

---

## 7. Safety

Retain and strengthen existing safety gates:

- active target still connected;
- vehicle disarmed;
- shared vehicle operation gate acquired;
- explicit propellers-removed confirmation before first test operation;
- bounded throttle;
- bounded duration;
- command acknowledgement required;
- emergency STOP available whenever a test is running;
- disconnect transitions to a clear safe state;
- auto-stop after bounded duration.

Do not allow an arbitrary long-running manual motor command.

The STOP command must remain immediately available and must not wait behind a normal command queue.

---

## 8. Test All / Sequence

Provide:

```text
Test selected motor
Test all in sequence
STOP
```

Optionally provide "Test all" only if its semantics are safe and clearly distinct from sequence.

Prefer ArduPilot's sequence/test-order support rather than sending many concurrent motor commands.

The sequence count must come from the resolved motor layout.

---

## 9. UI

Use the Optional Hardware tab layout, with:

```text
Frame: QUAD / X
4 test positions

Throttle: 10 %
Duration: 2 s

[Test A] Motor ...
[Test B] Motor ...
[Test C] Motor ...
[Test D] Motor ...

[Test all in sequence]   [STOP]
```

Show the active test visually.

Do not copy classic button geometry.

---

## Tests

Cover at least:

1. Quad/X resolves four test positions.
2. Hexa resolves six.
3. Octa resolves eight.
4. DodecaHexa resolves twelve.
5. frame change rebuilds motor buttons.
6. unresolved/scripting frame fails closed.
7. motor test order and motor number are distinct in the model.
8. individual test sends the intended order/target.
9. sequence uses the layout count.
10. armed vehicle rejects before send.
11. disconnect while running transitions safely.
12. timeout/rejected ACK is visible.
13. STOP remains available while running.
14. ESC Calibration page no longer owns a duplicate motor-test UI.

---

## Hardware acceptance

With the user's Quad/X FC:

1. connect vehicle;
2. verify four buttons;
3. confirm displayed frame `QUAD/X`;
4. remove all props;
5. test A/B/C/D one at a time;
6. verify each physical motor corresponds to the displayed test order;
7. run sequence;
8. verify STOP and auto-stop;
9. change frame parameters in SITL/test environment and verify button count/layout changes.

---

## Acceptance criteria

Complete when the Motor Test tab is truly frame-derived, safe, and uses one shared actuator-test implementation.
