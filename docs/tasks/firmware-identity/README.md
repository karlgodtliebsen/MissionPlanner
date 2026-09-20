# MissionPlanner Next Gen — Firmware Identity and Recovery Workflow

Execute in order.

## Goal
Distinguish:
1. physical/device identity
2. bootloader identity
3. currently running firmware identity
4. selected firmware identity

Keep normal upgrades strict, but add an explicit safe recovery workflow for the case where the wrong ArduPilot target is already installed.

## Real scenario
```text
Running firmware:
  speedybeef4
  board id 134

Selected firmware:
  omnibusf4
  board id 1002

Current result:
  compatibility.board-id-mismatch
```

The mismatch is valid, but the value currently shown as `Detected board ID` may actually be the **running firmware board ID** from `AUTOPILOT_VERSION.board_version`.

## Rules
- Do not weaken normal compatibility checks.
- Do not add generic force-flash.
- Recovery must be explicit.
- UI must state the source of every identity value.
- Prefer bootloader/device identity over running firmware identity for recovery.
- Parse local APJ embedded metadata; never trust filename alone.
- BoardId alone is not complete artifact identity.

## Tasks
1. `TASK_01_Model_Identity_Sources.md`
2. `TASK_02_Refactor_Compatibility_Evaluation.md`
3. `TASK_03_Explicit_Recovery_Workflow.md`
4. `TASK_04_Install_Firmware_UI_And_Diagnostics.md`
5. `TASK_05_Regression_Tests_And_Documentation.md`
