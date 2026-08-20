# Codex Tasks — Replace Light/Dark Theming with an N-Theme Architecture

## Objective

Replace MissionPlanner Next Gen's current MAUI/UraniumUI Light/Dark theming mechanism with a proper application-level theme system supporting an arbitrary number of themes.

The resulting View syntax should normally be:

```xml
BackgroundColor="{DynamicResource Surface}"
TextColor="{DynamicResource OnSurface}"
```

rather than:

```xml
BackgroundColor="{AppThemeBinding
    Light={StaticResource Surface},
    Dark={StaticResource SurfaceDark}}"
```

A View must describe the **semantic purpose** of a color and must not know which concrete theme is active.

The first three application themes must be:

- Mission Light
- Mission Dark
- Mission Blue

In addition, `System` must remain available as a **selection policy**, resolving automatically to Mission Light or Mission Dark according to the operating-system appearance.

The architecture must make adding future themes such as Night Vision, Outdoor, High Contrast, or ArduPilot Classic straightforward without changing application Views.

---

# Current Code Baseline

The implementation must start from the uploaded `MissionPlanner-202600820-v1` source.

Relevant current files include:

```text
src/UI/MissionPlanner.App/
    App.xaml
    App.xaml.cs
    AppShell.xaml

    AppViewModels/
        AppShellContentViewModel.cs

    Services/
        PlannerSettingsRuntime.cs

    Resources/Styles/
        Colors.xaml
        Styles.xaml
        Override.xaml

    Views/Preferences/
        PreferencesPage.xaml
        PreferencesViewModel.cs

src/Core/MissionPlanner.Core/ConfigTuning/Planner/
    PlannerTheme.cs
    PlannerAppearanceSettings.cs
    PlannerSettings.cs
    PlannerSettingsService.cs
    IPlannerSettingsService.cs

src/UI/UraniumUI/
    UraniumUI.Material.VirtualizedDataGrid/
        Resources/
            StyleResource.xaml
            StyleResource.xaml.cs
```

Important current-state observations:

- `PlannerTheme` currently contains `System`, `Light`, and `Dark`.
- `PlannerSettings.CurrentSchemaVersion` is currently `4`.
- `PlannerAppearanceSettings` currently contains both `Theme` and `PreferDarkTheme`.
- `PlannerSettingsRuntime` currently implements theme preview by assigning `Application.UserAppTheme`.
- `AppShellContentViewModel` currently exposes `AppTheme[] AppThemeList`.
- `PreferencesViewModel` currently exposes `Enum.GetValues<PlannerTheme>()`.
- `Styles.xaml` contains 72 `AppThemeBinding` occurrences.
- `Override.xaml` contains 9 `AppThemeBinding` occurrences.
- MissionPlanner.App currently contains approximately 198 `AppThemeBinding` occurrences in total.
- The application uses UraniumUI Material controls extensively, including SelectField, TabItem, CheckBox, TextField, PickerField, TabView and DataGrid controls.
- The local VirtualizedDataGrid `StyleResource` currently performs its own Light/Dark color mapping and copies application color overrides into a UraniumUI `ColorResource`.

Do not simply add a third branch to `AppThemeBinding`.

The objective is to remove the Light/Dark decision from application Views entirely.

---

# Task 1 — Introduce the Semantic Theme Contract

Create a single authoritative definition of the color/resource roles every MissionPlanner theme must provide.

Use a class such as:

```csharp
public static class ThemeResourceKeys
{
    public const string Primary = nameof(Primary);
    ...
}
```

or an equivalent strongly defined mechanism.

The minimum required semantic contract should contain:

```text
Primary
OnPrimary
PrimaryContainer
OnPrimaryContainer

Secondary
OnSecondary
SecondaryContainer
OnSecondaryContainer

Tertiary
OnTertiary
TertiaryContainer
OnTertiaryContainer

Surface
OnSurface
SurfaceVariant
OnSurfaceVariant

SurfaceContainerLow
SurfaceContainer
SurfaceContainerHigh

Background
OnBackground

Success
OnSuccess
SuccessContainer
OnSuccessContainer

Warning
OnWarning
WarningContainer
OnWarningContainer

Info
OnInfo
InfoContainer
OnInfoContainer

Error
OnError
ErrorContainer
OnErrorContainer

Outline
OutlineVariant

Shadow
Scrim

InverseSurface
InverseOnSurface
InversePrimary

DisabledText
DisabledBackground
```

