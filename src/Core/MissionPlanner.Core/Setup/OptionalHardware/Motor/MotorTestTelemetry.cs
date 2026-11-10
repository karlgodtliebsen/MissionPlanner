using System.Diagnostics;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.OptionalHardware.Motor;

/// <summary>Waits for the heartbeat to catch up after a bounded Copter motor test.</summary>
public static class MotorTestTelemetry
{
    public static async Task WaitUntilDisarmedAsync(IActiveVehicleContext activeVehicle, VehicleId id,
        CancellationToken cancellationToken)
    {
        // Copter sets motors->armed(true) during motor tests, including in HEARTBEAT.
        // Never start another test or write assignments until it reports disarmed again.
        var elapsed = Stopwatch.StartNew();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (activeVehicle.VehicleId != id || !activeVehicle.IsOnline)
                throw new InvalidOperationException("The vehicle connection changed during the motor test.");
            if (activeVehicle.State?.IsArmed == false) return;
            if (elapsed.Elapsed >= TimeSpan.FromSeconds(4))
                throw new InvalidOperationException("The controller has not reported disarmed after the motor test. Bursts stopped; disarm before continuing.");
            await Task.Delay(50, cancellationToken).ConfigureAwait(false);
        }
    }
}
