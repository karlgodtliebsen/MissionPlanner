# Subsystem-oriented Setup UX

Compass is the first semantic Setup page. Its structure is **Status → Configuration →
Calibration / Actions → Advanced**, followed by a pending-change footer only while
values differ from the confirmed configuration.

## Ownership

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