Additional semantic roles may be introduced where current Views contain a genuine semantic concept that cannot reasonably be represented by these keys.

Do not introduce arbitrary resources such as:

```text
SomePageBlue
DarkGreen2
LightPanelGrey
ButtonColour3
```

A resource name must describe its UI role.

## Acceptance criteria

Every installed theme exposes the complete required semantic contract.

Theme switching must never produce a missing-resource exception.

All required color values are `Color` resources.

A test must verify that all palettes expose the same required keys.

---

# Task 2 — Create the Theme Model and Catalog

Introduce an application theme abstraction independent of MAUI's `AppTheme`.

Recommended concepts:

```csharp
public sealed record ThemeDescriptor(
    string Id,
    string DisplayName,
    ThemeBaseAppearance BaseAppearance,
    string ResourcePath);
```

and:

```csharp
public enum ThemeBaseAppearance
{
    Light,
    Dark
}
```

Create:

```text
IThemeCatalog
ThemeCatalog
ThemeDescriptor
ThemeBaseAppearance
ThemeIds
```

Recommended built-in IDs:

```text
system
mission-light
mission-dark
mission-blue
```

`system` is special and is not itself a concrete color palette.

It is a selection policy.

The concrete catalog initially contains:

```text
mission-light
mission-dark
mission-blue
```

The catalog should expose theme descriptors in an order appropriate for the UI.

Use stable string identifiers.

Do not use an enum for concrete theme IDs.

This allows future themes to be introduced without modifying a serialized enum.

## Acceptance criteria

Adding a fourth concrete theme requires:

1. adding its ResourceDictionary;
2. registering a descriptor.

Existing Views require no changes.

---

# Task 3 — Split Concrete Themes into Independent Resource Dictionaries

Create:

```text
Resources/Themes/
    MissionLight.xaml
    MissionDark.xaml
    MissionBlue.xaml
```

Each dictionary must expose exactly the same semantic resource contract.

Do not use keys such as:

```text
PrimaryDark
SurfaceDark
WarningDark
ErrorDark
```

inside the new theme dictionaries.

For example both `MissionLight.xaml` and `MissionDark.xaml` contain:

```xml
<Color x:Key="Surface">...</Color>
<Color x:Key="OnSurface">...</Color>
```

because only the active palette determines what `Surface` means.

## Mission Light

Use the improved cool-neutral green light palette currently present in the uploaded `Colors.xaml`.

Preserve its visual intent.

## Mission Dark

Translate the existing dark theme into the same unsuffixed semantic key set.

The visual appearance of the current Dark theme should remain essentially unchanged.

This is important because the existing Dark theme is already considered successful.

## Mission Blue

Add the supplied `MissionBlue.xaml`.

Register it as:

```text
Id: mission-blue
Display name: Mission Blue
Base appearance: Light
```

Mission Blue should be visually distinct from Mission Light and should therefore serve as the primary proof that the application supports more than two themes.

---

# Task 4 — Turn `Colors.xaml` into the Active Theme Resource Dictionary

The existing `Colors.xaml` is referenced as:

```xml
<ResourceDictionary
    x:Name="AppColors"
    Source="Resources/Styles/Colors.xaml" />
```

and is also passed to the VirtualizedDataGrid UraniumUI style resource.

Preserve this named resource-dictionary concept where practical.

Refactor `Colors.xaml` so that it represents the application's **currently active semantic palette**, rather than simultaneously containing both Light and Dark palettes.

It may contain a bootstrap/default palette so XAML always has valid resources before `IThemeManager` applies the persisted theme.

Dark is an acceptable bootstrap because MissionPlanner currently defaults strongly toward Dark mode.

Keep truly theme-independent primitives such as:

```text
Transparent
White
Black
```

if needed for compatibility.

Generic MAUI `Gray100`, `Gray200`, etc. may temporarily remain during migration, but normal application UI must ultimately use semantic resources instead.

## Important

The ThemeManager should mutate or refresh the active semantic dictionary rather than teaching individual Views about concrete palette files.

---

# Task 5 — Implement `IThemeManager`

Create:

```text
IThemeManager
ThemeManager
ThemeChangedEventArgs
```

