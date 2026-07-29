# Vehicle Parameters

How MissionPlanner requests, streams, stores, edits and enriches ArduPilot parameters.

This document consolidates the earlier PARAMETER_USAGE, PARAMETER_STREAMING_* and
PARAMETER_METADATA_SYSTEM documents and reflects the current code
(`MissionPlanner.Core/Vehicles` and `MissionPlanner.MavLink/Parameters`).

---

## Building blocks

| Concern | Type | Location |
|---|---|---|
| Parameter value object | `VehicleParameter`, `MavParamType` | `MissionPlanner.MavLink.Parameters` |
| Request/set single parameters | `IVehicleParameterService` → `VehicleParameterService` | `MissionPlanner.Core.Vehicles` |
| Bulk download with progress/retry | `IVehicleParameterStreamService` → `VehicleParameterStreamServiceV4` | `MissionPlanner.Core.Vehicles` |
| Per-vehicle storage | `IVehicleParameterRegistry` → `VehicleParameterRegistry` (singleton) | `MissionPlanner.Core.Vehicles` |
| Rich metadata (descriptions, ranges, units) | `IVehicleParameterMetadataService` → `VehicleParameterMetadataService` | `MissionPlanner.Core.Vehicles` + `MissionPlanner.MavLink.Parameters.Metadata` |
| Incoming PARAM_VALUE handling | `ParamValueVehicleHandler` → stores + publishes `VehicleParameterReceived` | `MissionPlanner.Core.Vehicles.Handlers` |
| Shared Config edit state | `IParameterEditSessionFactory` → `ParameterEditSessionFactory` | `MissionPlanner.Core.Configuration` |
| Save/load parameter files (UI) | `ParametersFileHandler` | `MissionPlanner.App.Views.ConfigTuning` |
| Parameter editor UI | `FullParametersListTabView(Model)` | `MissionPlanner.App.Views.ConfigTuning.Tabs` |

All services are registered in `DomainConfigurator.AddDomainServices`.

---

## Requesting and setting parameters

`IVehicleParameterService` (`MissionPlanner.Core.Vehicles.Abstractions`):

```csharp
Task<bool> RequestParameterListAsync(VehicleId vehicleId, CancellationToken ct = default);
Task<bool> RequestParameterAsync(VehicleId vehicleId, string parameterName, CancellationToken ct = default);
Task<bool> RequestParameterByIndexAsync(VehicleId vehicleId, ushort parameterIndex, CancellationToken ct = default);
Task<bool> SetParameterAsync(VehicleId vehicleId, string parameterName, float value, MavParamType paramType, CancellationToken ct = default);
```

MAVLink message flow:

```
Request all:   GCS → PARAM_REQUEST_LIST,  Vehicle → PARAM_VALUE × N
Request one:   GCS → PARAM_REQUEST_READ,  Vehicle → PARAM_VALUE
Set:           GCS → PARAM_SET,           Vehicle → PARAM_VALUE (confirmation)
```

Notes:

- Parameter names are limited to 16 characters (MAVLink spec).
- Values travel as 32-bit floats on the wire; `MavParamType` says how to interpret them
  (`Uint8/Int8/Uint16/Int16/Uint32/Int32/Real32`).
- Each received `PARAM_VALUE` is stored in the registry by `ParamValueVehicleHandler` and
  published as a `VehicleParameterReceived` domain event (index/count carry download progress).

Stored values are read back from `IVehicleParameterRegistry`:

```csharp
var one  = parameterRegistry.GetParameter(vehicleId, "ACRO_RP_P");
var all  = parameterRegistry.GetAllParameters(vehicleId);   // IReadOnlyDictionary<string, VehicleParameter>
parameterRegistry.ClearParameters(vehicleId);
```

---

## Bulk download (streaming service)

Fetching ~1000 parameters one-by-one is far too slow, and a naive PARAM_REQUEST_LIST
listener misses messages. The streaming service handles the full download with progress
and retry of missing indexes:

```csharp
var result = await streamService.StreamAllParametersWithRetryAsync(
    vehicleId,
    progress: new Progress<ParameterStreamProgress>(p =>
        Status = $"{p.ReceivedCount}/{p.TotalCount} ({p.PercentComplete}%)"),
    maxRetries: 3,
    timeout: TimeSpan.FromSeconds(60));

if (result.Success)
{
    // result.Parameters: IReadOnlyDictionary<string, VehicleParameter>
    // result.TotalCount, result.Duration
}
else
{
    // result.ErrorMessage
}
```

