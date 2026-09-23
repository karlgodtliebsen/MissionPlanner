# MissionPlanner Next Gen — Telemetry/Arming Follow-up Codex Tasks

These tasks are based on the latest full telemetry recording analysis.

## Confirmed findings

The recording shows:

- the compass issue is fixed (`COMPASS_ENABLE=0`, `EK3_SRC1_YAW=0`);
- RC input is working continuously;
- the user moved RC channel 5 from approximately 999 to 2000 and back;
- `RC5_OPTION=0` while `FLTMODE_CH=5`, so RC5 is not configured as Arm/Disarm;
- `SERVO_OUTPUT_RAW` is arriving repeatedly but remains a `RawMavLinkMessage`;
- `AHRS2`, `VFR_HUD`, `GPS_RAW_INT`, and `POWER_STATUS` also fall back to `RawMavLinkMessage`;
- the full logger metadata says `Size=0`, `Vehicle=Unknown`, `Firmware=Unknown`, and reports a shorter duration than the indexed packet stream;
- no real arm command/ACK was observed in the recording, and there were no current `PreArm:` failures.

## Task order

1. `TASK_01_Arming_Switch_Diagnostics_And_Configuration.md`
2. `TASK_02_Add_SERVO_OUTPUT_RAW_Typed_Decoder_And_State_Propagation.md`
3. `TASK_03_Add_Missing_Typed_MAVLink_Decoders.md`
4. `TASK_04_Fix_Telemetry_Log_Metadata_Finalization.md`
5. `TASK_05_Improve_Arming_Diagnostics_When_No_Arm_Request_Reaches_ArduPilot.md`

Execute one task at a time.
