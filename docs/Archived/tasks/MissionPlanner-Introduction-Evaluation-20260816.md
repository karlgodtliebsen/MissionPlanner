# MissionPlanner Introduction — Evaluation and Suggestions

Source reviewed: uploaded `MissionPlanner-202600816-v1` snapshot.

## Overall assessment

The Introduction subsystem is now a solid small help/onboarding feature rather than a hard-coded startup page.

The separation is good:

```text
Introduction/Content/Introduction.json
Introduction/Content/*.md
Introduction/Images/*
Views/Introduction/*
```

Good choices already present:

- JSON controls topic order, screenshots, captions, callouts and actions.
- Markdown keeps most prose out of XAML.
- The renderer is native Avalonia rather than a WebView.
- Screenshots are lazy-loaded per selected topic.
- Desktop gets a left topic navigator; compact layouts switch to a Picker.
- Topic actions allow the guide to become navigable rather than one long document.
- The distinction between application preferences, vehicle setup and vehicle configuration is emerging clearly.

I would keep this architecture.

---

# Concrete issues found

## 1. `setup.jpg` is referenced but does not exist

`Introduction.json` contains:

```text
Images/setup.jpg
```

but the supplied Images folder contains `setup-1.png` through `setup-6.png` and no `setup.jpg`.

The image loader intentionally fails quietly, so this is easy to miss.

Recommendation:

- remove the `setup.jpg` entry, or
- add the intended screenshot.

Also add content validation/logging for missing image assets so a bad path is reported during Introduction loading.

## 2. `configuration.md` contains an empty section

Current structure:

```text
## Typical configuration work

## Parameters
```

The first heading has no content.

This is currently the most obvious text defect.

Suggested content:

```markdown
## Typical configuration work

Configuration pages are used after the vehicle has a working firmware and basic hardware setup. Depending on the vehicle and installed capabilities, they can include:

- geofences and other navigation limits;
- basic and extended tuning;
- onboard OSD configuration;
- MAVFTP file access;
- the full ArduPilot parameter set;
- optional hardware or communication components such as CubeLAN.

Use the dedicated configuration page when one exists. Use the full parameter list when you need a parameter that is not exposed by a higher-level editor.
```

## 3. Setup and Configuration overlap too much

`setup.md` and `configuration.md` currently begin almost identically, and both discuss parameters.

The screenshots suggest a useful distinction:

**Setup**
- firmware installation/update;
- initial board commissioning;
- sensors/calibration;
- RC/control hardware;
- bootloader/DFU/recovery;
- mandatory hardware setup.

**Configuration**
- geofence;
- tuning;
- OSD;
- MAVFTP;
- complete parameter editor;
- optional subsystem configuration such as CubeLAN.

I would make that distinction explicit.

A useful one-line rule:

> **Setup makes the vehicle ready to operate; Configuration changes how an already working vehicle behaves.**

It is not perfect for every feature, but it gives a new user a useful mental model.

## 4. The Maps topic currently looks like Mission Planning

The Maps topic uses:

```text
flightplanner.jpg
flightplanner-editor.png
flightplanner-quick-editor.png
```

and Mission Planning uses the same three images again.

That makes the two topics visually redundant.

The Maps topic should instead teach the map control itself.

Suggested dedicated screenshots/callouts:

1. map source/layer selector;
2. zoom in/out;
3. center/follow vehicle;
4. north/orientation control;
5. vehicle marker;
6. Home marker;
7. GCS/current-device position;
8. route/mission overlay;
9. coordinates/scale where available;
10. context menu or map interaction entry point.

Then let Mission Planning keep the route/editor screenshots.

## 5. Geolocation needs screenshots

The Geolocation topic is the only major topic with no image.

Add at least:

- Windows 11 `Settings > Privacy & security > Location`;
- optionally a cropped `services.msc` screenshot showing **Geolocation Service** for troubleshooting.

Keep service manipulation under a clearly marked **Troubleshooting** heading rather than as the normal setup path.

## 6. Several screenshots are too wide to teach their control well

Examples:

```text
topbar.png     2772 x 77
statusbar.png  1493 x 92
```

At normal help-page width the actual controls become very small.

Prefer two levels of screenshot:

- one full application screenshot for orientation;
- tightly cropped screenshots for individual controls.

For the Top Bar, crop around:

```text
Connect | Preferences | More
```

For the Status Bar, crop around:

```text
unit selector | vehicle/telemetry status | GCS coordinates
```

## 7. The Introduction image payload is larger than it needs to be

The supplied screenshots total roughly 23 MB.

Most of that is concentrated in four PNG files:

```text
introduction-overview.png
flyout.png
connect.png
connected.png
```

These contain large map areas, which compress poorly as PNG.

Recommendations:

- crop aggressively when only a UI control is being explained;
- keep PNG for small text/control crops;
- consider JPEG for large map-heavy full-screen screenshots where a small amount of compression is acceptable;
- avoid decoding many 2800x1500 images simultaneously.

This matters for startup package size and runtime image memory.

## 8. Setup and Configuration topics are very long image stacks

Setup currently has nine image entries and Configuration has eight.

`IntroductionTopicView` places them all in a `BindableLayout` inside the topic ScrollView. All image views for the selected topic are therefore created together.

The existing 50 ms detach/rebind workaround in `IntroductionPage` is already evidence that large screenshot lifecycle/decoding deserves care.

I would consider a gallery/carousel for topics with many screenshots:

```text
[ Previous ]   Screenshot 3 of 9   [ Next ]
```

with caption and optional thumbnail strip.

That keeps the Introduction readable and limits simultaneous large image decoding.

## 9. `HasError` does not notify when `ErrorMessage` changes

Current ViewModel:

```csharp
[ObservableProperty]
public partial string? ErrorMessage { get; set; }

public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);
```

XAML binds:

```text
IsVisible="{Binding HasError}"
```

but there is no `OnErrorMessageChanged` notification for `HasError`.

If loading fails after the initial binding, the error message text can change while `HasError` remains visually stale.

Fix with the same observable dependency pattern being used elsewhere in the project.

## 10. Content validation should go beyond ID/title

`IntroductionContentLoader.Validate(...)` currently checks:

- at least one topic;
- duplicate topic ID;
- topic ID;
- title.

Add validation/warnings for:

- missing Markdown asset;
- missing image asset;
- duplicate order values if unintended;
- `Topic` actions whose target ID does not exist;
- unsupported/invalid action targets.

The missing `setup.jpg` path demonstrates why this is worthwhile.

Do not make one missing optional screenshot prevent the entire Introduction from loading; log it and render a visible placeholder/caption where appropriate.

## 11. Some screenshot captions are placeholders rather than help text

Examples:

```text
Vehicle setup view 1.
Vehicle setup view 2.
Configuration Views
Geo Fence
Basic Tuning.
```

Captions should tell the user what they are looking at or why they would open that view.

Example:

```text
Firmware catalogue: choose the vehicle type, release, manufacturer and exact board target before downloading.
```

is much more useful than:

```text
Vehicle setup view 2.
```

## 12. Keep product naming consistent

The current files use several forms:

```text
MissionPlanner
MissionPlanner NextGeneration
new MissionPlanner interface
```

Pick one visible product name and one short form.

For example:

```text
MissionPlanner NextGen
MissionPlanner
```

Then use them consistently in the Introduction title/subtitle and prose.

---

# Text review by topic

## Welcome

Strong overall.

The workflow is understandable and the final safety quote is appropriate.

Suggested small wording change:

Current:

> A new interface, not a new MAVLink

This is technically accurate but slightly implementation-oriented for a first-run introduction.

Alternative:

```text
## The interface changes; the vehicle protocol does not
```

Then keep the existing explanation.

The welcome screenshot is useful, but the numbered callout labels should correspond exactly to the arrows baked into the screenshot. For example, if callout 1 points specifically to the hamburger button, call it **Open Flyout** rather than **Flyout Menu**.

## Flyout

Good and concise.

Consider adding one sentence explaining the current visual state:

```text
The selected destination remains highlighted so you can see which work area owns the main content.
```

Only add this if the current Flyout actually does so consistently.

## Top Bar

Good distinction between connection and application preferences.

I would replace:

```text
The first release focuses on one active vehicle connection at a time.
```

with:

```text
MissionPlanner currently works with one active vehicle connection at a time.
```

That will age better as documentation.

## Connecting

Good.

Potentially add:

```text
A successful transport connection is not the same as a flight-ready vehicle. MissionPlanner may still report calibration, sensor, RC, GPS, power or other PreArm issues after connecting.
```

