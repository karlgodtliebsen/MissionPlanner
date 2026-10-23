# MissionPlanner Next Gen — ArduPilot Bootloader Firmware Upgrade

This package contains one focused Codex task:

`TASK_ArduPilot_Bootloader_Normal_Firmware_Upgrade.md`

The task changes the normal ArduPilot upgrade path from manual STM32 DFU to automatic reboot/discovery/upload through the installed ArduPilot bootloader, while retaining DFU as a recovery mechanism.

The three available OmnibusF4 drones running the same older firmware are explicitly included as the physical acceptance-test matrix.