The ThemeManager must own theme resolution and theme application.

Recommended API shape:

```csharp
public interface IThemeManager
{
    IReadOnlyList<ThemeDescriptor> AvailableThemes { get; }

    string SelectedThemeId { get; }

    ThemeDescriptor ActiveTheme { get; }

    event EventHandler<ThemeChangedEventArgs>? ThemeChanged;

    void Initialize(ResourceDictionary activeResources);

    Task ApplyAsync(
        string themeId,
        CancellationToken cancellationToken = default);

    Task PreviewAsync(
        string themeId,
        CancellationToken cancellationToken = default);
}
```

Adjust exact signatures to fit the project architecture.

## Theme application sequence

Before modifying active resources:

1. resolve the requested theme ID;
2. load the concrete theme dictionary;
3. validate the entire theme contract;
4. validate resource types;
5. only after successful validation update the active resources.

Do not partially apply an invalid theme.

Apply updates on the MAUI UI dispatcher.

Do not clear the active dictionary before applying the new values.

Overwrite the semantic keys after the new palette has been validated, avoiding a period where DynamicResource lookups have no value.

Raise a single `ThemeChanged` notification after the palette has been applied.

## Base MAUI appearance

`Application.UserAppTheme` should remain in use, but its role changes.

It must no longer be the MissionPlanner theme engine.

It should only tell MAUI/native controls whether the selected custom theme is fundamentally light- or dark-based.

For example:

```text
Mission Light -> AppTheme.Light
Mission Blue  -> AppTheme.Light
Mission Dark  -> AppTheme.Dark
```

This also gives UraniumUI and any not-yet-migrated platform/native controls a reasonable fallback.

---

# Task 6 — Implement `System` as a Theme Selection Policy

`System` is not a palette.

When the user selects:

```text
system
```

resolve the active concrete theme from the OS:

```text
OS Light -> mission-light
OS Dark  -> mission-dark
```

When `system` is selected:

```csharp
Application.UserAppTheme = AppTheme.Unspecified;
```

Subscribe to the MAUI requested-theme change event.

If the OS changes from Light to Dark while MissionPlanner is running and `system` is selected:

```text
Mission Light -> Mission Dark
```

must happen automatically.

If the user has explicitly selected:

```text
mission-blue
```

an OS appearance change must **not** replace Mission Blue.

Dispose/unsubscribe correctly.

Avoid recursive theme-change loops when assigning `UserAppTheme`.

## Tests

Cover:

```text
System + OS Light -> Mission Light
System + OS Dark  -> Mission Dark

System OS transition:
Light -> Dark
Dark -> Light

Mission Blue + OS transition:
remains Mission Blue
```

---

# Task 7 — Replace Persisted `PlannerTheme` with `ThemeId`

The current Core model contains:

```csharp
PlannerTheme Theme
bool PreferDarkTheme
```

Replace the persisted application-theme concept with:

```csharp
string ThemeId
```

or preferably a small serializable `ThemeId` value object if doing so remains simple.

Recommended default:

```text
system
```

Remove `PreferDarkTheme` from the current settings contract.

The "Always Use Dark Mode" concept becomes unnecessary because users can explicitly select Mission Dark.

`PlannerTheme` should either be removed completely or retained only temporarily for schema migration.

Do not allow MAUI `AppTheme` to leak into Core.

## Validation

Core settings validation should validate the persisted ID structurally:

- non-null
- non-empty
- sensible maximum length
- restricted to a safe identifier format if appropriate

Core should not depend upon the UI-layer ThemeCatalog.

If the settings contain a structurally valid but unavailable theme ID, the UI ThemeManager must resolve safely to `system` or another documented fallback and log the problem.

This allows future extension themes without introducing a Core/UI dependency.

---

# Task 8 — Migrate Planner Settings Schema from v4 to v5

Increment:

```csharp
PlannerSettings.CurrentSchemaVersion
```

from `4` to `5`.

Extend `PlannerSettingsService.ParseAndMigrate`.

Map existing theme values:

```text
System -> system
Light  -> mission-light
Dark   -> mission-dark
```

The migration must inspect the original JSON where necessary because the old properties may no longer exist in the new CLR model.

The existing `PreferDarkTheme` setting must not cause arbitrary behavioral changes.

Migration precedence should preserve the legacy persisted `Theme` value when it is valid.

