# Task 03 — Integrate Runtime Detection into InstallFirmware

## Objective

Integrate Tasks 01–02 into the existing `InstallFirmware` refresh/device-selection workflow. A serial controller that responds as ArduPilot over MAVLink must be represented as an ArduPilot application-runtime controller even without a normal MissionPlanner vehicle connection. Keep the UI substantially unchanged.

## Expected state for the motivating device

```text
Physical endpoint:     COM10
Runtime:               ArduPilot
Runtime evidence:      MAVLink probe
Runtime verification:  Verified
Operating mode:        Application
Exact board:           Not yet verified
```

Existing UI wording may follow repository conventions. Do not redesign the screen merely to reproduce the text above. The functional correction is that `Runtime: Unknown` must not remain after successful ArduPilot MAVLink identification.

## Required investigation

Locate where `InstallFirmware`:

- enumerates/selects serial devices;
- refreshes/probes the selected device;
- invokes current Betaflight detection;
- populates runtime/boot-environment state;
- derives workflow/status information;
- derives firmware filtering/compatibility hints;
- manages cancellation when selection changes.

Identify existing bound properties before considering any UI edit.

## Required behavior

### Runtime

- successful ArduPilot MAVLink result => verified ArduPilot;
- successful Betaflight MSP result => verified Betaflight;
- neither => typed Unknown/unavailable;
- USB metadata remains a hint only and does not establish exact board.

### Operating mode

A controller responding as normal ArduPilot application firmware must be represented as `Application` or the closest existing semantic equivalent, not as a failed/unknown bootloader simply because no bootloader is active.

If the current UI field is called `Boot environment`, prefer adapting state/value behind it rather than redesigning the screen. A tiny text-only correction is permissible only if truly necessary and it does not touch protected buttons.

### Existing session

If already connected through the normal vehicle subsystem, use Task 02's session result without disrupting the port.

### Refresh/race handling

Use existing cancellation/versioning patterns so:

- switching devices cannot apply stale probe results;
- repeated refresh does not leak serial handles;
- navigation/lifetime cancellation works normally.

### Firmware filtering

Runtime may refine workflow/protocol decisions, but it must not:

- claim an exact board;
- reintroduce broad USB-based exact-board matching;
- mark an APJ compatible merely because runtime is ArduPilot.

## UI protection — hard constraint

Prefer **zero InstallFirmware XAML/view changes**.

Do not modify any icon button, icon resource, button style/template, command declaration/binding, toolbar order/layout, tooltip, visibility, size or spacing. Do not add new buttons.

Existing buttons may naturally become enabled/disabled because corrected ViewModel state reaches existing `CanExecute` logic. Implement this in model/ViewModel/service code, not button markup.

If correct runtime state cannot be surfaced without colliding with ongoing UI work, implement the state correctly and report the UI limitation for later work.

## Tests

At minimum cover:

1. verified ArduPilot result => runtime ArduPilot, mode Application, exact board unverified;
2. verified Betaflight => preserved behavior;
3. unknown result => Unknown plus diagnostic outcome where supported;
4. stale probe cannot overwrite a newer selected device;
5. active vehicle session causes no duplicate port takeover;
6. USB-only `ArduPilot` name does not set verified runtime or exact board;
7. ArduPilot runtime alone cannot make an unknown/mismatched APJ target compatible.

## Acceptance criteria

- [ ] InstallFirmware consumes the runtime-identification service.
- [ ] MAVLink-proven ArduPilot is no longer reported as runtime Unknown.
- [ ] Normal ArduPilot runtime is represented as application mode.
- [ ] Exact board remains unresolved until authoritative evidence exists.
- [ ] Betaflight remains functional.
- [ ] Existing vehicle sessions are not disrupted.
- [ ] Stale probe results are prevented.
- [ ] Runtime knowledge is not used as exact-board proof.
- [ ] Relevant tests pass.
- [ ] No icon-button definitions/resources/styles/bindings were modified.
- [ ] View markup remains unchanged unless an unavoidable text-only status correction is explicitly documented.
