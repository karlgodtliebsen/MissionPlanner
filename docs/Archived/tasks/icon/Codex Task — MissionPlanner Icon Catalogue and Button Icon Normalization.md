# MissionPlanner Next Gen — Create MissionPlanner Icon Catalogue and Normalize Button Icons

## Objective

Create and maintain a **MissionPlanner Icon Catalogue** for the Avalonia UI and use it to standardize button iconography throughout the application.

The existing `AXAML_BUTTON_INVENTORY.md` is the starting point.

For every in-scope button, review:

- the action represented by the button;
- the current text or tooltip;
- the current icon, if any;
- whether the current icon correctly communicates the action;
- whether an outline-style Material Design Icon is available;
- whether the action is sufficiently MissionPlanner-specific that a future custom MissionPlanner icon would be preferable.

Then update the AXAML so ordinary action buttons use icons rather than visible text where appropriate.

The final markdown document must become the authoritative **MissionPlanner Icon Catalogue**.

---

# 1. Important Scope Exclusions

The existing inventory intentionally omitted some UI areas.

Continue to exclude them.

Do **not** scan, catalogue, or modify buttons belonging to:

1. all Dialog / Dialogue views;
2. `TopbarView` / the application's top-bar view;
3. `VirtualizedDataGrid` and its internal controls/templates.

Do not reintroduce these omitted areas merely because an automated scan finds them.

Also do not modify generated files, designer files, test snapshots, or unrelated UI controls.

---

# 2. Source Document

Use the existing:

```text
AXAML_BUTTON_INVENTORY.md
```

as the starting point.

Do not create a second competing inventory.

Update this document in place.

After all AXAML modifications are complete, rescan the source files and update the line numbers in the markdown document so the catalogue refers to the **final source**, not the original pre-edit line numbers.

---

# 3. Required Catalogue Format

For every in-scope button, the catalogue must contain:

```text
AXAML file
→ line
→ tooltip/action
→ current icon
→ recommended icon
→ source
→ comments
```

Use a Markdown table similar to:

```markdown
| AXAML File | Line | Tooltip / Action | Current Icon | Recommended Icon | Source | Comments |
|---|---:|---|---|---|---|---|
| ... | 42 | Refresh parameters | Refresh | Refresh | MDI | Existing icon retained |
| ... | 55 | Install firmware | Update | DownloadBoxOutline | MDI | Replaced misleading Update icon |
| ... | 24 | Arm vehicle | none | VehicleArm | MissionPlanner custom | Temporary MDI fallback used |
```

The `Source` column must contain only:

```text
MDI
MissionPlanner custom
```

The `Comments` column should explain decisions where useful, especially:

- existing icon retained;
- existing icon changed;
- text button converted;
- outline replacement chosen;
- no satisfactory MDI icon exists;
- MissionPlanner custom icon recommended;
- temporary MDI fallback being used;
- button intentionally retains text;
- dynamic/state-dependent icon requires special handling.

---

# 4. Icon Selection Strategy

Use this priority order:

1. **Material Design Icons — outline variant**
2. Other visually compatible MDI icon where no outline equivalent exists
3. **MissionPlanner custom icon** recommendation

Do not introduce Font Awesome, Lucide, Bootstrap, Fluent, etc. in this task.

The UI should look like one coherent icon family.

The project already uses Material Design Icons, so MDI remains the primary icon set.

---

# 5. Prefer Outline Icons

Where MDI provides both filled and outline versions, prefer the outline version.

For example:

```text
DeleteOutline
CheckCircleOutline
CloseCircleOutline
FileCheckOutline
ContentSaveOutline
FolderOpenOutline
FileImportOutline
FileExportOutline
CloudDownloadOutline
ShieldCheckOutline
```

Do not replace a perfectly understandable icon merely because the name does not contain `Outline` if no visually appropriate outline version exists.

Semantic clarity is more important than satisfying a naming convention.

---