Use `PreferDarkTheme` only as a fallback for malformed/older appearance data where an explicit valid legacy `Theme` cannot be determined.

After migration the persisted settings document should contain the new theme ID and schema version 5.

Do not continue writing:

```text
theme: "Dark"
preferDarkTheme: true
```

after successful migration.

## Tests

Add tests for:

```text
v4 System -> v5 system
v4 Light -> v5 mission-light
v4 Dark -> v5 mission-dark

legacy document without theme
legacy malformed theme
existing v5 mission-blue
unknown but structurally valid theme ID
```

Verify migration is persisted exactly once.

---

# Task 9 — Refactor `IPlannerSettingsService.SaveTheme`

Current code uses:

```csharp
SaveTheme(
    PlannerSettings settings,
    PlannerTheme theme,
    bool preferDarkTheme,
    ...)
```

Replace it with an API based on the theme ID:

```csharp
SaveTheme(
    PlannerSettings settings,
    string themeId,
    CancellationToken cancellationToken = default)
```

or equivalent strongly typed `ThemeId`.

Update all callers.

Update `SaveFlyout` so it preserves `ThemeId` rather than reconstructing legacy Light/Dark properties.

Ensure Reset Appearance returns to:

```text
system
```

unless another default is explicitly selected for the project.

---

# Task 10 — Refactor `PlannerSettingsRuntime`

`PlannerSettingsRuntime` currently performs:

```csharp
application.UserAppTheme = ToAppTheme(theme);
```

Remove this conversion logic.

Inject:

```text
IThemeManager
```

and delegate appearance application to it.

`PlannerSettingsRuntime` remains responsible for applying persisted runtime settings, but should not know how a palette is implemented.

Conceptually:

```csharp
themeManager.ApplyAsync(settings.Appearance.ThemeId);
```

Ensure startup application happens after the active ResourceDictionary is available.

Avoid having both `PlannerSettingsRuntime` and a ViewModel independently manipulate `Application.UserAppTheme`.

There must be exactly one theme engine.

---

# Task 11 — Register Theme Services with the Correct Lifetime

Review current DI registration in:

```text
ApplicationConfigurator.cs
```

Register ThemeCatalog and ThemeManager with lifetimes appropriate to application-global state.

The ThemeManager should normally be a singleton.

Review the current `PlannerSettingsRuntime` transient registration as part of this task.

Because it subscribes to global settings events, verify whether singleton lifetime is more appropriate.

Do not accidentally create multiple runtime/theme subscriptions.

## Acceptance test

Resolve the services repeatedly and verify that theme changes do not result in duplicate callbacks.

---

# Task 12 — Connect `App.xaml` and `App.xaml.cs` to the ThemeManager

Preserve the existing application resource merge ordering where possible:

```text
App colors
App styles
UraniumUI Material styles
Application overrides
VirtualizedDataGrid styles
```

Expose the active application color dictionary to ThemeManager.

The current:

```xml
x:Name="AppColors"
```

is useful and may be retained.

Initialize ThemeManager after MAUI has created `Application` resources but before ordinary application content becomes dependent upon the final persisted palette.

Avoid flashing first with one palette and then another if possible.

Do not recreate the whole Application or Shell when changing themes.

Theme changes must occur live.

---

# Task 13 — Convert `Styles.xaml` to `DynamicResource`

The uploaded `Styles.xaml` currently contains approximately 72 `AppThemeBinding` usages.

Replace them with semantic `DynamicResource` lookups.

Example:

Current:

```xml
<Setter
    Property="TextColor"
    Value="{AppThemeBinding
        Light={StaticResource OnPrimary},
        Dark={StaticResource PrimaryDark}}" />
```

New:

```xml
<Setter
    Property="TextColor"
    Value="{DynamicResource OnPrimary}" />
```

Current:

```xml
<Setter
    Property="Stroke"
    Value="{AppThemeBinding
        Light={StaticResource OutlineVariant},
        Dark={StaticResource Gray500}}" />
```

New:

```xml
<Setter
    Property="Stroke"
    Value="{DynamicResource OutlineVariant}" />
```

Use semantic resources for:

- ActivityIndicator
- IndicatorView
- Border
- BoxView
- Button
- CheckBox
- DatePicker
- Editor
- Entry
- Label
- Picker
- ProgressBar
- RadioButton
- RefreshView
- SearchBar
- SearchHandler
- Shadow
- Slider
- SwipeItem
- Switch
- TimePicker
- Shell
- NavigationPage
- TabbedPage
- all remaining styles

Do not preserve Dark branches merely because they currently exist.

The selected palette provides the value.

## Acceptance criterion

`Styles.xaml` contains zero `AppThemeBinding` instances unless a documented non-color platform-specific reason exists.

---

# Task 14 — Convert `Override.xaml` to `DynamicResource`

The current UraniumUI override dictionary contains approximately 9 `AppThemeBinding` usages.

Convert these to semantic dynamic resources.

For example the UraniumUI Button should become conceptually:

```xml
<Style
    TargetType="Button"
    BaseResourceKey="UraniumUI.Styles.Button"
    CanCascade="True">

    <Setter
        Property="TextColor"
        Value="{DynamicResource OnPrimary}" />

    <Setter
        Property="BackgroundColor"
        Value="{DynamicResource Primary}" />

</Style>
```

The UraniumUI `Select` styling should use:

```text
Surface
Outline
PrimaryContainer
SurfaceVariant
```

through `DynamicResource`.

Preserve UraniumUI shadows/elevation behavior unless there is a genuine reason to change it.

## Acceptance criterion

`Override.xaml` contains no theme-specific `*Dark` resource references.

---

# Task 15 — Convert MissionPlanner Application Views

After Styles and Override are converted, migrate the remaining application XAML.

The current source contains approximately 198 `AppThemeBinding` instances overall.

Every ordinary MissionPlanner-owned XAML View must be inspected.

Common conversion:

```xml
BackgroundColor="{AppThemeBinding
    Light={StaticResource Surface},
    Dark={StaticResource SurfaceDark}}"
```

becomes:

```xml
BackgroundColor="{DynamicResource Surface}"
```

Warnings:

```xml
TextColor="{DynamicResource Warning}"
```

Errors:

```xml
TextColor="{DynamicResource Error}"
```

Primary accents:

```xml
TextColor="{DynamicResource Primary}"
```

## Important

Do not mechanically convert a hard-coded Light/Dark pair into one arbitrary existing key.

Determine its semantic role.

Examples requiring consideration include:

```text
AppShell selected-item overlay
Introduction page panels
FlightPlanner dock background
FlightPlanner header background
FlightPlanner dock borders
FlightPlanner splitters
map overlay panels
status text
warning text
parameter validation text
```

If a meaningful reusable semantic resource is missing, add it to the Theme Contract and all palettes.

Prefer existing roles such as:

```text
Surface
SurfaceVariant
SurfaceContainer
Outline
OutlineVariant
PrimaryContainer
```

before creating new keys.

## Completion check

Run a repository search:

```text
AppThemeBinding
```

under:

```text
src/UI/MissionPlanner.App
```

The intended result is zero application-owned Light/Dark color bindings.

Document any deliberate exceptions.

---

# Task 16 — Remove Theme-Suffixed Resource Usage

After application XAML has been converted, search for resources including:

```text
PrimaryDark
OnPrimaryDark
PrimaryContainerDark
OnPrimaryContainerDark

SurfaceDark
OnSurfaceDark
SurfaceVariantDark
OnSurfaceVariantDark

BackgroundDark
OnBackgroundDark

WarningDark
ErrorDark
OutlineDark
OutlineVariantDark
```

Remove MissionPlanner dependencies on them.

Once no application or required UraniumUI integration depends upon these compatibility resources, delete them from `Colors.xaml`.

Do not retain two parallel theme systems indefinitely.

---

# Task 17 — Refactor AppShell Theme Selection

Current AppShell uses:

```csharp
AppTheme[] AppThemeList
AppTheme SelectedTheme
bool PreferDarkTheme
```

Remove those concepts.

Inject or otherwise consume the application ThemeCatalog / ThemeManager.

Expose UI-oriented options such as:

```csharp
public IReadOnlyList<ThemeOption> ThemeOptions { get; }
```

with choices including:

```text
System
Mission Light
Mission Dark
Mission Blue
```

Replace:

```text
Always Use Dark Mode
```

because explicit Mission Dark selection makes it redundant.

Selecting a theme should immediately preview/apply it and persist the selected ThemeId according to the existing Shell UX.