The active implementation is `VehicleParameterStreamServiceV4`. Its key design point
(learned the hard way through V1–V3, whose write-ups this doc replaces): subscribe to the
decoded MAVLink `PARAM_VALUE` message stream directly — *before* sending
PARAM_REQUEST_LIST — instead of listening to higher-level domain events, so no early
parameters are lost while subscriptions spin up. Missing indexes are re-requested
individually via `RequestParameterByIndexAsync` up to `maxRetries` times. Default overall
timeout is 30 s.

> `VehicleParameterStreamService` (V1), `V2` and `V3` still exist in the code but are not
> registered anywhere — they are historical iterations and can be deleted.

---

## Parameter metadata

Rich, human-readable information about each parameter (description, range, units,
enumerated values, bitmask bits, read-only/reboot flags) comes from the ArduPilot
parameter definition XML files.

```
IVehicleParameterMetadataService     (lookup by VehicleId or VehicleType)
        │
IParameterMetadataRepository         (in-memory + file cache, 7-day expiry)
        │
ParameterMetadataDownloader          (https://autotest.ardupilot.org/Parameters/{vehicle}/apm.pdef.xml.gz)
ParameterMetadataXmlParser           (apm.pdef.xml format)
```

API (`MissionPlanner.Core.Vehicles.Abstractions`):

```csharp
Task<ParameterMetadata?> GetMetadataAsync(VehicleId vehicleId, string parameterName, CancellationToken ct = default);
Task<ParameterMetadata?> GetMetadataAsync(VehicleType vehicleType, string parameterName, CancellationToken ct = default);
Task<IReadOnlyDictionary<string, ParameterMetadata>> GetAllMetadataAsync(VehicleId vehicleId, CancellationToken ct = default);
Task<IReadOnlyDictionary<string, ParameterMetadata>> GetAllMetadataAsync(VehicleType vehicleType, CancellationToken ct = default);
Task RefreshMetadataAsync(VehicleType vehicleType, CancellationToken ct = default);
```

`ParameterMetadata` (`MissionPlanner.MavLink.Parameters`) carries
`DisplayName/Description/Units/Range/Values/Bitmask/Increment/UserLevel/RebootRequired/ReadOnly`
plus helpers: `MinValue`, `MaxValue`, `GetValueOptions()` (enum params),
`GetBitmaskOptions()` (flag params), `IsValueValid(value)` and `GetValidationError(value)`.

Practical points:

- The vehicle type is resolved automatically from the vehicle's heartbeat via
  `IVehicleRegistry` (`VehicleTypeUtil`).
- File cache lives under the local app-data folder (`.../MissionPlanner/ParameterCache/{VehicleType}_metadata.xml`), expires after 7 days, refreshes automatically.
- First call per vehicle type costs a few seconds (download + parse); cached lookups are
  in-memory. Concurrent requests for the same vehicle type are deduplicated.
- Missing metadata is normal — always fall back:
  `var title = metadata?.DisplayName ?? parameterName;`

### Validation pattern before setting a parameter

```csharp
var metadata = await metadataService.GetMetadataAsync(vehicleId, name);
if (metadata is { ReadOnly: true }) return Fail("read-only");
if (metadata?.GetValidationError(newValue) is { } error) return Fail(error);
if (metadata is { RebootRequired: true }) WarnRebootRequired();
await parameterService.SetParameterAsync(vehicleId, name, newValue, param.Type);
```

### Shared configuration editing session

Config pages edit parameters through the singleton `IParameterEditSessionFactory`. The
factory creates one session for the active `VehicleId` and reported firmware identity. A
session retains the value first loaded, the latest confirmed live value, and the pending
editor value separately. It also projects ranges, increments, enum values, bitmask flags,
units, descriptions, read-only state, and reboot requirements from firmware metadata.
Because `ParameterEditScope` is local to the selected vehicle and firmware, the factory
creates `IParameterEditSession` through `IDomainFactory`, which supplies the remaining
constructor dependencies from DI.