# 6. Establish a Consistent MissionPlanner Icon Vocabulary

The same conceptual operation should use the same icon throughout the application unless there is a compelling contextual reason not to.

Use the following semantics as the starting vocabulary.

## Files

| Operation | Preferred concept |
|---|---|
| Load local file | `FolderOpenOutline` |
| Save local file | `ContentSaveOutline` |
| Import | `FileImportOutline` |
| Export | `FileExportOutline` |
| Copy | `ContentCopy` |
| Open external resource | `OpenInNew` / suitable outline equivalent |

## Vehicle communication

Keep direction consistent.

```text
Vehicle → MissionPlanner = Read / Download
MissionPlanner → Vehicle = Write / Upload
```

Use:

| Operation | Preferred concept |
|---|---|
| Read from vehicle | `DownloadOutline` or best MDI equivalent |
| Write to vehicle | `UploadOutline` or best MDI equivalent |
| Refresh/re-read | `Refresh` |
| Connect | suitable connection icon |
| Disconnect | suitable disconnect icon |

Do not mix the direction semantics between pages.

## Editing/configuration

| Operation | Preferred concept |
|---|---|
| Apply pending values | `CheckCircleOutline` |
| Apply and persist | `ContentSaveCheckOutline` |
| Validate | `FileCheckOutline` |
| Verify | `ShieldCheckOutline` |
| Revert pending changes | `FileUndoOutline` |
| Reset/default/live state | `Restore` |
| Retry failed operation | `ReloadAlert` or suitable equivalent |
| Cancel operation | `CloseCircleOutline` |
| Delete persistent object | `DeleteOutline` |
| Clear transient data | suitable clear/eraser icon |

Do not use the same visual icon for `Delete`, `Clear`, `Reset`, `Revert`, and `Cancel`.

These operations have different semantics.

---

# 7. Review Existing Icons — Do Not Merely Fill Missing Ones

The catalogue is not just a missing-icon exercise.

Review **all existing icon assignments**.

If an existing icon is misleading, replace it.

Known examples from the current inventory that deserve review include concepts such as:

```text
PlusBoxMultipleOutline
```

being used for Apply,

```text
MessageAlertOutline
```

being used for Retry,

```text
FaceManProfile
```

being used for Parameter Profiles,

```text
NotificationClearAll
```

being used for Reset,

```text
Copyright
```

being used for Copy URL,

and especially:

```text
Update
```

currently representing several completely different firmware actions.

Do not treat these examples as a hardcoded replacement list.

Review their actual context and choose the icon that best represents the operation.

---

# 8. Convert Static Text Action Buttons to Icon Buttons

A large portion of the inventory still contains buttons such as:

```xml
<Button Content="Refresh"
        ToolTip.Tip="Refresh"
        Command="{Binding RefreshCommand}" />
```

Convert ordinary **static action buttons** to the same icon-based style already used elsewhere in MissionPlanner.

For example:

```xml
<Button ToolTip.Tip="Refresh"
        Command="{Binding RefreshCommand}">
    <avalonia:MaterialIcon Kind="Refresh"
                           Width="30"
                           Height="30" />
</Button>
```

The exact icon size should follow the surrounding MissionPlanner conventions/styles. Do not unnecessarily duplicate hardcoded dimensions if a reusable style already controls them.

---

# 9. Move Text Meaning into ToolTip.Tip

When converting a static text action button:

### Before

```xml
<Button Content="Apply"
        Command="{Binding ApplyCommand}" />
```

### After

Conceptually:

```xml
<Button ToolTip.Tip="Apply"
        Command="{Binding ApplyCommand}">
    <avalonia:MaterialIcon Kind="CheckCircleOutline" />
</Button>
```

The former visible action text must become the tooltip if no better tooltip already exists.

If an existing tooltip is more descriptive than the button text, retain the descriptive tooltip.

Example:

```text
Button text:
Abort Landing

existing tooltip:
{Binding AbortLandingReason}
```

