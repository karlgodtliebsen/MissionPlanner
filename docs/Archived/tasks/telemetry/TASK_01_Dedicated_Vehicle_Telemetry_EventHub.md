# TASK 01 — Dedicated Vehicle Telemetry EventHub

## Goal
Decouple live vehicle diagnostics from the general application EventHub and from UI classes.

## Requirements
Inspect the current EventHub implementation and DI setup first. Reuse the existing EventHub implementation if suitable, but expose a dedicated interface such as:

```csharp
public interface IVehicleTelemetryEventHub
{
    ValueTask PublishAsync<T>(T @event, CancellationToken cancellationToken = default);
    IDisposable Subscribe<T>(Func<T, CancellationToken, ValueTask> handler);
}
```

Adapt signatures to current conventions.

DI must resolve:

```text
IEventHub                 -> existing application EventHub instance
IVehicleTelemetryEventHub -> separate EventHub instance
```

Do not accidentally alias both interfaces to the same singleton object.

## Events
Reuse existing domain events where they already model the required meaning. Add only missing events needed for diagnostics, e.g.:

```text
VehicleConnectionChanged
VehicleHeartbeatObserved
VehicleArmingChanged
VehicleStatusTextReceived
VehicleCommandSent
VehicleCommandAcknowledged
VehicleParameterChanged
VehicleRcInputObserved
VehicleServoOutputObserved
VehiclePowerObserved
VehicleSensorHealthObserved
VehicleMotorTestChanged
```

Normal Inspector panels consume normalized events/state. Raw MAVLink observation is optional and only for the advanced Raw panel.

## Rules
- Publishers must not depend on Inspector UI.
- No-subscriber publish is harmless.
- Slow subscribers must not stall the MAVLink receive loop.
- Subscriber exceptions are contained/logged.
- Events are vehicle-scoped; never assume SysID 1.
- Cancellation/disposal follows connection/application lifetime.

## Tests
Prove:
- application and telemetry EventHubs are distinct instances;
- publish/subscribe works;
- no subscribers is safe;
- one bad subscriber does not break processing;
- vehicle identities remain isolated;
- unsubscribe/disposal works.

## Acceptance
Dedicated telemetry hub exists, uses the current EventHub implementation where practical, is separately registered, and has no UI dependency.