Pending edits are validated immediately. Apply first creates an immutable write plan with
the exact live and pending values, units, difference, read-only/validation state, and reboot
flag. The UI displays that snapshot and requires explicit `Write N parameters`
confirmation. The session rechecks vehicle/firmware scope and every planned value after
confirmation, so a stale preview cannot send anything.

Confirmed plans are written sequentially through `IVehicleParameterService`. Progress
reports validation, write, readback confirmation, completion, and skipped phases for each
name. A value succeeds only after equivalent registry readback. Reports retain every
outcome (`Confirmed`, `Unchanged`, `ValidationFailed`, `WriteFailed`, `ReadbackFailed`, or
`Skipped`). Failed writes remain pending; retry selects only write/readback failures that
are still valid and modified and never rewrites confirmed values. Reboot-required state is
aggregated across a partial apply and later retry.

The factory invalidates the session when the vehicle disconnects, the active vehicle
changes, or the firmware identity changes. Invalid sessions retain pending edits for user
review but refuse all writes. Leaving the Config workspace with unapplied edits requires
explicit confirmation; moving between Config tabs keeps the shared edits intact.

Firmware-specific Config pages define ordered aliases with `ParameterFieldDefinition` and
optional `ParameterPresenceRule` predicates. Resolution selects only an alias that exists
in the live registry and whose explicit presence rule is satisfied; it never invents or
silently guesses a parameter name.

`FullParametersListTabViewModel` uses this session after its existing bulk download. The
bulk service continues to prefer packed MAVFTP parameters and automatically falls back to
the classic parameter stream. File imports populate pending values, while Apply performs
confirmed session writes. Session change notifications update existing table rows in place
and preserve row identity; they do not recreate enum and bitmask choices while an editor is
handling input. The ViewModel exposes one stable all-row collection.
`VirtualizedDataGrid` owns UI filtering, paging, counts, and visible-row realization,
avoiding a second filtered collection and clear/add churn. Disposal only detaches/cancels
ownership; it never clears a bound collection. Multi-option changes are committed as one
pending value.
The session projection retains the source unit text, user level, range, value, bitmask, and
increment metadata used by the raw editor. When numeric metadata provides both range bounds
but no increment, the increment/decrement controls use the rounded range divided by ten.
Numeric stepping uses decimal arithmetic to avoid accumulated binary floating-point drift;
an overshooting step lands on the advertised minimum or maximum instead of being ignored.

### Comparison and profiles

`IParameterValueEquivalence` is the single numeric policy used by modified detection,
comparison, imports, and matching readback. Metadata increment supplies precision when
present; otherwise comparisons use absolute `1e-6` and relative `1e-5` tolerances. NaN and
infinity are handled explicitly and are never accepted as staged parameter values.

`IParameterComparisonService` compares live, pending, original, file, or profile sources
using the union of names, so source-only entries are retained. Rows distinguish equal,
different, left/right-only, invalid, read-only, and missing-metadata values. Only finite,
writable differences already present in the target session are stageable. Staging calls
`TrySetPending`; it never sends `PARAM_SET`. JSON and CSV exports retain source identity,
firmware identity, timestamp, status, values, metadata, and messages.

Named profiles are schema-versioned JSON documents containing source identity, timestamps,
firmware family/version and frame scope, tags, and values. `IParameterProfileRepository`
keeps persistence replaceable; its local implementation writes a temporary document and
atomically replaces the target. Loading a profile must go through comparison and staging,
then the normal preview/confirmation/write path.

### Troubleshooting writes and imports

- **Write rejected:** the value remains modified with `WriteFailed`; correct the value or
  connection issue and retry failed entries.
- **Readback timeout:** the write was sent but not proven. Refresh that field before retry
  if the vehicle state is ambiguous.
- **Stale session/preview:** reconnect or refresh after a vehicle, connection, firmware, or
  pending-value change, then create a new preview.
- **Firmware mismatch:** inspect comparison warnings and stage only entries known to be
  compatible with the connected firmware.
- **Missing metadata:** the value remains visible, but automatic safety/staging is disabled
  until metadata is available.
- **Absent parameter:** file/profile-only entries remain in comparison as unsupported and
  are not staged.
- **Disconnect/cancellation:** no new write starts after cancellation; already confirmed
  results remain recorded and all remaining targets are skipped.

