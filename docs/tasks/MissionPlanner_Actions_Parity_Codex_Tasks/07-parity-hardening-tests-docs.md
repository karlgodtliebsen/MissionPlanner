# Codex Task 07 — Actions Parity Hardening, Tests, and Documentation

## Goal

Finish the FlightData Actions parity work by auditing the combined result of Tasks 01–06, strengthening regression coverage, and documenting intentional parity decisions.

This task is primarily validation/hardening. Do not add unrelated features.

## 1. Create a parity document

Create:

`docs/flightdata-actions-parity.md`

Document every legacy FlightData Actions capability that was evaluated and classify it as one of:

- **Keep / Already present**
- **Added to NextGen Actions**
- **Moved / Belongs elsewhere**
- **Intentionally not replicated**
- **Expert-only generic command**
- **Deferred because semantics/support remain unresolved**

At minimum include:

- Arm
- Disarm
- Set Flight Mode
- Auto shortcut
- Loiter/Hold
- RTL
- Land
- Takeoff
- Set Home Here
- Set Home Alt
- Set Current WP
- Restart Mission
- Resume Mission
- Abort Landing
- Change Speed
- Change Altitude
- Set Loiter Radius
- Set Mount / gimbal
- Joystick
- Raw Sensor View
- Clear Track
- Message
- legacy Do Action
- NextGen Expert MAV CMD
- Reboot Autopilot

For each item, briefly state the rationale and current NextGen location/status.

## 2. Preserve deliberate scope decisions

Do **not** re-add these legacy utilities to Actions as part of this task:

- Joystick
- Raw Sensor View
- Clear Track
- Message utility
- Mount/Gimbal controls

Instead, inspect whether each already has an appropriate NextGen home.

If a capability is absent altogether, record it as a separate deferred feature candidate in the parity document. Do not expand this task into implementing it.

Do not recreate the old generic **Do Action** menu. Keep **Expert MAV CMD** as the generic advanced path.

## 3. Cross-feature command-policy audit

Audit every Actions command after Tasks 01–06.

Verify each operator action has its own appropriate capability/policy result rather than borrowing another action's state.

At minimum verify independent gating for:

- Arm
- Disarm
- Set Mode
- Takeoff
- Land
- Hold
- RTL
- Set Home Here
- Set Home Alt
- Reboot
- Set Current WP
- Restart Mission
- Resume Mission
- Abort Landing
- Change Speed
- Change Altitude
- Set Loiter Radius

Where commands legitimately share a common policy primitive, document why while retaining semantically named action capabilities at the UI boundary.

## 4. Multi-vehicle isolation regression tests

Add explicit tests around two simultaneously available vehicle/session contexts.

Demonstrate that an action executed from vehicle A's Actions ViewModel/service context is never transmitted through vehicle B's transport/session.

Cover at least:

- one existing core command such as Set Mode or RTL;
- one new mission intervention command;
- one new in-flight adjustment command.

No production code or tests may rely on COM11/COM12; transports should be mocked/faked according to existing test conventions.

## 5. Command-status regression tests

Verify the shared status panel correctly handles:

- pending command;
- accepted ACK;
- rejected ACK;
- timeout;
- cancellation;
- telemetry confirmation where implemented;
- accepted-but-not-telemetry-confirmed cases without falsely claiming confirmation;
- sequential commands clearing/replacing stale status correctly according to current intended UX.

## 6. Layout and binding regression audit

Review `ActionsTabView.xaml` and ViewModel bindings after all additions.

Specifically verify:

- Expert Command ID is bound only to `ExpertCommandId`;
- Takeoff altitude remains bound only to `TakeoffAltitudeMeters`;
- new numeric fields do not share accidental bindings;
- independent `Can...` properties are wired to their corresponding controls;
- no new raw command IDs/parameters appear outside Expert MAV CMD;
- core flight controls remain visually prominent;
- secondary Mission intervention and In-flight adjustment sections remain compact and responsive.

Add binding-focused tests where supported by the repository's UI testing conventions.

## 7. Manual acceptance matrix

Add a concise manual verification matrix to the parity document for:

### SITL

- connect/disconnect;
- Arm/Disarm;
- Set Mode;
- Takeoff/Land/Hold/RTL;
- Set Home Here vs Set Home Alt distinction;
- mission Set Current WP / Restart / Resume / Abort Landing where applicable;
- Change Speed / Change Altitude / Set Loiter Radius;
- Expert MAV CMD command ID entry;
- command status and ACK behavior.

### Real flight controllers

Describe a safe bench-verification procedure using separate vehicle connections, emphasizing:

- props removed / motors made safe when motor-capable hardware is connected;
- commands checked against the selected vehicle only;
- no production assumption about specific COM port numbers;
- destructive/in-flight-only commands tested in SITL unless there is a controlled reason to exercise them on hardware.

## 8. Build/test gate

Before completion:

1. Build the full solution using the repository's supported build commands.
2. Run all relevant test projects, not only newly added tests.
3. Resolve all failures caused by Tasks 01–06.
4. Do not introduce new warnings attributable to this work.

## Acceptance criteria

The task is complete only when:

- the parity document exists and accurately reflects implementation decisions;
- all Actions operator controls have semantically correct policy gating;
- multi-vehicle isolation is covered by automated regression tests;
- command-status success/failure behavior is covered;
- the known Expert Command ID binding regression is permanently tested or otherwise guarded;
- the solution builds successfully;
- relevant tests pass;
- no intentionally excluded legacy tool has been reintroduced merely for pixel/function-count parity.

The final Codex report must list:

- files changed;
- tests added/changed;
- build/test commands and results;
- any deferred behavior and why;
- any follow-up feature candidates identified outside Actions.
