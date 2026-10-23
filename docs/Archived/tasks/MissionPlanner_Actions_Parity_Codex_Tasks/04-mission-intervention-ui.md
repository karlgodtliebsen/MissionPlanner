# Codex Task 04 — Mission Intervention Actions UI

## Goal

Expose the mission-intervention backend capabilities from Task 03 in the FlightData Actions UI while preserving the clean NextGen design.

Required controls:

- Set Current Waypoint
- Restart Mission
- Resume Mission
- Abort Landing

## UI design

Add a compact section named **Mission intervention** (or an equally clear term consistent with existing UI language).

Prefer an expandable/collapsible section if that matches the current control library and existing application patterns. The common core controls at the top of Actions must remain visually dominant.

Do **not** recreate the legacy MissionPlanner matrix of equally weighted green buttons.

### Set Current Waypoint

Provide:

- a mission sequence/item numeric input or suitable selector;
- a **Set Current WP** action button.

Show enough context to avoid ambiguity between a UI row number and the actual MAVLink mission sequence number. Use the canonical mission sequence expected by the backend.

When current mission metadata is available, constrain/validate the input appropriately.

### Restart Mission

Provide a clearly named action. Since this can materially alter mission execution, use the application's established confirmation pattern if one exists for disruptive in-flight operations.

### Resume Mission

Provide a clearly named action without pretending it is equivalent to simply selecting AUTO mode.

### Abort Landing

Provide the action only when its independent policy/capability allows it. The label must make the consequence clear.

## ViewModel work

Extend `ActionsTabViewModel` using the existing command pattern.

Requirements:

- independent `Can...` state for each mission action;
- asynchronous relay commands;
- no raw MAVLink command construction in the ViewModel;
- selected vehicle/session targeting through the existing service architecture;
- command-status panel updated through the existing status/event mechanism;
- no duplicated ACK logic in the ViewModel if the command service already owns it.

Refresh availability when vehicle state, flight mode, connection state, mission state, or policy-relevant state changes.

## UX requirements

1. Controls disabled when not applicable rather than failing only after click.
2. Validation errors for waypoint input are explicit and local to the control where practical.
3. Pending execution prevents accidental repeated submission according to existing command serialization rules.
4. Existing core Actions layout remains usable at desktop and mobile/tablet widths.
5. Do not expose command IDs or protocol parameters.

## Acceptance tests

Automated ViewModel/UI tests must demonstrate at minimum:

1. Set Current WP invokes the typed backend method with the entered mission sequence.
2. An invalid waypoint value does not invoke the backend.
3. Restart invokes only the typed Restart Mission operation.
4. Resume invokes only the typed Resume Mission operation.
5. Abort Landing invokes only the typed Abort Landing operation.
6. Each action has independent policy/capability gating.
7. Disabled policy state is reflected in control/command CanExecute state.
8. Command exceptions/timeouts/cancellation surface through the existing status model without crashing the UI.
9. Existing Actions controls continue to function and retain their bindings.
10. Expert MAV CMD remains separate and is not used internally by these controls.

## Manual acceptance

Using ArduCopter SITL with a small uploaded mission, provide a verification sequence covering:

- start/enter a mission;
- change current mission item;
- restart;
- resume after an interruption in the state supported by the implementation;
- exercise Abort Landing only in an applicable simulated state.

Record expected telemetry/status transitions in the completion report.

## Build/test gate

Build the affected app/projects and run relevant UI/ViewModel tests plus the backend tests introduced in Task 03. Report exact commands and results.
