# TASK 07 — Extract Reusable Setup Page Pattern

## Goal

After Compass is complete and validated on real hardware, extract only the proven reusable pieces needed by future Setup pages.

Do not prematurely create a large UI framework.

## Reusable concepts

Identify common behavior from Compass:

```text
Status section
Configuration section
Actions section
Advanced section
Pending changes
Apply / Discard
Defaults
Reboot required
Validation summary
Unsupported capability
```

## Reusable ViewModel support

Extract a lightweight abstraction only where duplication is real.

Potential concepts:

```csharp
SetupSettingState<T>
PendingSettingChange
SetupValidationMessage
SetupApplyResult
RebootRequirement
```

Avoid a generic base ViewModel if composition is simpler.

## Reusable controls

Consider small controls/components for:

```text
Setting label + description
Secondary parameter name/value
Changed marker
Default/reset affordance
Validation banner
Pending-change footer
```

Use existing Avalonia/Uranium/NextGen styling.

## Do not over-generalize

Do not attempt to model all ArduPilot parameters generically.

The intended architecture remains:

```text
View
  -> ViewModel
    -> Feature Configuration Service
      -> Parameter subsystem
```

not:

```text
Generic Setup Framework
  -> arbitrary parameter reflection
```

## Arming follow-up preparation

Document how the same pattern should later map to an Arming page:

```text
Status
  Ready / Not Ready / Armed

Configuration
  ARMING_CHECK
  ARMING_RUDDER
  ARMING_NEED_LOC
  RCx_OPTION = Arm/Disarm

Actions
  Arm / Disarm

Advanced
  relevant ARMING_* and RC option parameters
```

Do not implement the full Arming page in this task unless explicitly requested.

## Acceptance criteria

- Reusable pieces are extracted from working Compass code.
- Compass remains simple and readable.
- `docs/SetupUxPattern.md` includes guidance for Arming and later Setup pages.
- No unnecessary generic parameter-form framework is introduced.
