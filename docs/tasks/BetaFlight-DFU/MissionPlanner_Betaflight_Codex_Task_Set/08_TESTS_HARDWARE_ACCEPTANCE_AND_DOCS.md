# Task 08 — Automated Regression, Physical Hardware Acceptance, and Documentation

## Objective

Validate Tasks 01–07 together on real hardware, close remaining automated-test gaps, and document the architecture and recovery procedure.

Unit tests alone are not sufficient for this task.

## A. Automated coverage gate

Ensure coverage for all of the following.

### MSP
- exact v1 vectors;
- framing/checksum;
- fragmented and multiple frames;
- noise/resync;
- malformed input;
- MSP error;
- timeout/cancellation/disconnect;
- v2 where implemented.

### Betaflight identity
- exact `BTFL`;
- other MSP firmware variant;
- non-MSP serial;
- API/version;
- BOARD_INFO current/older/extended;
- UID;
- optional commands.

### Discovery
- enrichment;
- active-port avoidance;
- cache/revalidation;
- COM reuse;
- no ArduPilot/PX4/DFU regression.

### Bootloader entry
- exact ROM reboot mode;
- ACK;
- disconnect-after-write race;
- armed rejection;
- strategy ordering;
- disposal.

### DFU correlation
- matching endpoint;
- unrelated pre-existing DFU;
- multiple candidates;
- ambiguity;
- topology mismatch;
- delay;
- no DFU;
- cancellation.

### Conversion
- exact compatible mapping;
- unsupported board;
- wrong target;
- reboot failure;
- DFU ambiguity;
- download/program failure;
- post-flash timeout;
- post-flash non-ArduPilot;
- successful verification;
- conversion receipt.

### UI
- identity/actions;
- safety/target confirmation;
- progress;
- error;
- existing ArduPilot flow regression.

## B. Physical acceptance — first 5-inch FC

Use a physical 5-inch Betaflight FC already known through prior manual work to accept ArduPilot `omnibusf4`.

### Preparation

- remove propellers;
- back up Betaflight configuration through a trusted method;
- record connected USB port;
- ensure correct board is selected.

### Sequence

1. Boot normal Betaflight.
2. Select FC in MissionPlanner.
3. Capture:
   - `BTFL`;
   - Betaflight version;
   - MSP API;
   - manufacturer ID;
   - board identifier;
   - target name;
   - board name;
   - MCU;
   - UID;
   - build/revision.
4. Verify MissionPlanner shows Betaflight identity instead of only `STM Device`.
5. Create/verify any `omnibusf4` compatibility mapping only from this exact observed board identity.
6. Choose `Reboot to DFU`.
7. Verify COM disappears without pressing BOOT.
8. Verify STM32 ROM DFU enumerates (`0483:DF11` where platform exposes it).
9. Verify MissionPlanner correlates that DFU endpoint with the selected FC.
10. Confirm the correct ArduPilot target `omnibusf4`.
11. Flash through MissionPlanner's existing DFU installer.
12. Verify normal USB/serial returns.
13. Verify MissionPlanner identifies ArduPilot.
14. Connect as a vehicle.
15. Exercise:
    - Parameters read/write;
    - Radio;
    - Motors with props removed;
    - external buzzer;
    - external GPS.
16. Record board-specific observations.

## C. Second 5-inch FC

Repeat identity, reboot, correlation, and conversion on a second physical unit.

Purpose:

- catch hard-coded COM assumptions;
- catch hard-coded USB-path assumptions;
- confirm otherwise identical boards remain distinct physical devices;
- validate different MCU UID/device identities.

## D. BetaFPV Pavo 20 acceptance

Use the Pavo 20 with DJI Air Unit.

1. Start in Betaflight.
2. Capture full MSP identity.
3. Verify board/manufacturer/MCU parsing.
4. Software `Reboot to DFU`.
5. Verify STM32 ROM DFU enumeration.
6. Verify selected-device correlation.
7. **Do not flash ArduPilot unless exact compatibility is independently established.**
8. Return/recover the Pavo to its correct Betaflight firmware through a trusted recovery method if needed.
9. Confirm DJI Air Unit wiring is irrelevant to the identity/DFU logic.

This Pavo then becomes a useful later fixture for OSD work.

## E. Multi-device safety

Where practical:

```text
FC A -> COM11 -> selected
FC B -> COM12 -> untouched
optional DFU X -> pre-existing
```

Verify:

- FC A remains selected across reboot;
- FC B is untouched;
- pre-existing DFU is ignored;
- ambiguity stops the flash.

## F. Documentation

Update:

```text
docs/INSTALL_FIRMWARE_VIEWMODELS.md
```

Add a focused document, e.g.:

```text
docs/BETAFLIGHT_DFU_AND_ARDUPILOT_CONVERSION.md
```

Document:

1. MSP architecture;
2. commands used;
3. identity model;
4. serial probing policy;
5. ROM-DFU reboot;
6. serial-to-DFU correlation;
7. compatibility mapping policy;
8. conversion phases;
9. recovery via physical BOOT;
10. how to add a reviewed board mapping;
11. verified physical hardware identities;
12. limitations and deferred Betaflight flashing.

Do not document an unverified Pavo-to-ArduPilot mapping.

## G. Build/test report

Run current appropriate project/solution build and tests.

Completion report must list exact commands and results.

## Final acceptance criteria

Accepted when:

- real Betaflight identity is reliable;
- one known-compatible 5-inch FC completes Betaflight -> DFU -> ArduPilot;
- a second unit validates physical-device isolation;
- Pavo 20 validates Betaflight identity + software DFU without unsafe target assumptions;
- multiple-device ambiguity is handled safely;
- current ArduPilot firmware installation has no regression;
- architecture/recovery is documented.

---
## Codex execution rules

1. Work from the current `main` branch and locate every referenced symbol before editing. MissionPlanner is evolving quickly; do not rely on stale paths or duplicate an abstraction that already exists.
2. Keep this task cohesive and limited to its stated scope.
3. Prefer typed protocol/domain models over unstructured metadata.
4. Reuse current MissionPlanner firmware, DFU, device-matching, installation, progress, and recovery abstractions wherever they already solve the problem.
5. Add or update automated tests for every behavioral change.
6. Run the affected project tests plus the relevant `MissionPlanner.Firmware` tests. Run a broader build/test when practical.
7. Fail closed on uncertain identity, compatibility, or physical-device correlation.
8. In the completion report include:
   - files changed;
   - architectural decisions;
   - tests run and results;
   - hardware/manual validation still required;
   - deviations from this task and why.
