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

The UI editor (`FullParametersListTabViewModel`) merges metadata with live values, which
is also the model for future editors: ComboBox for `Values`, checkboxes for `Bitmask`,
numeric editors clamped to `Range`, tooltips from `Description`.

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
- Parameter write support in the editor UI is incomplete.
- Delete the unused `VehicleParameterStreamService` V1–V3 classes.
- Parameter file compare (diff two `.param` files) is not yet implemented.
