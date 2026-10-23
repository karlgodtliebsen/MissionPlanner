# Deferred Task — Installing/Updating Betaflight Firmware from MissionPlanner

## Status

**Do not implement in the initial task set.**

Tasks 01–08 solve the current need:

```text
identify Betaflight
-> enter STM32 ROM DFU
-> safely convert compatible FC to ArduPilot
```

## Why Betaflight installation is separate

A reliable Betaflight installer needs policy beyond raw DFU programming:

- authoritative firmware/artifact source;
- current Betaflight target/config system;
- Cloud Build/API behavior where applicable;
- target/manufacturer/board revision matching;
- firmware channels/versions;
- artifact integrity;
- flash layout/address;
- configuration backup/migration;
- custom defines/options;
- deprecated targets;
- recovery;
- post-flash MSP verification.

MissionPlanner should not accidentally become an incomplete Betaflight Configurator.

## Reusable work from Tasks 01–08

Future Betaflight flashing can reuse:

- MSP parser/client;
- Betaflight identity;
- board/MCU/UID data;
- MSP ROM-DFU reboot;
- serial-to-DFU correlation;
- existing MissionPlanner DFU backends;
- progress UI;
- post-flash MSP verification concepts.

## Proposed later task set

1. Betaflight artifact/catalog investigation.
2. Exact board-target resolver.
3. Integrity/flash-address validation.
4. configuration backup/export strategy.
5. installer orchestration using existing DFU stack.
6. post-flash `BTFL` verification.
7. UI/version/channel selection.
8. physical hardware/recovery matrix.

## Separate future OSD work

Betaflight OSD/MSP DisplayPort/DJI Air Unit support is also a separate feature.

The Pavo 20 is useful test hardware for both, but firmware identity/DFU and OSD configuration should remain separate subsystems.

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
