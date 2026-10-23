# MissionPlanner Next Gen — Arming Page / Functionality Codex Tasks

Repository reviewed: `karlgodtliebsen/MissionPlanner`, branch `main`.

## Current state
- `ArmingPage.axaml` is still a direct LiveMarkdown placeholder.
- `ArmingPage.axaml.cs` still builds `# Hello, Markdown!`.
- `ArmingViewModel` is essentially empty.
- Core already has `VehicleArmingDiagnostic`, `VehicleLiveDiagnostics.GetArming()`,
  `VehicleCommandService.ArmAsync/DisarmAsync`, `VehicleCommandPolicy`,
  `RadioArmingConfiguration`, `SafetyAssessmentService`, parameter edit/readback,
  `InformationDocumentView`, `UserDocument`, and the Compass Setup UX conventions.
- Heartbeat is authoritative for actual armed state; ACK alone is not.
- Radio already knows `RCx_OPTION=153` and conflict rules.
- `docs/SetupUxPattern.md` already identifies Arming as the next consumer.

## Intended page

```text
Arming
DISARMED / READY · RC input live · Arm switch RC8

Information | Configuration
```

Information explains readiness, blockers, request/ACK history and current configuration.
Configuration edits pre-arm checks, arming methods and supported vehicle requirements,
plus guarded GCS Arm/Disarm actions.

Arming does **not** replace the broader Safety or Failsafe pages.

## Task order
1. `TASK_01_Arming_Semantic_Model_And_Configuration_Service.md`
2. `TASK_02_Arming_Pending_Changes_And_RC_Assignment.md`
3. `TASK_03_Arming_Information_Documents.md`
4. `TASK_04_Rebuild_Arming_Page_UX.md`
5. `TASK_05_Guarded_Arm_Disarm_Actions.md`
6. `TASK_06_Lifecycle_Integration_And_Cross_Page_Ownership.md`
7. `TASK_07_Tests_Hardware_Acceptance_And_Documentation.md`

Execute one task at a time.

## Safety
Do not add force-arm, magic bypass values, or pre-arm bypass controls to the normal page.
Normal arming continues through the typed vehicle-command service and ArduPilot checks.
