# TASK 06 — Compass Regression Tests and Documentation

## Goal

Make the new Compass UX safe enough to become the template for later Setup pages.

## Unit tests

Cover configuration service:

- parameter -> semantic model mapping;
- semantic model -> parameter change set;
- missing parameters;
- metadata defaults;
- dependent changes;
- write verification;
- reboot aggregation.

## ViewModel tests

Cover:

- initial load;
- disconnected state;
- enabled/disabled Compass;
- pending changes;
- discard;
- Apply;
- default reset;
- calibration command availability;
- unsupported controls;
- validation banner;
- reconnect handling.

## Integration tests

Using simulator/fake parameter transport:

1. load `COMPASS_ENABLE=0`, `EK3_SRC1_YAW=0`;
2. enable Compass;
3. set orientation;
4. Apply;
5. verify expected parameter writes;
6. reread;
7. UI becomes clean.

Also test disabling a Compass currently used for yaw and verify dependent yaw-source change is surfaced.

## Regression protection

Ensure:

- calibration still works;
- Full Parameters page still works;
- Compass page does not create a separate parameter cache;
- no ViewModel directly writes arbitrary parameter names;
- Browser/WASM build remains valid;
- Desktop build remains valid.

## Documentation

Create/update a design document:

```text
docs/SetupUxPattern.md
```

Document:

- friendly setting vs raw parameter;
- feature configuration service;
- pending-change workflow;
- defaults;
- reboot handling;
- validation;
- Advanced section;
- unsupported capability handling.

Add Compass as the first concrete example.

## Acceptance criteria

- Comprehensive automated coverage exists.
- Setup UX architecture is documented for future migration.
- Compass becomes the reference implementation.