`GeoFenceTabViewModel` opens the same session with an explicit fence field catalog. It
resolves only parameters present for the connected firmware and commits parameter changes
before uploading typed fence geometry. Cross-field checks, such as minimum/maximum altitude
ordering and return-altitude bounds, run together with geometry validation; a rejected or
unconfirmed parameter write prevents the geometry phase.

Basic Tuning also opens the shared session, but first selects a curated profile for the
reported Copter, Plane, Rover, or Sub family. Each logical field resolves only an explicitly
listed live parameter or justified legacy alias. Metadata determines the actual editor,
range, enum values, increment, read-only state, and reported units; the catalog supplies a
plain-language description and unit fallback. Group apply/revert/refresh operations contain
only fields displayed in that group and retain the session's confirmed-readback behavior.

Coupled group validation runs before writes and after imports. Basic Tuning JSON is
invariant, family-tagged, and restricted to the active profile's presented parameter names.
An invalid import restores the previous pending values atomically. Static recommendations
are not guessed: the UI only exposes a recommended/default value when the catalog has both
an authoritative value and its source.

Extended Tuning applies the same presence and metadata rules to reusable advanced
descriptors. Descriptor expansion generates repeated axis and sensor-instance names for
controller, filter, estimator, and navigation families, then removes parameters absent from
the live registry. Editor rows are materialized only when a virtualized group is expanded or
matched by search. Cross-field rules and normalized axis comparisons operate on pending
session values.

Axis copy is deliberately two-stage: the Core service creates a scope-bound, non-mutating
preview, and only an unchanged preview can be copied into pending state after confirmation.
Writing still happens later through the group's shared-session apply and readback. The UI
shows `PID_TUNING` response metrics as read-only context; the metrics collector never sends
commands and Extended Tuning does not execute autotune.

Onboard OSD performs discovery before opening its editor: live names matching numbered
`OSD<n>_*` screen and item-property patterns select the fields loaded into the shared
session. Item stems are not hard-coded, so custom firmware items with enable, X, and Y
parameters appear automatically; further parameters under the same stem are exposed as
metadata-backed options. Coordinate metadata supplies character-grid bounds.

OSD validation rejects non-integral/out-of-grid positions and same-cell collisions unless
screen metadata explicitly advertises dynamic/overlapping items. Such collisions remain
warnings that require confirmation before group apply. Layout import is firmware-family-
tagged, restricted to discovered OSD parameters, and restores previous pending state on any
metadata or bounds error. Reset means revert to confirmed live values; firmware defaults are
not guessed.

The Planner tab is intentionally outside this parameter architecture. Its units, map,
theme, logging, connection-default, cache, confirmation, update, and accessibility values
are local application preferences; saving or importing them never creates a parameter edit
session and never sends `PARAM_SET`.

### Frame setup transaction

Initial Setup uses `IFrameConfigurationService` rather than writing parameters from the
ViewModel. Its family catalog identifies candidate frame parameter names, while live
parameter presence and metadata enum values determine what is actually visible. Copter uses
`FRAME_CLASS` and `FRAME_TYPE`; Plane uses the corresponding `Q_FRAME_CLASS` and
`Q_FRAME_TYPE` values when the connected firmware exposes them; Rover choices are likewise
presence- and metadata-gated.

The service revalidates every reviewed value, writes changes sequentially, and waits for the
matching registry readback after each `PARAM_SET`. If a later write fails it attempts to
restore already-confirmed values in reverse order and reports anything that needs manual
review. Cancellation is connection-scoped. The UI shows current and pending values,
metadata reboot requirements, and separately selected initial recommendations; it stores
Setup evidence only after every requested value was confirmed.

Accelerometer calibration requests fresh `INS_ACCOFFS_*`, `INS_ACCSCAL_*`, and
`AHRS_TRIM_*` values after protocol-confirmed success when those parameters are present in
the registry. Calibration completion is not inferred from a parameter write or UI action.

---

## Known issues / next steps

- Parameter loading into the Full Parameters List UI is slow (see FEATURES.md) — the
  merge of ~1000 values with metadata needs profiling.
- Delete the unused `VehicleParameterStreamService` V1–V3 classes.
- The comparison engine and exports are implemented; the MAUI workflow currently exposes
  live-versus-pending review while richer file/profile source selection is being expanded.
