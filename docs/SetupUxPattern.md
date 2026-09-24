# Subsystem-oriented Setup UX

Compass is the first semantic Setup page. Its structure is **Status → Configuration →
Calibration / Actions → Advanced**, followed by a pending-change footer only while
values differ from the confirmed configuration.

## Setup page layout and actions

Use `CompassSetupView` as the visual reference for setup content in InstallFirmware,
MandatoryHardware, and OptionalHardware. The content root is a centered Grid with
the rows required by the page and one constrained column:

```xml
<Grid RowDefinitions="Auto,*" HorizontalAlignment="Center">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*" MinWidth="600" MaxWidth="900" />
    </Grid.ColumnDefinitions>
</Grid>
```

Let cards and scrollable content stretch within that column. Do not use the
`SourceBorder` class, a named sizing border, or sibling `Bounds.Width` bindings
to size these pages. Keep `SectionCard` on cards. Navigation hosts retain room
for their left-hand tabs; constrain the content of each tab instead. Dialogs and
reusable firmware fragments retain their containing layout.

Use `ToolbarIconButton` for page actions and `ToolbarIconDropDownButton` for
toolbar menus, with `ActionIcon`, a tooltip, and an accessible name. Actions
inside cards or editor rows use `EmbeddedIconButton` or `EmbeddedTextButton`.
Calibration actions belong with their instructions, and receiver actions belong
with receiver settings. Keep existing command and availability bindings. Use
wrapping action groups and vertical cards where needed at the minimum width;
wide parameter tables should retain their own horizontal scrolling. Dialog and
drawer X buttons continue to use Ursa's `OverlayCloseButton` theme.

## Information and Configuration tabs

Setup content now separates reports from configuration controls. Safety and Setup
Summary retain their existing layouts. Compass retains its dedicated semantic
reporting. Hardware ID and Antenna Tracker currently have no setup operations and
therefore expose Information only. Firmware selection and confirmation dialogs
retain their focused layouts; firmware installation exposes Information and
Configuration, with help/support actions under Configuration.

`ISetupReportService.Capture` produces a plain `SetupReport` for each subsystem
from the selected vehicle and existing parameter/load-status caches. It performs
no protocol operations, metadata downloads, parameter writes, or device discovery.
Topic definitions specify relevant configuration evidence and workflow guidance.
Missing/failed/partial loads never export parameter assignments; disconnects and
vehicle changes remove the prior vehicle's evidence. Reported settings are not
presented as proof of physical hardware presence or health. Local serial and
analysis tools report their own operation observations separately from vehicle
configuration and never include editable credentials.

`SetupReportDocumentFactory` owns Markdown formatting and escaping. Confirmed
parameters use copyable `NAME = value` assignments, with unavailable values as
comments. Hardware identifiers retain hexadecimal diagnostic comments. The
overview separates workflow messages from confirmed vehicle observations.
`SetupInformationView` renders the overview and optional parameter document using
`InformationDocumentView` inside a ScrollViewer. Its own injected ViewModel is
scoped to the visible Information tab, listens for connection/load completion,
and refreshes cached evidence once per second while visible. Identical Markdown
preserves the document instance and selection; hiding or disposing the view
releases subscriptions and cancels polling. The parent setup ViewModel continues
to own all configuration actions and their existing guards.

Firmware discovery reporting uses `FirmwareDiscoveryReporting` with observations
from the existing serial/DFU discovery models. It distinguishes telemetry, serial
candidates, USB endpoints, runtime identity, installation progress and platform
availability without triggering another scan. Discovery notifications update the
Markdown only when its content changes.

## Compass ownership

The view binds friendly concepts. The ViewModel owns only local desired values and
presentation/lifecycle state. The existing `ICompassConfigurationService` translates
semantic `CompassConfiguration` / `CompassSetting` values to explicit firmware
parameters. It reads the existing vehicle parameter registry and metadata service;
there is no separate parameter cache or MAVLink access in the ViewModel.

The service remains registered as the existing transient Compass service. Contextual
`IParameterEditSession` instances are created through the existing `IDomainFactory`
registration with a captured vehicle/firmware scope. The shared vehicle operation gate
prevents configuration writes from competing with calibration or vehicle commands.

## Friendly settings and capability

A `CompassSettingDefinition` supplies the friendly label, explanation, current value,
metadata choices/default, reboot requirement, edit capability, and optional diagnostic
parameter name. Null values remain unknown. Absent secondary/tertiary controls are hidden;
missing primary controls stay visible with an explanation. Enum choices require firmware
metadata. Binary compass enable/use switches have explicit semantic Off/On meanings.

Non-binary external sensor modes are preserved as enum values, including forced-external
mode. Unknown rotations, yaw choices and external modes are never coerced to a guessed
value. Metadata defaults are retained by the existing XML parser only when explicitly
present and finite. An unavailable default reads **Unknown** and cannot be reset.