Do not convert ThemeDescriptor directly into a platform dependency in Core.

---

# Task 18 — Refactor Preferences Theme Selection

Current `PreferencesViewModel` uses:

```csharp
Enum.GetValues<PlannerTheme>()
```

and `SelectedTheme`.

Replace this with ThemeCatalog options.

Preserve the existing live-preview behavior.

The Preferences page should display:

```text
System
Mission Light
Mission Dark
Mission Blue
```

using friendly names.

If Preferences supports cancel/revert semantics, previewing a theme must not permanently persist it until the appropriate save action.

If the current page saves immediately, preserve that behavior consistently.

Reset Appearance should restore the documented default ThemeId.

Remove `PreferDarkTheme` UI and ViewModel state.

---

# Task 19 — Audit UraniumUI Material Controls

MissionPlanner currently uses UraniumUI Material controls extensively.

At minimum verify theme behavior for:

```text
SelectField
TabItem
CheckBox
TextField
PickerField
TabView
DataGrid
Button
Select
```

The external UraniumUI library may continue internally to understand only Light and Dark.

That is acceptable as a **native/base fallback**, because ThemeManager sets the selected theme's `BaseAppearance`.

However MissionPlanner-owned overrides must ensure important visible colors come from the active semantic palette.

Mission Blue must visibly affect UraniumUI controls where MissionPlanner expects application styling.

Do not fork the whole UraniumUI package merely to accomplish this unless the override surface becomes unmaintainable.

Prefer application-level override styles first.

---

# Task 20 — Fix the Local VirtualizedDataGrid Theme Integration

The local:

```text
UraniumUI.Material.VirtualizedDataGrid/Resources/StyleResource.xaml
```

currently contains Light/Dark bindings for:

```text
BackgroundColor
LineSeparatorColor
Stroke
SelectionColor
```

Convert these to semantic `DynamicResource` usage.

For example:

```xml
BackgroundColor="{DynamicResource Surface}"
LineSeparatorColor="{DynamicResource Outline}"
Stroke="{DynamicResource Outline}"
SelectionColor="{DynamicResource Primary}"
```

The current `StyleResource.xaml.cs` creates a UraniumUI `ColorResource`, copies overrides into it, and reinvokes `InitializeComponent()` because its `StaticResource` values were captured during initialization.

That mechanism should be reconsidered now that the styles use `DynamicResource`.

Preferred outcome:

- remove the Light/Dark color-copy workaround;
- remove repeated XAML reinitialization if no longer necessary;
- allow the grid to resolve active semantic resources dynamically;
- preserve `ColorsOverride` only if there is still a genuine architectural need.

If resource scoping prevents direct application-level DynamicResource resolution, implement a clean ThemeManager/ThemeChanged refresh boundary rather than reverting to `AppThemeBinding`.

## Required regression test

Switch repeatedly:

```text
Mission Dark
Mission Light
Mission Blue
Mission Dark
```

while a VirtualizedDataGrid is visible.

Verify:

- background changes
- separators change
- border changes
- selection changes
- no duplicate styles
- no exceptions
- no increasing event subscription count

---

# Task 21 — Add Theme-Aware Brushes

Where a Brush rather than a Color is required, define a single dynamic semantic brush resource.

For example:

```xml
<SolidColorBrush
    x:Key="PrimaryBrush"
    Color="{DynamicResource Primary}" />
```

Do not create:

```text
PrimaryBrush
PrimaryDarkBrush
BluePrimaryBrush
```

The brush must track the semantic resource.

Audit existing:

```text
PrimaryBrush
SecondaryBrush
TertiaryBrush
```

which currently use `AppThemeBinding`.

Convert them to dynamic semantics.

---

# Task 22 — Introduce GCS Status Semantics

Take advantage of the theme refactoring to stop using `Primary` for unrelated operational status concepts.

Use semantic status resources:

```text
Success
Warning
Info
Error
```

for application states.

Examples:

```text
successful operation -> Success
warning/failsafe caution -> Warning
informational state -> Info
failure/critical error -> Error
```

Do not blindly change all existing green elements.

Primary remains the normal application/action accent.

This separation is especially useful for a Ground Control Station where operational status has meaning independent of branding.

---

# Task 23 — Register Mission Blue

Add the supplied:

