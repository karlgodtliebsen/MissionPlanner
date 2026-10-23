# Codex Task 01 — Mission Execution State and Verified Onboard Mission Snapshot

## Goal

Add the mission execution state and onboard mission-snapshot infrastructure required by the remaining Actions parity work.

This task is infrastructure only. Do **not** add the Set Current WP / Restart / Resume / Abort Landing UI or backend operations yet.

## Why this task exists

The first parity investigation found that NextGen retained only enough mission state for basic display/use, but not enough to safely decide whether operations such as Abort Landing are applicable.

Modern `MISSION_CURRENT` carries the state needed to solve this cleanly:

- `seq`
- `total`
- `mission_state`
- `mission_mode`
- `mission_id`

The mission protocol also exposes an opaque mission ID during download/upload. A GCS can use these IDs to detect whether its onboard mission snapshot still represents the mission currently held by the autopilot.

## Required investigation before editing

Locate and document the current NextGen implementations for:

- `MISSION_CURRENT` MAVLink record/decoder;
- vehicle navigation/mission state;
- `VehicleSession` or equivalent state application;
- mission download and upload services;
- `MISSION_COUNT` and `MISSION_ACK` handling, including any existing `opaque_id` support;
- selected-vehicle/session lifetime and disconnect replacement;
- existing mission item model, including command IDs/types.

Do not duplicate an existing abstraction if the repository already has the right home for this state.

## 1. Decode and retain complete MISSION_CURRENT execution state

Update the MAVLink layer if necessary so `MISSION_CURRENT` exposes the MAVLink 2 extension fields safely.

The decoder must support both:

- full MAVLink 2 payloads containing the extension fields; and
- shorter/older payloads where extension fields are absent and therefore mean unknown/unsupported.

Do not treat absent/zero extension values as authoritative state.

Expose the information through a typed vehicle-domain state, conceptually equivalent to:

```csharp
VehicleMissionExecutionState
{
    CurrentSequence
    TotalItems
    MissionState
    MissionMode
    MissionId
}
```

Use repository enums/value objects where they already exist. Do not pass raw byte values into the UI layer.

### Semantics

- `CurrentSequence`: current mission sequence from `MISSION_CURRENT.seq`.
- `TotalItems`: known total when valid; preserve the protocol distinction between unsupported/no-mission sentinel values and a real count.
- `MissionState`: typed `MISSION_STATE`, with Unknown when unsupported.
- `MissionMode`: typed semantic representation of unknown / mission mode / suspended.
- `MissionId`: non-zero ID when supported; zero means no usable ID and must not be represented as “verified ID 0”.

## 2. Capture mission snapshot identity during mission download

Extend the existing mission download result/snapshot so it retains:

- selected vehicle identity;
- mission type (normal mission, not fence/rally unless existing code naturally shares the model);
- immutable/copy-safe ordered mission items with their canonical MAVLink sequence and command type;
- the downloaded mission/opaque ID when the protocol provides a non-zero value;
- retrieval timestamp only for diagnostics, not as proof of freshness.

If current mission download code discards `MISSION_COUNT.opaque_id`, retain it.

Do not create a second mission downloader.

## 3. Add explicit snapshot freshness

Provide a typed freshness result rather than a loose boolean hidden in UI code.

Required semantics:

### Verified by Mission ID

A snapshot is **VerifiedCurrent** only when:

```text
snapshot.Vehicle == active vehicle
snapshot.MissionId != 0
executionState.MissionId != 0
snapshot.MissionId == executionState.MissionId
```

### Unverified

If either mission ID is `0` / unsupported, the snapshot may still be useful for display/editing, but it is **not ID-verified**. Do not use timestamps alone to promote it to VerifiedCurrent.

### Stale

If both IDs are non-zero and differ, the snapshot is stale.

## 4. Invalidate correctly

Ensure snapshot/currentness state responds correctly to at least:

- vehicle disconnect;
- replacement/new session for the same SysId/CompId transport context;
- current streamed mission ID changing;
- successful local mission upload returning a different non-zero mission ID;
- mission clear/reset operations already present in the application, where applicable.

A snapshot from vehicle A must never be considered current for vehicle B even when sequence/count happen to match.

## 5. Expose current mission item lookup safely

Provide a typed way for application/policy code to answer:

```text
Do we have a VerifiedCurrent snapshot?
If yes, which mission item corresponds to CurrentSequence?
What command is that item?
```

The result must fail closed when:

- snapshot is absent;
- snapshot is unverified/stale;
- current sequence is absent/out of range;
- the sequence is not present in the downloaded snapshot.

Do not let safety policy index blindly into a list using `CurrentSequence`.

## 6. Event/state propagation

Ensure consumers can react when any relevant value changes:

- current sequence;
- total;
- mission state;
- mission mode;
- mission ID;
- snapshot/freshness.

Use the repository's existing observable state/event architecture. Do not add polling from the Actions ViewModel.

## Out of scope

Do not implement in this task:

- Set Current WP;
- Restart Mission;
- Resume Mission;
- Abort Landing;
- Zero Altitude;
- Change Speed / Altitude / Loiter Radius;
- new Actions XAML.

## Acceptance tests

Add automated coverage at minimum for:

1. A full MAVLink 2 `MISSION_CURRENT` decodes `seq`, `total`, `mission_state`, `mission_mode`, and `mission_id` correctly.
2. A short/legacy `MISSION_CURRENT` payload decodes without error and reports extension state as unknown/unsupported rather than fabricated values.
3. Applying `MISSION_CURRENT` updates only the target vehicle session.
4. A mission download retains canonical sequence, command type, vehicle identity, and non-zero opaque mission ID.
5. Matching non-zero snapshot/execution mission IDs produce `VerifiedCurrent`.
6. Mismatching non-zero IDs produce Stale.
7. Unsupported ID (`0`) produces Unverified, not VerifiedCurrent.
8. Changing the streamed mission ID invalidates the previous verified relationship immediately.
9. Disconnect/session replacement invalidates the previous snapshot relationship.
10. Current-item lookup succeeds only for a verified snapshot and matching sequence.
11. Current-item lookup fails closed for missing/stale/unverified snapshots.
12. Two vehicles with identical mission sequences and counts remain completely isolated by vehicle identity.
13. Existing mission upload/download tests remain green.

## Build/test gate

Build the affected MAVLink/Core/Application projects and run all existing mission protocol/state tests plus the new tests.

If the repository has integration tests for mission download against SITL, run them when practical and report the result separately from unit tests.
