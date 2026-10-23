# TASK 04 — Pending Changes, Defaults, Apply and Reboot UX

## Goal

Make Compass configuration changes deliberate, reviewable and reversible.

## Pending-change model

Do not immediately write each UI edit to the FC.

Maintain:

```text
Current value
Pending value
Default value
Dirty state
Requires reboot
```

## Page footer

When there are pending changes, show:

```text
3 unsaved changes

[ Discard ]                         [ Apply ]
```

No footer when clean.

## Per-setting changed state

For a changed setting, show a subtle marker:

```text
Enable compass
Off -> On                         Changed
```

or equivalent MissionPlanner styling.

## Default values

Where parameter metadata supplies a default:

```text
Default: <friendly value>
```

Provide:

```text
[ Reset to default ]
```

Reset should modify pending state only until Apply.

If default metadata is unavailable:

```text
Default: Unknown
```

Do not guess.

## Apply workflow

Before write:

1. validate full desired Compass configuration;
2. calculate parameter change set;
3. show dependency changes clearly;
4. write via `ICompassConfigurationService`;
5. reread actual parameter values;
6. update current state;
7. clear pending state only after confirmed success.

## Dependent changes example

If user disables Compass while yaw source currently requires it, the change review should show both changes:

```text
Disable Compass
  COMPASS_ENABLE 1 -> 0

Yaw source
  EK3_SRC1_YAW Compass -> None
```

Never hide the second change.

## Reboot required

Aggregate reboot requirements.

If any change requires reboot:

```text
Reboot required after applying these changes.
```

After successful Apply:

```text
Changes applied. Reboot required.

[ Reboot Vehicle ]
```

Use the existing safe reboot command infrastructure.

Do not automatically reboot.

## Disconnect during edit

If the vehicle disconnects while changes are pending:

- retain pending values in the ViewModel only while page/session remains valid;
- disable Apply;
- show disconnected warning;
- after reconnect reread current FC state and detect conflicts before applying.

Do not blindly apply stale pending values after reconnect.

## Tests

Cover:

- dirty/clean transitions;
- discard;
- reset to default;
- dependent change review;
- apply success;
- apply partial failure;
- reread mismatch;
- reboot required;
- disconnect/reconnect conflict.

## Acceptance criteria

- Compass edits are never silently written.
- Users can understand exactly what will change.
- Defaults and reboot requirements are explicit.