```text
MissionBlue.xaml
```

under:

```text
src/UI/MissionPlanner.App/Resources/Themes/
```

Register:

```text
Id: mission-blue
DisplayName: Mission Blue
BaseAppearance: Light
```

Mission Blue intentionally uses:

```text
blue primary/action colors
cool blue-grey surfaces
amber warnings
green success
red errors
```

This is a functional third theme, not merely a test dictionary.

Selecting Mission Blue must not be internally translated to:

```text
AppTheme.Light
```

as the complete theme decision.

`AppTheme.Light` is only its native base appearance.

Its actual palette comes from MissionBlue.xaml.

---

# Task 24 — Add Theme Contract Tests

Create automated tests that inspect every registered concrete theme.

For each ThemeDescriptor:

1. load its ResourceDictionary;
2. verify every required key exists;
3. verify required color keys contain `Color`;
4. verify no duplicate semantic keys;
5. verify there are no `*Dark` parallel keys in individual palettes;
6. verify no required resource is null.

Also compare the key sets of:

```text
MissionLight
MissionDark
MissionBlue
```

They must agree for the required contract.

A theme with a missing `OnSurface`, for example, must fail a test before runtime.

---

# Task 25 — Add ThemeManager Tests

Test:

```text
initialization
Mission Light application
Mission Dark application
Mission Blue application
repeated switching
invalid theme
unknown theme
missing theme resource
malformed resource dictionary
System resolution
OS theme transition
event notification count
disposal/unsubscription
```

Verify that switching theme replaces semantic values in the active palette.

Specifically assert that:

```text
Surface under Mission Light != Surface under Mission Dark
Primary under Mission Blue != Primary under Mission Light
```

This proves that the active resource layer is actually changing.

---

# Task 26 — Add Settings Migration Tests

Extend `PlannerSettingsTests`.

Cover schema v4 appearance documents containing:

```json
{
  "theme": "System"
}
```

```json
{
  "theme": "Light"
}
```

```json
{
  "theme": "Dark"
}
```

and existing `preferDarkTheme`.

Verify output schema 5 contains the expected `themeId`.

Also test saving and reloading:

```text
mission-blue
```

to demonstrate that Core no longer assumes a finite Light/Dark enum.

---

# Task 27 — Add a Static Architecture Guard

Add a test or repository-check helper that prevents accidental reintroduction of the old pattern.

At minimum scan MissionPlanner-owned XAML for:

```text
AppThemeBinding Light=
SurfaceDark
PrimaryDark
BackgroundDark
WarningDark
ErrorDark
```

Fail the test if these reappear outside explicitly documented compatibility locations.

The objective is to prevent future contributors from writing:

```xml
{AppThemeBinding Light=..., Dark=...}
```

instead of:

```xml
{DynamicResource ...}
```

for application colors.

Do not scan generated build directories.

---

# Task 28 — Hard-Coded Color Audit

Search MissionPlanner.App XAML for direct color literals:

```text
#RRGGBB
#AARRGGBB
White
Black
OrangeRed
Red
Green
```

Review each occurrence.

Not every literal is wrong.

For example colors used in an actual artificial horizon or map symbology may represent domain graphics rather than application chrome.

Classify each occurrence as:

```text
theme semantic
domain/telemetry visualization
image/icon implementation detail
legitimate constant
```

Move application-chrome colors into semantic theme resources.

Do not make domain graphics theme-dependent without good reason.

---

# Task 29 — Verify Flight Data and Map Presentation Carefully

Theme changes must not accidentally recolor map imagery or flight instrumentation in ways that reduce operational readability.

Verify:

```text
HUD
map
Quick tab
Actions
Messages
status bar
top bar
connection controls
mission overlays
flight-planner dock
```

Use semantic theme colors for surrounding application chrome.

Preserve domain-specific colors where they communicate flight state.

Mission Blue should alter surrounding UI without making OpenStreetMap itself blue.

---

# Task 30 — Runtime Theme Switching Regression Test

Perform this manually and/or through suitable UI tests:

```text
Start MissionPlanner
Mission Dark
Mission Light
Mission Blue
Mission Dark
Mission Blue
Mission Light
```

Do this:

```text
disconnected
connected to SITL
while Mandatory Hardware is open
while Optional Hardware is open
while Preferences is open
while Full Parameters is open
while a DataGrid is visible
while Flight Data is visible
```

