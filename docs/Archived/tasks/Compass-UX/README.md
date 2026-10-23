# MissionPlanner Next Gen — Compass UX Transition Codex Tasks

These tasks establish a new Setup UX pattern using `CompassSetupView` / `CompassSetupViewModel` as the first implementation.

## Objective

Move Setup pages away from parameter-name-driven configuration and toward a subsystem-oriented configurator UX:

```text
Status
Configuration
Actions
Advanced parameters
```

The Full Parameters List remains available as the expert escape hatch, but users should not need to leave a Setup page for normal subsystem configuration.

## Design principles

1. Friendly concepts first; parameter names remain visible but secondary.
2. ViewModels do not directly encode arbitrary parameter names.
3. Feature-oriented configuration services translate UX concepts into ArduPilot parameters.
4. Current state, pending changes, defaults, validation, and reboot requirements are explicit.
5. No silent parameter overwrites.
6. Unsupported or unknown capability must be represented as such.
7. The Compass implementation should become a reusable template for later Setup pages, especially Arming.
8. Preserve existing calibration functionality and current application architecture where possible.

## Task order

1. `TASK_01_Define_Setup_UX_Architecture_And_Compass_Model.md`
2. `TASK_02_Implement_Compass_Configuration_Service.md`
3. `TASK_03_Rebuild_CompassSetupView_UX.md`
4. `TASK_04_Add_Pending_Changes_Defaults_Apply_And_Reboot.md`
5. `TASK_05_Add_Compass_Validation_Diagnostics_And_Dependency_Explanations.md`
6. `TASK_06_Compass_Regression_Tests_And_Documentation.md`
7. `TASK_07_Extract_Reusable_Setup_Page_Pattern.md`

Execute one task at a time.
