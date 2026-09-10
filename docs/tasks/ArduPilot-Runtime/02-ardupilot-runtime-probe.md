# Task 02 — Add Safe ArduPilot Runtime Detection

## Objective

Add a bounded, read-only runtime-detection path that can prove a selected serial device is running ArduPilot **without requiring a normal MissionPlanner vehicle connection first**. Preserve the existing Betaflight/MSP probe and make the probes cooperate safely.

A controller such as `ArduPilot (COM10)` that responds as ArduPilot over MAVLink must no longer fall through to `Runtime: Unknown`.

## Required investigation

Locate and reuse:

1. existing Betaflight/MSP probe/result types;
2. normal MissionPlanner MAVLink serial transport/session implementation;
3. vehicle registry/session manager;
4. existing heartbeat/autopilot-identification helpers;
5. serial endpoint normalization/comparison;
6. timeout/cancellation/disposal conventions.

Do not introduce a second MAVLink parser/transport stack.

## Required probe order

### Step 1 — Existing active vehicle session

If MissionPlanner already has a vehicle session for the same serial endpoint:

- do not reopen or steal the COM port;
- use existing authoritative session/autopilot information;
- if it identifies `MAV_AUTOPILOT_ARDUPILOTMEGA`, return verified ArduPilot;
- mark evidence as existing vehicle session or equivalent.

Do not classify generic MAVLink as ArduPilot without checking autopilot identity.

### Step 2 — Bounded MAVLink probe when port is free

- Open through existing transport abstractions where practical.
- Prefer passive heartbeat reception/read-only interrogation.
- Do not modify parameters or vehicle state.
- Identify ArduPilot only when valid MAVLink identity says `MAV_AUTOPILOT_ARDUPILOTMEGA`.
- Honor cancellation and finite timeout.
- Always release resources on success, timeout, cancellation or exception.

Windows friendly name `ArduPilot` may prioritize discovery but **must not be the verification mechanism**.

### Step 3 — Existing Betaflight/MSP probe

If ArduPilot was not proven, run the existing Betaflight/MSP probe **sequentially**. Preserve its current verification semantics such as `FC_VARIANT == BTFL` if still applicable. Never run MAVLink and MSP probes concurrently against the same COM port.

### Step 4 — Typed unknown/failure result

Distinguish useful outcomes such as:

- MAVLink timeout/no response;
- MAVLink detected but autopilot is not ArduPilot;
- not Betaflight/MSP;
- port busy/owned;
- cancelled;
- transport/protocol error.

Use existing result/error abstractions when available.

## Safety requirements

The probe must not reboot, enter DFU/bootloader, erase/write flash, write parameters, arm/disarm, claim exact board identity, or forcibly take ownership of an active COM port.

## Architecture

Keep low-level MAVLink/serial logic out of `InstallFirmwareViewModel`. Prefer an explicit probe/service abstraction and return the typed runtime-identification result introduced/refined by Task 01.

## UI restrictions

No XAML/view changes. Do not touch icon buttons, bindings, resources, styles or toolbar composition.

## Tests

At minimum cover:

1. existing same-endpoint ArduPilot session => verified ArduPilot, no serial reopen;
2. free endpoint + ArduPilot heartbeat => verified ArduPilot;
3. valid non-ArduPilot MAVLink heartbeat => not falsely ArduPilot;
4. MAVLink timeout + successful Betaflight MSP => Betaflight;
5. neither recognized => Unknown with meaningful typed outcome;
6. busy port => no destructive takeover;
7. cancellation => prompt exit and resource release;
8. MAVLink/MSP probes are never concurrent for one endpoint.

## Acceptance criteria

- [ ] ArduPilot is verifiable via MAVLink without manual vehicle connection.
- [ ] Existing sessions are reused rather than disrupted.
- [ ] Betaflight detection still works.
- [ ] Probes are sequential, bounded, cancellable and read-only.
- [ ] Resources are reliably disposed.
- [ ] Probe does not claim exact board identity.
- [ ] Tests cover success, fallback, busy, timeout and cancellation.
- [ ] No icon-button definitions/resources/styles/bindings were modified.
- [ ] No XAML/view changes were made.