No restart should be necessary.

Verify:

- no stale old-theme controls
- no mixed Light/Dark resources
- no missing resources
- no visual exception
- no duplicated subscriptions
- no steadily increasing memory caused by theme switching

---

# Task 31 — Platform Verification

Build and smoke-test at least the platforms currently available in CI/development.

Pay special attention to:

```text
WinUI
Android
Mac Catalyst
```

Verify the interaction between:

```text
ThemeBaseAppearance
Application.UserAppTheme
native controls
UraniumUI
MissionPlanner DynamicResource palette
```

A Mission Blue theme should still tell MAUI that it has a Light native base while using the custom Mission Blue palette for MissionPlanner UI.

---

# Task 32 — Document the Theme Architecture

Create:

```text
docs/Theming.md
```

Document:

## Semantic resource rule

Views use:

```xml
{DynamicResource Surface}
```

and do not select Light/Dark values.

## Adding a theme

The documented process should be approximately:

1. create a ResourceDictionary implementing the semantic contract;
2. register a ThemeDescriptor;
3. run theme-contract tests;
4. no View changes.

## System mode

Explain that System resolves Mission Light/Mission Dark from the operating system.

## Base appearance

Explain that `ThemeBaseAppearance` exists only for MAUI/native/UraniumUI compatibility.

## Theme IDs

Explain why persisted string IDs are used instead of a finite enum.

---

# Task 33 — Final Cleanup

Once all application and local UraniumUI code is migrated:

Remove obsolete:

```text
PlannerTheme
PreferDarkTheme
ToAppTheme
ToPlannerTheme
AppThemeList
Selected AppTheme properties
PrimaryDark-style application resources
SurfaceDark-style application resources
legacy Light/Dark brushes
```

Remove migration-only code only if it is safe to stop supporting schema v4.

Normally schema migration support should remain.

Do not remove backward compatibility merely to make the current code look cleaner.

---

# Architectural Rules

The final architecture must obey these rules.

### Rule 1

Views know semantic roles.

```xml
{DynamicResource Surface}
```

### Rule 2

Views do not know concrete themes.

No:

```xml
MissionBlue
Dark
Light
SurfaceDark
```

inside normal Views.

### Rule 3

ThemeManager is the sole runtime theme authority.

### Rule 4

MAUI `AppTheme` is only native/base appearance metadata.

### Rule 5

`System` is a policy, not a palette.

### Rule 6

Concrete themes implement one shared contract.

### Rule 7

Theme selection is persisted by stable ID rather than enum ordinal/name.

### Rule 8

Adding a new theme should not require editing existing Views.

---

# Expected Final Resource Flow

```text
Persisted ThemeId
       │
       ▼
 IThemeManager
       │
       ├──── system ────► OS Light/Dark resolution
       │
       ▼
 ThemeCatalog
       │
       ▼
 Concrete ResourceDictionary
       │
       ├── MissionLight.xaml
       ├── MissionDark.xaml
       └── MissionBlue.xaml
       │
       ▼
 Active AppColors dictionary
       │
       ▼
 Semantic resources
       │
       ├── Surface
       ├── OnSurface
       ├── Primary
       ├── Warning
       ├── Error
       └── ...
       │
       ▼
 Styles / Uranium overrides / Views
       │
       ▼
 {DynamicResource Surface}
```

---

# Definition of Done

The theme refactoring is complete when:

- Mission Light works.
- Mission Dark works and retains the current successful visual appearance.
- Mission Blue works as a genuine third theme.
- System automatically follows the operating system.
- Mission Blue remains selected when the OS changes between Light and Dark.
- Views use semantic `DynamicResource` references.
- MissionPlanner-owned XAML no longer contains Light/Dark color selection.
- Settings schema supports arbitrary Theme IDs.
- Existing schema v4 settings migrate correctly.
- Shell and Preferences use the same ThemeCatalog.
- `PreferDarkTheme` is no longer part of the active model/UI.
- UraniumUI controls remain visually coherent.
- VirtualizedDataGrid updates live.
- no restart is required to change theme.
- theme contract tests pass.
- settings migration tests pass.
- repeated switching causes no duplicate subscriptions or resource accumulation.
- future themes can be introduced without modifying ordinary Views.
