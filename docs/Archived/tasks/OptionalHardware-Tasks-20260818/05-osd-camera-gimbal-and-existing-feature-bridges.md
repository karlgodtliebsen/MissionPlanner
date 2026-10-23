# Codex Task 5 — Reuse Existing OSD and Camera/Gimbal Subsystems in Optional Hardware

## Goal

Complete these Optional Hardware entries without duplicating existing NextGen subsystems:

```text
Onboard OSD
Camera / Gimbal
```

The Optional Hardware page should act as the setup/configuration entry point, while existing Config/FlightData services remain the source of truth.

---

# Part A — Onboard OSD

## Existing NextGen implementation

Inspect:

```text
src/Core/MissionPlanner.Core/ConfigTuning/Osd/*
src/UI/AvaloniaUI/MissionPlanner.AvaloniaUI.App/Views/ConfigTuning/Tabs/
    OnboardOSDTabView.axaml
    OnboardOsdTabViewModel.cs
    OnboardOsdPreviewView.axaml
```

Do **not** create a second OSD configuration engine in Optional Hardware.

Choose one clean integration:

1. host the existing OSD view as lifecycle content when safe, or
2. make the Optional Hardware tab a concise capability/status card with an explicit action that navigates to the existing OSD configuration workspace.

Prefer navigation if hosting creates duplicate ViewModel ownership/lifecycle issues.

Availability must be based on actual OSD parameters/capability.

Do not implement the classic `ConfigHWOSD` stream-rate-only UI as the primary modern OSD setup if the newer Onboard OSD subsystem already supersedes it.

If stream-rate settings are still genuinely needed, expose them as an advanced subsection and explain why.

---

# Part B — Camera / Gimbal configuration

## Existing NextGen live-control implementation

Inspect:

```text
src/Core/MissionPlanner.Core/FlightData/Payload/
    ICameraProtocolService.cs
    IGimbalProtocolService.cs
    PayloadProtocolService.cs
    CameraCapabilities.cs
    GimbalCapabilities.cs

src/UI/AvaloniaUI/MissionPlanner.AvaloniaUI.App/Views/FlightData/Tabs/
    PayloadControlTabView.axaml
    PayloadControlTabViewModel.cs
```

These provide flight-time protocol control and must not be duplicated.

Optional Hardware must focus on **configuration**.

---

## Configuration model

Inspect classic:

```text
src-v.1.38/GCSViews/ConfigurationView/ConfigMount.cs
```

but map it to current ArduPilot parameter families rather than hard-coding only classic names.

Potential configuration concepts:

```text
mount/gimbal backend/type
servo outputs/functions
pitch/roll/yaw limits
stabilization flags
retract/neutral positions
RC input mapping
camera trigger type
camera servo on/off values
```

Build a metadata-backed projection that detects whichever parameter family the connected firmware exposes.

Do not assume old `MNT_*` names are universal on current firmware.

---

## Relationship between setup and live control

The Optional Hardware tab should clearly distinguish:

```text
Configuration
Live camera/gimbal control
```

Provide an action such as:

```text
Open Payload Control
```

that navigates to the existing Flight Data payload tab.

Do not embed live flight controls into the setup form unless there is a strong reason.

---

## Component discovery

If camera/gimbal components advertise capabilities separately from autopilot parameters:

- show detected components;
- show which component is configured/active;
- avoid assuming component ID 1/1;
- preserve active-vehicle/component identity in service calls.

---

## Tests

1. OSD tab reuses/navigates to existing OSD subsystem.
2. no second OSD service is registered.
3. OSD availability follows actual parameters.
4. Camera/Gimbal tab hides when neither parameters nor component capabilities exist.
5. configuration parameter families are discovered by presence/metadata.
6. writes are readback-confirmed.
7. live-control navigation targets existing Payload Control.
8. active component identity is retained.
9. disconnect clears stale component configuration.

---

## Acceptance criteria

Complete when Optional Hardware exposes OSD and Camera/Gimbal cleanly while keeping one authoritative implementation for each underlying subsystem.
