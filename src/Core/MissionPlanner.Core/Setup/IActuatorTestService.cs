using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Setup;

/// <summary>Runs bounded, safety-gated motor tests and reports ESC calibration guidance.</summary>
public interface IActuatorTestService : IDisposable
{
    /// <summary>Gets the current immutable actuator-test state.</summary>
    MotorTestSnapshot Current { get; }

    /// <summary>Gets the maximum permitted test duration in seconds.</summary>
    double MaximumDurationSeconds { get; }

    /// <summary>Gets the maximum permitted throttle percentage.</summary>
    double MaximumThrottlePercent { get; }

    /// <summary>Occurs when the actuator-test state changes.</summary>
    event EventHandler<MotorTestStateChangedEventArgs>? StateChanged;

    /// <summary>Determines whether the vehicle family exposes motor testing.</summary>
    /// <param name="family">The firmware family.</param>
    /// <returns><see langword="true"/> when motor testing applies.</returns>
    bool SupportsMotorTest(FirmwareFamily family);

    /// <summary>Reports whether and how ESC calibration applies to the active vehicle.</summary>
    /// <param name="vehicleId">The active target vehicle.</param>
    /// <returns>The ESC calibration guidance.</returns>
    EscCalibrationGuidance GetEscCalibrationGuidance(VehicleId vehicleId);

    /// <summary>Starts a single bounded motor test after validating safety and bounds.</summary>
    /// <param name="vehicleId">The active target vehicle.</param>
    /// <param name="request">The bounded test request.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The accepted or rejected result.</returns>
    Task<MotorTestResult> TestMotorAsync(VehicleId vehicleId, MotorTestRequest request, CancellationToken cancellationToken = default);

    /// <summary>Starts a bounded sequence test across the given motor count.</summary>
    /// <param name="vehicleId">The active target vehicle.</param>
    /// <param name="throttlePercent">The throttle percentage per motor.</param>
    /// <param name="durationSecondsPerMotor">The bounded per-motor duration.</param>
    /// <param name="motorCount">The number of motors to test in sequence.</param>
    /// <param name="cancellationToken">A connection-scoped cancellation token.</param>
    /// <returns>The accepted or rejected result.</returns>
    Task<MotorTestResult> TestSequenceAsync(VehicleId vehicleId, double throttlePercent, double durationSecondsPerMotor, int motorCount, CancellationToken cancellationToken = default);

    /// <summary>Immediately stops any running actuator test.</summary>
    /// <param name="cancellationToken">A token that cancels the stop transmission.</param>
    /// <returns>A task that completes once the stop is sent and state is stable.</returns>
    Task EmergencyStopAsync(CancellationToken cancellationToken = default);
}
