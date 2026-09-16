using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.OptionalHardware.Motor;

/// <summary>One user-observed motor start threshold in a frame-derived workflow.</summary>
/// <param name="Motor">Logical motor, test order, and frame position.</param>
/// <param name="OutputChannel">Resolved physical output, when unambiguous.</param>
/// <param name="ThresholdPercent">User-confirmed reliable rotation percentage.</param>
public sealed record MotorThresholdMeasurement(MotorLayoutMotor Motor, int? OutputChannel, double? ThresholdPercent)
{
    /// <summary>Gets the motor, frame position, physical output, and observed threshold.</summary>
    public string Display => $"{Motor.Label} · Position roll={Motor.Roll:0.##}, pitch={Motor.Pitch:0.##} · " +
        $"Output {OutputChannel?.ToString() ?? "unknown"} · Threshold {ThresholdPercent?.ToString("0.#") ?? "not measured"}%";
}

/// <summary>Guides bounded pulses and confirmed parameter writes using existing motor services.</summary>
/// <param name="activeVehicle">Active vehicle and connection lifetime.</param>
/// <param name="parameters">Current downloaded parameters.</param>
/// <param name="resolver">Existing frame layout resolver.</param>
/// <param name="outputs">Existing physical motor-output mapping.</param>
/// <param name="actuators">Existing guarded motor test workflow.</param>
/// <param name="spinParameters">Existing confirmed parameter-write workflow.</param>
/// <param name="gate">Shared vehicle-operation ownership.</param>
public sealed class MotorStartThresholdService(
    IActiveVehicleContext activeVehicle,
    IVehicleParameterRegistry parameters,
    MotorLayoutResolver resolver,
    IMotorOutputResolver outputs,
    IActuatorTestService actuators,
    IMotorSpinParameterService spinParameters,
    IVehicleOperationGate gate) : IDisposable
{
    private VehicleId? vehicleId;
    private MotorLayout? layout;
    private CancellationTokenSource? lifetime;
    private readonly List<MotorThresholdMeasurement> measurements = [];
    private bool tested;
    private bool busy;

    /// <summary>Gets whether a run still owns its acknowledged vehicle context.</summary>
    public bool HasSession => vehicleId is not null;

    /// <summary>Gets frame-derived motor measurements in test order.</summary>
    public IReadOnlyList<MotorThresholdMeasurement> Measurements => measurements.ToArray();

    /// <summary>Gets the next motor that needs a reliable-rotation observation.</summary>
    public MotorThresholdMeasurement? CurrentMotor => measurements.FirstOrDefault(item => item.ThresholdPercent is null);

    /// <summary>Gets the next pulse percentage; each motor starts at five percent.</summary>
    public double TestPercent { get; private set; } = 5;

    /// <summary>Gets the latest workflow instruction or result.</summary>
    public string Instruction { get; private set; } = "Start the assistant after removing all propellers.";

    /// <summary>Gets whether every motor has a user-confirmed threshold.</summary>
    public bool IsComplete => vehicleId is not null && measurements.Count > 0 && CurrentMotor is null;

    /// <summary>Starts a new frame-aware run after explicit props-removed acknowledgement.</summary>
    public void Start(bool propsRemoved)
    {
        if (!propsRemoved)
        {
            throw new InvalidOperationException("Explicit props-removed acknowledgement is required.");
        }
        if (busy)
        {
            throw new InvalidOperationException("Wait for the current pulse or write to finish.");
        }
        if (!activeVehicle.IsOnline || activeVehicle.VehicleId is not { } id || activeVehicle.State?.IsArmed != false)
        {
            throw new InvalidOperationException("Connect and disarm the vehicle before testing.");
        }
        layout = resolver.Resolve(parameters.GetAllParameters(id)) ?? throw new InvalidOperationException("A supported frame layout is required.");
        lifetime?.Cancel();
        lifetime?.Dispose();
        lifetime = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken);
        vehicleId = id;
        measurements.Clear();
        measurements.AddRange(layout.Motors.OrderBy(motor => motor.TestOrder)
            .Select(motor => new MotorThresholdMeasurement(motor, outputs.Resolve(id, motor.MotorNumber).OutputChannel, null)));
        TestPercent = 5;
        tested = false;
        Instruction = "Pulse the selected motor, then confirm reliable rotation or increase by 1%.";
    }

    /// <summary>Increases the next pulse by one percentage point without sending a command.</summary>
    public void Increase()
    {
        EnsureReady();
        if (busy || CurrentMotor is null || TestPercent >= Math.Min(25, actuators.MaximumThrottlePercent))
        {
            throw new InvalidOperationException("The next pulse cannot be increased.");
        }
        TestPercent++;
        tested = false;
    }

    /// <summary>Commands one one-second pulse and waits for its bounded duration before observation.</summary>
    public async Task PulseAsync(CancellationToken cancellationToken = default)
    {
        var id = EnsureReady();
        if (busy || CurrentMotor is not { } motor)
        {
            throw new InvalidOperationException("Select an unmeasured motor and wait for any active operation.");
        }
        busy = true;
        tested = false;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime!.Token);
        try
        {
            var result = await actuators.TestMotorAsync(id,
                new MotorTestRequest(motor.Motor.TestOrder, MotorThrottleType.Percent, TestPercent, 1), linked.Token).ConfigureAwait(false);
            Instruction = result.Message;
            if (result.Success)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), linked.Token).ConfigureAwait(false);
                EnsureReady();
                tested = true;
                Instruction = "Did this motor rotate reliably? Confirm the observation or increase by 1% and pulse again.";
            }
        }
        finally
        {
            busy = false;
        }
    }

    /// <summary>Records the user's reliable-rotation observation and advances to the next motor.</summary>
    public void ConfirmReliableRotation()
    {
        EnsureReady();
        if (busy || !tested || CurrentMotor is not { } motor)
        {
            throw new InvalidOperationException("Complete a successful pulse before confirming observed rotation.");
        }
        var index = measurements.IndexOf(motor);
        measurements[index] = motor with { ThresholdPercent = TestPercent };
        TestPercent = 5;
        tested = false;
        Instruction = IsComplete ? "All motors measured. Review the proposed values before writing." : "Next motor selected. Start with a low pulse.";
    }

    /// <summary>Calculates normalized armed/minimum values from the highest observed threshold.</summary>
    public static (double HighestPercent, double SpinArm, double SpinMin) Recommend(
        IReadOnlyList<double> thresholds, double armMargin = 2, double minimumMargin = 3)
    {
        if (thresholds.Count == 0 || thresholds.Any(value => !double.IsFinite(value) || value < 0) ||
            !double.IsFinite(armMargin) || !double.IsFinite(minimumMargin) || armMargin < 1 || minimumMargin < 1)
        {
            throw new ArgumentException("Measured thresholds and positive safety margins are required.");
        }
        var highest = thresholds.Max();
        var arm = highest + armMargin;
        var minimum = arm + minimumMargin;
        if (arm >= 20 || minimum > 20)
        {
            throw new InvalidOperationException("The recommendation exceeds the assistant's 20% setup limit. Review the motor/ESC configuration.");
        }
        return (highest, arm / 100, minimum / 100);
    }

    /// <summary>Writes the reviewed pair only after explicit confirmation, preserving arm/minimum ordering.</summary>
    public async Task<MotorSpinWriteResult> ApplyAsync(bool confirmed, CancellationToken cancellationToken = default)
    {
        if (!confirmed)
        {
            return new(false, "No parameters written; confirmation was declined.");
        }
        var id = EnsureReady();
        if (!IsComplete || busy)
        {
            return new(false, "Finish all motor observations before writing.");
        }
        var recommendation = Recommend(measurements.Select(item => item.ThresholdPercent!.Value).ToArray());
        if (!gate.TryAcquire(id, "motor start threshold parameters", out var lease))
        {
            return new(false, "Another vehicle operation is active.");
        }
        using (lease)
        using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime!.Token))
        {
            busy = true;
            try
            {
                var state = spinParameters.GetState(id);
                if (state.SpinArmPercent is not { } currentArm || state.SpinMinNormalized is not { } currentMin)
                {
                    return new(false, "Download both motor spin parameters before applying.");
                }
                // Raise MIN first only if the new ARM would otherwise violate the current ordering.
                if (recommendation.SpinArm >= currentMin)
                {
                    var minimumResult = await spinParameters.SetSpinMinAsync(id, recommendation.SpinMin * 100 - currentArm, linked.Token).ConfigureAwait(false);
                    if (!minimumResult.Success)
                    {
                        return minimumResult;
                    }
                }
                EnsureReady();
                var armResult = await spinParameters.SetSpinArmAsync(id, recommendation.HighestPercent, 2, linked.Token).ConfigureAwait(false);
                if (!armResult.Success)
                {
                    return new(false, armResult.Message + " Refresh values; an earlier write may already have succeeded.");
                }
                EnsureReady();
                var result = await spinParameters.SetSpinMinAsync(id, 3, linked.Token).ConfigureAwait(false);
                Instruction = result.Message;
                return result;
            }
            finally
            {
                busy = false;
            }
        }
    }

    /// <summary>Cancels the run, requests a motor stop, and never writes parameters.</summary>
    public async Task CancelAsync(CancellationToken cancellationToken = default)
    {
        lifetime?.Cancel();
        vehicleId = null;
        tested = false;
        Instruction = "Assistant cancelled. No further pulses or parameter writes will be sent.";
        await actuators.EmergencyStopAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lifetime?.Cancel();
        lifetime?.Dispose();
        lifetime = null;
        vehicleId = null;
    }

    private VehicleId EnsureReady()
    {
        if (vehicleId is not { } id || lifetime?.IsCancellationRequested != false ||
            activeVehicle.VehicleId != id || !activeVehicle.IsOnline || activeVehicle.State?.IsArmed != false)
        {
            throw new InvalidOperationException("The assistant requires its original connected, disarmed vehicle.");
        }
        var current = resolver.Resolve(parameters.GetAllParameters(id));
        if (current?.FrameClass != layout?.FrameClass || current?.FrameType != layout?.FrameType ||
            measurements.Any(item => outputs.Resolve(id, item.Motor.MotorNumber).OutputChannel != item.OutputChannel))
        {
            throw new InvalidOperationException("Frame or output mapping changed. Restart the assistant.");
        }
        return id;
    }
}
