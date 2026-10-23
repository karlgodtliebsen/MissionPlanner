# MissionPlanner Next Gen — Live Telemetry Inspector

Execute tasks in order.

## Architectural decisions
- Add a dedicated `IVehicleTelemetryEventHub`.
- Reuse the existing EventHub implementation, but register a **separate DI instance** from the general application EventHub.
- Keep publishers unaware of the Inspector UI.
- Use current `VehicleState` plus normalized diagnostic events.
- Maintain a bounded per-vehicle diagnostic journal.
- `Freeze` freezes presentation only; ingestion/logging continues.
- Implement one reusable Inspector View/ViewModel, hosted in an Ursa/Avalonia Drawer and optionally a detached desktop Window.
- Browser/WASM must support Drawer mode without requiring a native Window.
- Never hard-code SysID 1.
- Do not implement live telemetry by rereading `.tlog`.
- High-rate RC/output/raw MAVLink updates must be bounded/coalesced.

## Task order
1. TASK_01_Dedicated_Vehicle_Telemetry_EventHub.md
2. TASK_02_Diagnostic_State_And_Bounded_Journal.md
3. TASK_03_Arming_Readiness_Why_Not_Armed.md
4. TASK_04_Inspector_Drawer_And_Detached_Window.md
5. TASK_05_Connection_Power_RC_Panels.md
6. TASK_06_Motor_Output_Sensors_Raw_MAVLink.md
7. TASK_07_Freeze_Markers_Timeline_Context_Integration.md
