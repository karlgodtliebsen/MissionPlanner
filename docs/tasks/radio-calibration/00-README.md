# MissionPlanner NextGen — Radio Calibration Codex Task Set

Prepared for the current MissionPlanner NextGen radio-calibration implementation.

Run the tasks in this order:

1. `01-radio-channel-meter-control.md`
2. `02-radio-calibration-review-and-trim-workflow.md`
3. `03-radio-calibration-page-redesign.md`

The order matters:

- Task 1 creates the reusable visual channel-meter control.
- Task 2 corrects and strengthens the calibration state/workflow.
- Task 3 integrates both into the complete Radio Calibration page.

A screenshot of the current Radio Calibration page is included under:

```text
references/current-radio-calibration.png
```

## Important repository guidance

Before editing, inspect the repository guidance files that exist in the current branch, especially:

```text
docs/AGENTS.md
docs/AI.md
docs/CODEX.md
docs/DESIGN_CONCEPTS.md
docs/ARCHITECTURE_DECISION_RECORDS.md
```

Also inspect the current radio setup/calibration implementation and tests before making assumptions.

`src-v.1.38` is historical behavioral reference only. Do not edit it.

## Verification

Prefer normal repository build/test commands from `src/`.

At completion of each task report:

- files changed;
- behavior changed;
- state-machine/API changes;
- tests added/changed;
- build/test commands and results;
- any hardware-only verification still required.

## Design intent

The improved UI may take inspiration from modern flight-controller configurators such as Betaflight, but it must remain a distinct MissionPlanner design:

- no copied graphics/assets;
- no copied layout;
- no copied palette;
- use existing MissionPlanner/Ursa theme resources;
- prioritize ArduPilot semantics and MissionPlanner workflow over visual imitation.