This connects nicely to the new readiness/status work.

## Status Bar

The explanation of display units versus MAVLink/vehicle data is excellent and worth keeping.

If the status bar also shows explicit connection/telemetry state, mention the exact states shown by the current UI rather than generic future examples.

## Maps

The prose is conceptually good.

I would change:

```text
Home is an ArduPilot/Mission concept
```

to:

```text
Home is the vehicle's ArduPilot home/navigation reference. It should not be assumed to be the same as the current GCS position.
```

Most importantly, use map-specific screenshots rather than mission-editor screenshots.

## Geolocation

The conceptual distinction between GCS location and vehicle GPS is very good.

Suggested structure:

```text
## What MissionPlanner uses GCS location for
## Windows location settings
## Troubleshooting: Geolocation Service
## Accuracy
```

The service steps should remain troubleshooting, not the main path.

## Flight Data

Good explanation that the page is a projection of live vehicle state and that different telemetry fields update at different rates.

The two tab screenshots work well here.

Potentially mention that displayed stale/unknown values should remain distinguishable from valid zero values, if that behavior is already implemented.

## Mission Planning

Good and appropriately cautious.

I would keep this section relatively short; a full mission tutorial belongs in separate help.

A useful addition could be:

```text
Mission items are ordered commands. Moving a marker on the map changes the corresponding mission item; changing the editor changes the same underlying mission.
```

This reinforces the domain model without using architecture jargon.

## Setup

Refocus it on commissioning and hardware.

Suggested replacement opening:

```markdown
**Vehicle Setup** contains the workflows used to make a flight controller and its attached hardware ready for operation.

Typical setup work includes firmware installation, initial vehicle/frame selection, sensor calibration, radio/control setup, bootloader recovery, and other hardware-dependent commissioning steps.

MissionPlanner Preferences are different: they change the Ground Control Station application, not the connected vehicle.
```

Move the generic parameter explanation to Configuration.

## Configuration

This needs the most text work because the `Typical configuration work` section is empty.

Use the proposed content earlier in this report and tie the captions to the actual screenshots:

```text
Geofence
Basic Tuning
Extended Tuning
Onboard OSD
MAVFTP
Full Parameters
CubeLAN
```

Also use **MAVFTP**, not `Mav FTP`, for consistency with the protocol name.

## MissionPlanner Preferences

Good distinction.

If display units are already selectable directly from the Status Bar, avoid implying the Preferences page is necessarily the only place units are configured. Phrase it as application-level defaults/options.

---

# Suggested topic order

The current order is already reasonable.

I would use:

```text
Welcome
Flyout Menu
Top Bar
Connecting a Vehicle
Status Bar and Units
Maps
GCS Location
Flight Data
Mission Planning
Vehicle Setup
Vehicle Configuration
MissionPlanner Preferences
```

This is essentially your current order; only shorten **Ground Control Station Geolocation** to **GCS Location** in the navigator if space is limited.

---

# Suggested additions later

These are useful, but I would not block the current Introduction on them:

## Vehicle Readiness / PreArm

A short section explaining that connecting successfully does not mean the vehicle is ready to arm, and that ArduPilot PreArm messages should be resolved rather than merely hidden.

## Parameters

If the parameter editor becomes a major MissionPlanner differentiator, it may eventually deserve its own short Introduction topic rather than being buried under Configuration.

## Firmware

Likewise, the firmware workflow is now substantial enough that a short dedicated Introduction topic could eventually explain:

```text
normal ArduPilot update
local/custom APJ/PX4
embedded bootloader update
STM32 DFU initial/recovery installation
```

without duplicating the full firmware help.

---

# Recommended priority

I would make these changes first:

1. fix/remove missing `Images/setup.jpg`;
2. fill `configuration.md`;
3. separate Setup text from Configuration text;
4. replace Maps screenshots with map-specific screenshots;
5. add Geolocation screenshots;
6. rewrite generic screenshot captions;
7. fix `HasError` notification;
8. add Introduction asset/action validation;
9. crop/optimize the four very large screenshots;
10. consider a gallery for Setup/Configuration instead of vertically decoding every screenshot.

The Introduction is already useful. The next improvement is not more prose; it is tighter mapping between each short explanation and a screenshot that shows exactly the control or concept being discussed.
