# AI implementation guidance

## Firmware boundary

`MissionPlanner.Firmware` owns firmware metadata, package validation, device discovery orchestration, bootloader protocol, compatibility, recovery matching, diagnostics, and update workflows. It must remain independent of MAUI and other UI frameworks.

The MAUI project owns presentation, file pickers, dialogs, navigation/shutdown interaction, clipboard support, and platform UI. `MissionPlanner.Core` supplies adapters to the existing Mission Planner vehicle connection and acknowledged MAVLink command infrastructure. Do not move popup logic, global application state queries, or native view types into `MissionPlanner.Firmware`.

Never erase before protocol-confirmed identity and compatibility checks. Never report success without verification. Treat serial port names as transient, keep every wait bounded, preserve one-operation ownership, and do not reuse an old MAVLink parser/channel after a bootloader transition.