Device labels retain reported IDs and the existing bus/address/type decode. No sensor
model is invented. The first configured EKF source set is editable here; advanced
telemetry also lists the other reported source sets.

## Current, pending, and reviewed values

Local edits do not write parameters. Each edit produces a semantic evaluation and
explicit parameter review, including old/new values, dependency explanations, validation
errors and reboot requirements. Per-setting markers show friendly current-to-pending
values. Reset to default changes only the pending value. Discard restores current values.

When disabling the subsystem, available compass-use flags are staged Off and a
compass-dependent first yaw source is staged None. Both appear in the review. Other
reported EKF source sets that still require a compass block disabling and direct the user
to explicitly review those advanced configurations; they are never silently changed.
Selecting compass-dependent yaw while already disabled is invalid.

Apply requires an online, disarmed vehicle and explicit confirmation of the complete
review. The service revalidates vehicle, firmware, connection generation, current values,
metadata capabilities and dependencies. It removes the yaw dependency before disabling
usage/subsystem; enabling occurs before dependent usage/yaw writes. Each parameter goes
through the existing write-plan and live-readback verification machinery. The first
failure stops later writes. Results report confirmed names, actual state when available,
and any reboot requirement; unconfirmed edits remain pending.

A disconnect retains local edits for the same page/vehicle lifetime while disabling
Apply. Reconnection reads the current registry after the connection-owned parameter load;
changed current values block the old review. Vehicle/firmware changes and page deactivation
invalidate local edits. Late loads/evaluations cannot overwrite newer page/connection state.
No second parameter download is started merely by opening the page.

## Status and diagnostics

Configuration validity, hardware detection and live health are separate. Health uses
fresh SYS_STATUS magnetometer evidence (five seconds); missing/expired health stays
Unknown. Arming impact uses only current diagnostic reasons/readiness. Historical PreArm
messages are not recycled into current blockers. Enabled without a detected device is
reported as a configuration warning, not proof that the flight controller rejected arming.

The visible page refreshes semantic state once per second from the registry/metadata cache
and listens for parameter-load completion. Hidden pages release subscriptions and timers.
The collapsed Advanced section contains relevant raw values, device IDs, metadata/source
availability and health timestamps, plus navigation to the existing Full Parameters page.

## Calibration and reboot

The existing onboard compass calibration service still owns start, progress, explicit
acceptance, cancellation and success evidence. Calibration is unavailable when the compass
is disabled, edits are pending, the vehicle is armed/disconnected, or a confirmed change
still needs reboot. Calibration completion still records Setup workflow evidence.

Reboot requirements aggregate from firmware metadata for confirmed changes. Reboot is an
explicit, separately confirmed action through `IVehicleCommandService`; it is never
automatic. The page keeps the requirement until the requested reboot crosses a reconnect
boundary. A command acknowledgement alone does not claim the reboot has completed.

## Future reuse and Arming

Reuse existing SectionCard styling, the parameter edit/readback pipeline, confirmation,
navigation, operation gate and immutable semantic snapshots. Keep feature-specific rules
in feature services. The Compass row is intentionally a small composed ViewModel, not an
arbitrary-parameter form or a generic Setup base class.

After hardware validation, candidates for a second consumer are: a label/description and
secondary diagnostic row, default/reset affordance, validation banner and pending footer.
Extract only demonstrated duplication. Do not generalize the feature-to-parameter mapping.

An Arming page can follow the same structure:

- **Status:** Ready / Not Ready / Armed, fresh reasons and observed request source.
- **Configuration:** semantic arming checks, stick arming, location requirements and RC
  Arm/Disarm assignment, mapped by an Arming configuration service.
- **Actions:** existing guarded Arm / Disarm commands.
- **Advanced:** relevant ARMING_* / RC option evidence and Full Parameters navigation.

No full Arming page is introduced by this change.

## Information documents

Status is an `InformationDocumentView` bound to the ViewModel's immutable
`UserDocument`. `CompassSetupDocumentFactory` composes explanations in the App
presentation layer, using existing structured service facts. Native controls still
own configuration, calibration, Discard, and Apply. The dependency directions are:

```text
View -> ViewModel -> Feature Configuration Service -> Parameter subsystem
                   -> Application document factory -> UserDocument -> UI wrapper
```

See [MarkdownDocuments.md](MarkdownDocuments.md) for the LiveMarkdown 2.4.3 API,
escaping, restricted parsing, selection, copy actions, theme mapping, update
comparison, and the real-hardware acceptance checklist. The page scrolls as a
whole and has no fixed desktop minimum width or document height.

The `compass-live-markdown` package supersedes the earlier Compass UX package and
explicitly introduces this wrapper. Further migrations (Arming, Firmware, Battery,
GPS, or other Setup pages) remain gated on real-hardware validation of Compass.
