# MissionPlanner feature implementation task sets

These task sets were prepared from `MissionPlanner-20260722-v2.zip`.

Run each family sequentially. Commit after every completed task so a failed later task can be reverted independently.

Recommended order across families:

1. Flight Data tasks 01–03, because they establish shared action/message/status infrastructure. Folder name is docs\tasks\flight-data
2. Setup tasks, especially identity, firmware, calibration, and hardware workflows. Folder name is docs\tasks\seetup
3. Config tasks. Folder name is docs\tasks\config
4. Simulation tasks. Folder name is docs\tasks\simulation.
5. Return to the remaining Flight Data peripheral/log tabs as their underlying services become available.

Inspect the current source before implementing each task and adapt names to existing types rather than duplicating them. `src-v.1.38` is reference-only.