Do not replace useful contextual tooltip information with a shorter generic tooltip.

---

# 10. Do Not Blindly Remove Data-Bearing Text

Not every button `Content` value is merely an action label.

Do not automatically remove text when the text itself conveys essential dynamic information.

Examples include patterns such as:

```xml
Content="{Binding Label}"
Content="{Binding DisplayName}"
Content="{Binding AttributionText}"
```

or dynamic state labels such as:

```xml
Content="{Binding PauseButtonText}"
Content="{Binding PlayPauseText}"
```

Evaluate these individually.

## Rule

If the text tells the user **which item** the button represents rather than merely **what operation** it performs, visible text may need to remain.

Examples:

- Motor A / Motor 1 / Motor 2
- dynamically generated selection labels
- attribution/provider text
- dynamically varying operation state

Do not make several neighbouring buttons visually identical and impossible to distinguish.

Document these decisions in the catalogue.

Use comments such as:

```text
Retain text — content identifies dynamic item
```

or:

```text
Requires state-aware icon; not converted automatically
```

---

# 11. State-Dependent Buttons

Buttons whose operation changes state must not receive a misleading static icon.

Examples may include:

```text
Play / Pause
Start / Stop
Enable / Disable
Connect / Disconnect
```

Where practical, use an existing state-aware icon binding or introduce a simple ViewModel icon property.

Do not introduce complicated UI logic merely for the audit.

If implementing a state-aware icon would materially broaden the task, retain the current text temporarily and document:

```text
MissionPlanner custom/state-aware icon required
```

---

# 12. MissionPlanner-Specific Icons

Some actions do not have sufficiently descriptive generic application icons.

These should be identified as:

```text
Source = MissionPlanner custom
```

Likely candidates include, but are not limited to:

```text
Arm
Disarm
RTL / Return To Launch
Loiter / Hold
Takeoff
Land
Set Home
Restart Mission
Abort Landing
Set Loiter Radius

Flight-controller probe
Firmware flash
Bootloader update
Enter DFU
DFU target
Combined HEX

Motor Test
Motor Test Sequence
Servo Output
Six-position calibration
CompassMot

GeoFence inclusion polygon
GeoFence exclusion polygon
GeoFence inclusion circle
GeoFence exclusion circle
GeoFence return point

MAVLink
MAVFTP
Parameter Profile
Pre-arm Check
```

Do not automatically classify all of these as custom.

First check whether MDI has a clear icon.

The criterion is:

> Would a MissionPlanner user understand the operation from the icon and tooltip without having to memorize an arbitrary symbol?

If yes, use MDI.

If no, classify it as `MissionPlanner custom`.

---

# 13. Custom Icons Are Catalogue Recommendations in This Task

Do **not** create a large custom SVG library as part of this task.

The primary deliverable is the catalogue and standardized button usage.

For a button that ideally needs a future MissionPlanner custom icon:

1. record the proposed custom icon name in the catalogue;
2. set `Source` to `MissionPlanner custom`;
3. use the closest acceptable **temporary MDI outline fallback** in AXAML if one is sufficiently understandable;
4. document the fallback in `Comments`.

Example:

```text
Recommended Icon:
ReturnToLaunch

Source:
MissionPlanner custom

Comments:
Temporary MDI fallback: HomeImportOutline. Replace when MissionPlanner custom icon set is implemented.
```

This produces an icon-based UI now while giving a later custom-icon task an exact specification to work from.

Do not invent custom SVG artwork in this task.

---

# 14. Firmware Installation Icons Need Particular Attention

Review the Install Firmware views carefully.

The current inventory contains several unrelated operations represented by `Update`.

These include concepts such as:

```text
Clear Selected Firmware
Install Firmware
Update Embedded Bootloader
Validate Combined HEX
Review DFU Target
Cancel Operation
```

These operations must not share the same icon.

Choose separate semantics for:

```text
select
clear
download
install/flash
validate
bootloader
DFU
review/inspect
cancel
```

Mark genuinely flight-controller-specific operations as future custom icons where appropriate.

---

# 15. FlightData Actions Need Particular Attention

The FlightData Actions page contains some of the most important safety-critical buttons.

Review independently:

```text
Arm
Disarm
RTL
Loiter / Hold
Land
Set Mode
Takeoff
Set Home Here
Reboot Autopilot
Altitude Zero
Set Current WP
Restart Mission
Resume Mission
Abort Landing
Change Speed
Change Altitude
Set Loiter Radius
Validate, Confirm, and Send
```

Do not choose icons solely because their name loosely resembles the button text.

They must communicate the actual MAVLink/vehicle operation.

For safety-sensitive commands, avoid icons with ambiguous generic meanings.

If necessary, classify them as `MissionPlanner custom`.

---

# 16. GeoFence Icons

GeoFence editing actions should form a visually coherent family.

In particular:

```text
Inclusion Polygon
Exclusion Polygon
Inclusion Circle
Exclusion Circle
Set Return Point
Finish Polygon
Cancel Edit
Remove Selected
Remove Return
```

Where MDI cannot clearly distinguish inclusion from exclusion geometry, classify them as MissionPlanner custom icons rather than choosing unrelated generic symbols.

---

# 17. Calibration and Motor-Test Icons

Review the setup views as a family.

Actions such as:

```text
Start calibration
Confirm orientation
Accept results
Cancel calibration
Reset / retry
Test motor
Test sequence
STOP
CompassMot
Calibrate voltage
Calibrate current
```

should use consistent semantics across Mandatory Hardware and Optional Hardware.

The same operation should not receive different icons merely because it appears on another setup page.

---

# 18. Keep Commands and Behavior Unchanged

This task is primarily presentation/UX work.

Do not change:

- command implementations;
- ViewModel behavior;
- MAVLink operations;
- parameter semantics;
- firmware logic;
- navigation;
- validation rules;
- business/domain logic.

Preserve:

```text
Command
CommandParameter
IsEnabled
IsVisible
bindings
styles
classes
layout relationships
```

unless a minimal change is required for state-aware icon presentation.

---

# 19. Do Not Damage Layout

When converting text buttons to icons:

- preserve button alignment;
- preserve spacing;
- preserve toolbars;
- avoid making icon buttons unnecessarily wide;
- maintain consistent icon dimensions;
- maintain adequate hit targets;
- preserve keyboard navigation;
- preserve disabled states.

The goal is a cleaner icon-based toolbar UI, not a layout redesign.

---

# 20. Tooltip Requirement

Every icon-only actionable button must have a meaningful:

```xml
ToolTip.Tip
```

An icon-only button with no tooltip is not acceptable.

Tooltips should describe the action, not the appearance of the icon.

Good:

```text
Refresh parameters
Write parameters to vehicle
Validate selected firmware
Return vehicle to launch
```

Bad:

```text
Refresh icon
Arrow button
Check
```

---

# 21. Consistency Pass

After individual files are updated, perform a global consistency pass.

Search the catalogue for repeated actions such as:

```text
Refresh
Apply
Write
Read
Save
Load
Import
Export
Reset
Revert
Clear
Delete
Cancel
Retry
Validate
Verify
Open
Close
Connect
Disconnect
Start
Stop
```

Verify that the same semantic operation uses the same icon wherever appropriate.

Resolve accidental inconsistencies before finishing.

---

# 22. Rebuild the Catalogue from Final Source

Once the AXAML modifications are complete:

1. rescan all in-scope AXAML files;
2. regenerate final line numbers;
3. update the catalogue rows;
4. update `Current Icon` to describe what is now actually present;
5. retain `Recommended Icon` as the target design;
6. ensure the `Source` classification is correct;
7. update comments to describe what was changed or intentionally retained.

