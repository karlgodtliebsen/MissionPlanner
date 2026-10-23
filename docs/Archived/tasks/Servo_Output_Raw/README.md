# MissionPlanner Next Gen — Output Inspector SERVO_OUTPUT_RAW Fix

This package contains one focused Codex task:

`TASK_Fix_Output_Inspector_SERVO_OUTPUT_RAW.md`

The bug is confirmed by the latest diagnostic export:

- Raw MAVLink contains valid `SERVO_OUTPUT_RAW` frames.
- The Outputs panel still says `No SERVO_OUTPUT_RAW received`.
- Vehicle state fields for servo output remain null.

The task traces and fixes the propagation path from decoded MAVLink through VehicleState/diagnostic state into the Outputs panel.
