# MissionPlanner Next Gen — Receiver Bind and Follow-up Tasks

Execute in order.

## Tasks

1. `TASK_01_Add_Receiver_Bind_Command.md`
2. `TASK_02_Receiver_Bind_Diagnostics_And_UI.md`
3. `TASK_03_Guard_Embedded_Bootloader_Update.md`
4. `TASK_04_Fix_Raw_MAVLink_Capture_Integrity.md`

The first two tasks add FC-initiated CRSF/ExpressLRS receiver binding through ArduPilot's existing MAVLink support.

The third addresses the unsafe/ambiguous `Update Embedded Bootloader` action when running firmware identity and selected target disagree.

The fourth addresses a separate issue observed in exported Live Telemetry diagnostics: several raw MAVLink entries appear to retain only 3–4 payload bytes for messages whose actual payloads are much larger.
