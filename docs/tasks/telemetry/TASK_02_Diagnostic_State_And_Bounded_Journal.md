# TASK 02 — Live Diagnostic State and Bounded Journal

## Goal
Provide the state model consumed by the Live Telemetry Inspector.

Combine:
1. authoritative current `VehicleState`;
2. derived diagnostic state;
3. a bounded journal of significant events.

Do not scrape Serilog and do not reread `.tlog`.

## Service
Create a service equivalent in responsibility to:

```csharp
public interface IVehicleLiveDiagnostics
{
    VehicleLiveDiagnosticSnapshot GetSnapshot(VehicleId vehicleId);
    IReadOnlyList<VehicleDiagnosticEvent> GetRecentEvents(VehicleId vehicleId, int maxCount);
    IDisposable Subscribe(VehicleId vehicleId, Action<VehicleLiveDiagnosticUpdate> handler);
}
```

Prefer composition/adapters over duplicating `VehicleState`.

## Journal
Maintain a configurable bounded per-vehicle ring buffer, default around 1000 events.

Useful events:

```text
Connection transition
Arming transition
PreArm reason
Arm failure
Command sent / ACK
STATUSTEXT
RC lost/restored
Battery failsafe
Motor test
Parameter write
Calibration event
Transport fault
User marker
```

High-rate messages such as ATTITUDE, RC_CHANNELS and SERVO_OUTPUT_RAW update current state but should **not** create one journal entry per packet.

## Lifetime
The diagnostic service subscribes to `IVehicleTelemetryEventHub` and continues collecting even when the Inspector is closed. Opening the Drawer after a failure must still show the lead-up events.

## Threading/performance
- no Avalonia dependency in the service;
- thread-safe snapshot/journal;
- no lock-heavy MAVLink receive path;
- UI dispatch happens only at the UI boundary.

## Tests
Cover:
- per-vehicle snapshots;
- event ordering;
- ring-buffer eviction;
- high-rate signals do not flood the journal;
- meaningful transitions do create events;
- collection continues with no UI open;
- concurrent reads/writes are safe.

## Acceptance
Bounded multi-vehicle diagnostic state exists and is independent of Inspector UI lifetime.
