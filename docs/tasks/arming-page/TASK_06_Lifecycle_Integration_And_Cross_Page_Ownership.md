# TASK 06 — Lifecycle, Integration and Cross-Page Ownership

## Goal
Integrate Arming cleanly with existing MissionPlanner Setup architecture and avoid creating
overlapping sources of truth with Radio, Safety, Failsafe and Live Diagnostics.

## ViewModel dependencies
Use the actual existing DI contracts from `main`, likely including:
```text
IActiveVehicleContext
IArmingConfigurationService
IVehicleParameterRegistry
parameter-load / metadata context
IVehicleLiveDiagnostics
IVehicleCommandService
IVehicleCommandPolicy
IArmingSetupDocumentFactory
IUserConfirmationService
navigation service
operation runner / operation gate
```

Follow current constructor-injection conventions. Do not inject `IServiceProvider` into
`ArmingViewModel`.

## Activation / deactivation
Follow established Setup lifecycle:
- subscribe only while active;
- release subscriptions on deactivation;
- cancel page-owned operations at vehicle/connection boundaries;
- never retain Vehicle A's document/configuration after selecting Vehicle B;
- clear transient request presentation on reconnect;
- do not subscribe to high-rate telemetry merely to refresh Markdown.

A low-frequency semantic refresh may be used if needed for freshness/expiry behavior, as
with Compass. Only regenerate documents when rendered semantic content changes.

## Parameter loading
Opening the page must consume the existing parameter registry and load-status/metadata
state.

Do not start a competing full parameter download simply because Arming becomes visible.

`Refresh` should follow the same parameter-refresh/reprojection conventions already used
by current Setup pages.

## Radio ownership
Radio currently contains useful RC-specific arming support:
- current arming configuration summary;
- selected AUX channel;
- switch movement observation;
- conflict checking;
- `RCx_OPTION=153` assignment.

After Arming exists:
- Arming is the primary grouped place for arming configuration;
- Radio may retain RC-centric arming diagnostics/assignment because it naturally belongs
  with receiver setup;
- both locations must use the same semantic service / assignment rules;
- a write from either page must be reflected by the other after verified readback;
- there must not be two separate implementations of conflict logic or option 153 rules.

Do not delete the Radio functionality in this task unless an explicit later UX decision
says to do so.

## Safety ownership
`SafetySetupView` remains the broad safety assessment for:
```text
pre-arm configuration evidence
hardware safety switch
radio/throttle failsafe
battery failsafe
GCS failsafe
EKF failsafe
fence
```

Arming owns:
```text
arming readiness
arming configuration
arming methods
current blockers
Arm/Disarm actions
```

If a current blocker belongs elsewhere, explain it and direct the user to the responsible
subsystem rather than duplicating its editor.

Example:
```text
Battery failsafe is currently preventing arming.
Review Battery / Failsafe configuration.
```

## Failsafe ownership
Do not move failsafe configuration into Arming simply because failsafes can prevent arming.

The Arming page may consume their **current diagnostic consequence**, not own their full
configuration.

## Compass ownership
If Compass currently blocks arming, show that current evidence and allow normal navigation
to Compass setup. Do not add Compass settings inside Arming.

## Navigation
Use the same normal MissionPlanner navigation mechanism as Compass for:
```text
Open Full Parameters
```

Where useful, add ordinary UI navigation actions to:
```text
Radio
Safety
Failsafe
Compass
```

Do not make Markdown execute application actions or custom URI schemes.

## DI
Register:
```text
IArmingConfigurationService
IArmingSetupDocumentFactory
```
with lifetimes consistent with the existing Compass/Setup services.

Reuse the existing singleton live-diagnostic service; never instantiate another
`VehicleLiveDiagnostics` for this page.

## Shared UI extraction
Compass + Arming may now reveal genuine repeated patterns.

Only extract a shared setting-row/pending-footer/etc. component when there is demonstrated
duplication after Arming is implemented.

Do not introduce:
```text
GenericSetupViewModel<T>
generic arbitrary-parameter form engine
reflection-driven Setup editor
```

Feature-specific semantics remain in feature services.

## Documentation
Update `docs/SetupUxPattern.md` after implementation:
- Compass remains the first reference;
- Arming becomes the second full semantic consumer;
- record the ownership boundaries above;
- record any truly reusable UI components extracted from two proven pages.

## Acceptance criteria
- Connection/reconnect/vehicle switch cannot leak stale Arming state.
- Hidden Arming page releases page-specific work.
- Opening Arming does not start an independent parameter download.
- Radio and Arming share RC assignment semantics.
- Safety/Failsafe/Compass remain owners of their own configuration.
- No second arming diagnostic or command service is introduced.