The markdown must describe the repository **after this task**, not before it.

---

# 23. Catalogue Header

Add a short introduction at the top of `AXAML_BUTTON_INVENTORY.md`.

It should explain:

- the document is the MissionPlanner Icon Catalogue;
- which AXAML areas are scanned;
- which areas are intentionally excluded;
- MDI is the standard icon family;
- outline variants are preferred;
- MissionPlanner custom icons are used only when generic MDI does not adequately express a GCS/vehicle operation;
- visible button text is generally moved to tooltips for ordinary toolbar/action buttons;
- dynamic/data-bearing text may intentionally remain visible.

Keep the document useful for future UI development.

---

# 24. Validation

Build the affected application after the changes.

At minimum run the appropriate build for the MissionPlanner Avalonia application and ensure all modified AXAML compiles.

All MDI `Kind` values must actually exist in the installed icon package.

Do not assume an icon exists because it appears on a website version newer than the package used by the project.

Compile-time validation is required.

---

# 25. Automated/Manual Audit Checks

Before completing the task verify:

## Icon-only buttons

Every newly icon-only button has a non-empty tooltip.

## Static text buttons

There should be no ordinary static text-only action buttons remaining in the in-scope AXAML unless the catalogue explicitly explains why the text remains.

## Dynamic text buttons

Dynamic/data-bearing buttons must still be understandable and distinguishable.

## Existing icons

Existing icons have been reviewed, not merely accepted.

## Excluded areas

There must be no modifications caused by this task to:

```text
Dialog / Dialogue views
TopbarView
VirtualizedDataGrid
```

## Commands

No commands or application behavior have changed.

---

# 26. Acceptance Criteria

The task is complete only when all of the following are true:

1. `AXAML_BUTTON_INVENTORY.md` has become the MissionPlanner Icon Catalogue.

2. Every in-scope button has a catalogue entry containing:

   ```text
   AXAML file
   line
   tooltip/action
   current icon
   recommended icon
   source
   comments
   ```

3. Existing icons have been semantically reviewed.

4. Misleading icons have been replaced with better choices.

5. MDI outline variants are preferred where appropriate.

6. Ordinary static text-only action buttons have been converted to icon buttons.

7. Their former action text is available through `ToolTip.Tip`.

8. Existing more-descriptive tooltips have been preserved.

9. Data-bearing/dynamic button text has not been blindly removed.

10. MissionPlanner-specific icon gaps are explicitly classified as:

    ```text
    MissionPlanner custom
    ```

11. Such rows include a proposed semantic custom icon name.

12. Where necessary, a temporary MDI fallback is documented.

13. Repeated actions use a consistent icon vocabulary throughout the UI.

14. Install Firmware actions no longer use one generic icon for unrelated operations.

15. FlightData safety-critical operations have been reviewed individually.

16. GeoFence editing actions have been reviewed as a coherent icon family.

17. Calibration and motor-test operations use consistent semantics.

18. Final catalogue line numbers match the modified AXAML.

19. All referenced MDI icon kinds compile against the package actually installed in MissionPlanner.

20. MissionPlanner Avalonia builds successfully.

21. No command/domain/firmware/MAVLink behavior has been changed.

22. No excluded Dialog/Dialogue, TopbarView, or VirtualizedDataGrid source was changed.

---

# 27. Deliverables

Produce:

1. Updated in-scope AXAML files.
2. Updated `AXAML_BUTTON_INVENTORY.md`.
3. A short completion summary containing:
   - total buttons reviewed;
   - buttons already using suitable icons;
   - existing icons replaced;
   - text-only buttons converted;
   - buttons intentionally retaining visible text;
   - number of `MissionPlanner custom` icon candidates;
   - list of proposed MissionPlanner custom icon names;
   - build result.

Do not create the final custom SVG icon set in this task.

The completed catalogue will be used as the specification for a subsequent **MissionPlanner Custom Icon Set** task.