# Codex Task 1 — Optional Hardware Shell and Capability Catalog

## Goal

Replace the placeholder `OptionalHardwareView` with the real NextGen Optional Hardware workspace.

It must use the same left-side `ExtendedTabView` approach as Mandatory Hardware while supporting Optional Hardware's different availability rules.

The page must change cleanly when:

- no vehicle is connected;
- a vehicle connects;
- the active vehicle changes;
- firmware family/identity changes;
- the parameter set becomes available or changes;
- a capability/component appears or disappears.

Do not port the classic WinForms backstage control.

---

## Inspect first

Current placeholder:

```text
src/UI/MissionPlanner.App/Views/InitSetup/OptionalHardware/
    OptionalHardwareView.xaml
    OptionalHardwareView.xaml.cs
    OptionalHardwareViewModel.cs
```

Mandatory Hardware pattern:

```text
src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/MandatoryHardwareView.xaml
src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/MandatoryHardwareViewModel.cs
src/UI/MissionPlanner.App/Views/InitSetup/MandatoryHardware/Sections/TabsUtils/*
```

Shared tab infrastructure:

```text
src/UI/UraniumUI/UraniumUI.Material.Controls.Extensions/TabViews/
    ExtendedTabView.cs
    LifecycleTabView.cs
    TabViewLifecycleContent.cs
```

Existing optional-hardware domain:

```text
src/Core/MissionPlanner.Core/Setup/
    Abstractions/IOptionalHardwareCatalog.cs
    Abstractions/IOptionalHardwareModule.cs
    Abstractions/IOptionalHardwareService.cs
    OptionalHardwareCatalog.cs
    OptionalHardwareService.cs
    OptionalHardwareModuleView.cs
```

Classic menu behavior reference:

```text
src-v.1.38/GCSViews/InitialSetup.cs
```

Screenshots:

```text
references/classic-optional-connected.png
references/classic-optional-disconnected.png
```

---

## Required architecture

### 1. Create a stable tab catalog

Introduce a stable key/descriptor model, e.g.:

```text
OptionalHardwareTabKey
OptionalHardwareTabDescriptor
OptionalHardwareTabState
```

The catalog should define:

- stable key;
- title;
- short description;
- display order;
- availability rule/category;
- whether an active vehicle is required;
- whether full parameter availability is required;
- optional firmware-family requirement;
- optional parameter-presence requirement;
- optional component/capability requirement;
- optional offline/local-tool capability.

Do not bury all conditions in the ViewModel as a giant switch.

### 2. Preserve ExtendedTabView index alignment

`ExtendedTabView` currently assumes `HeaderItemsSource[index]` corresponds to `Tabs[index]`.

Do not dynamically remove arbitrary header items while leaving static `TabItem` content at different indices.

Use one of these safe designs:

**Preferred:** keep a fixed descriptor/header collection aligned with the static TabItems and let the header view collapse unavailable items while the underlying index remains stable.

If the current UraniumUI TabView cannot safely collapse an unavailable header, make the **smallest general improvement** to `ExtendedTabView` needed to support `IsHeaderVisible` / availability without breaking index alignment.

Whichever design is chosen, add tests for selection/index behavior.

### 3. Header model

Create an Optional Hardware header ViewModel with at least:

```text
Key
Title
Description
IsAvailable
IsVisible
CanOpen
StateDisplay
ReasonUnavailable
```

Use a new typed header view or generalize the existing slim header only if doing so remains clean.

Unavailable tabs must not become accidentally selectable.

If the currently selected tab becomes unavailable after disconnect/vehicle change, move selection to the first visible/available tab.

### 4. Connection behavior

Classic Mission Planner showed a smaller set when disconnected and many more items when connected.

NextGen should preserve the **intent**, but improve the rule:

- standalone tools may remain visible without a vehicle;
- vehicle-dependent tabs appear only when the required active vehicle/capability exists;
- do not show an item merely because classic MP showed it offline if the feature cannot actually operate offline.

Initial standalone candidates:

```text
SiK Radio
Bluetooth Setup
Joystick device setup
DroneCAN direct-adapter mode
RTK source setup
```

Other tabs should generally require an active vehicle and the relevant parameters/capabilities.

### 5. Vehicle heading/status

Use a compact header similar to Mandatory Hardware:

```text
Optional Hardware
<vehicle heading or No vehicle connected>
<availability summary>
```

Do not show setup-completion semantics such as "Record reviewed"; Optional Hardware is not a mandatory workflow checklist.

Useful summary examples:

```text
12 optional hardware tools available for ArduCopter.
5 standalone tools available; connect a vehicle to show vehicle-specific hardware.
```

### 6. Lifecycle

Every real tab content must implement/use the existing lifecycle-aware TabView model.

Requirements:

- create/activate expensive subscriptions only when selected;
- cancel operations when deactivated;
- release direct serial/CAN/joystick resources on deactivation;
- do not keep all specialty utilities active just because the page exists.

### 7. Parameter-change refresh

Availability that depends on parameter presence must refresh after parameter updates without reacting to every incoming parameter individually.

Use a small debounce/coalescing mechanism comparable to the existing Mandatory Hardware parameter refresh.

Do not refresh full module state on every single `PARAM_VALUE`.

### 8. Keep old generic OptionalHardwareSetup infrastructure

Do not delete the existing `IOptionalHardwareModule` / `OptionalHardwareService` framework.

It is useful for parameter-backed module discovery and write/readback.

The new workspace should build richer dedicated views on top of it where appropriate rather than replacing the service with UI-specific code.

---

## Initial tab order

Use this as the starting order; adjust only with a documented reason:

```text
RTK / GPS Inject
SiK Radio
DroneCAN / UAVCAN
Joystick
Battery Monitors
CAN GPS Order
Compass / Motor Calibration
Rangefinder
Airspeed
Optical Flow
Onboard OSD
Camera / Gimbal
Motor Test
Bluetooth Setup
Parachute
ESP8266 Setup
CubeID Update
Antenna Tracker
FFT Setup
```

PX4Flow-specific focus/calibration functionality should live under Optical Flow rather than forcing a duplicate top-level tab unless implementation constraints prove otherwise.

---

## Tests

Add tests for at least:

1. catalog has unique stable keys;
2. fixed tab/header ordering is deterministic;
3. no-vehicle state shows only standalone-capable items;
4. connected ArduCopter with representative parameters shows vehicle-specific tabs;
5. parameter-dependent tab hides when its signature parameters are absent;
6. firmware-family-restricted tab does not appear for another family;
7. selected hidden tab falls back safely after disconnect;
8. active-vehicle identity change recomputes availability;
9. parameter-change bursts coalesce rather than rebuild on every change;
10. ExtendedTabView header visibility/index alignment remains correct.

---

## Acceptance criteria

Complete when:

- `OptionalHardwareView` is no longer a placeholder;
- the page uses the same ExtendedTabView/left-tab family as Mandatory Hardware;
- the connected/disconnected set changes safely;
- the tab/header index contract cannot drift;
- lifecycle boundaries are correct;
- the availability logic is testable outside XAML;
- existing optional-hardware services remain reusable;
- future tasks can add each tab without redesigning the shell.
