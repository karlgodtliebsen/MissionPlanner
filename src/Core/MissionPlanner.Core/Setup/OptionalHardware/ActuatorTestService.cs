using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Setup.OptionalHardware.Motor;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Firmware;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Generated;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Runs bounded, safety-gated motor tests from MAV_CMD_DO_MOTOR_TEST and reports ESC guidance.</summary>
public sealed class ActuatorTestService : IActuatorTestService
{
    private const ushort MotorTestCommand = (ushort)MavCmd.DoMotorTest;
    private const ushort ActuatorTestCommand = (ushort)MavCmd.ActuatorTest;
    private const int MaximumLogEntries = 50;
    private static readonly TimeSpan ackTimeout = TimeSpan.FromSeconds(3);
    private readonly Lock sync = new();
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IVehicleRegistry vehicleRegistry;
    private readonly IEventHub eventHub;
    private readonly IMavLinkCommandEncoder encoder;
    private readonly IVehicleOperationGate operationGate;
    private readonly IVehicleParameterRegistry parameterRegistry;
    private readonly IDateTimeProvider clock;
    private readonly ILogger<ActuatorTestService> logger;
    private readonly List<ActuatorTestLogEntry> log = [];
    private IDisposable? operationLease;
    private CancellationTokenSource? autoStopCancellation;
    private IReadOnlyList<ActuatorOutputFunction> activeActuatorFunctions = [];
    private bool disposed;
    private readonly IVehicleConnectionSession session;

    /// <summary>Initializes the actuator-test service.</summary>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="vehicleRegistry">The vehicle registry used to resolve the endpoint.</param>
    /// <param name="eventHub">The decoded MAVLink event stream.</param>
    /// <param name="session"></param>
    /// <param name="encoder">The MAVLink command encoder.</param>
    /// <param name="operationGate">The shared vehicle operation gate.</param>
    /// <param name="parameterRegistry">The live parameter registry.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="logger">The logger.</param>
    public ActuatorTestService(
        IActiveVehicleContext activeVehicle,
        IVehicleRegistry vehicleRegistry,
        IEventHub eventHub,
        IVehicleConnectionSession session,
        IMavLinkCommandEncoder encoder,
        IVehicleOperationGate operationGate,
        IVehicleParameterRegistry parameterRegistry,
        IDateTimeProvider clock,
        ILogger<ActuatorTestService> logger)
    {
        this.activeVehicle = activeVehicle;
        this.vehicleRegistry = vehicleRegistry;
        this.session = session;
        this.eventHub = eventHub;
        this.encoder = encoder;
        this.operationGate = operationGate;
        this.parameterRegistry = parameterRegistry;
        this.clock = clock;
        this.logger = logger;
        activeVehicle.Changed += OnActiveVehicleChanged;
    }

    /// <inheritdoc />
    public MotorTestSnapshot Current { get; private set; } = MotorTestSnapshot.Initial;

    /// <inheritdoc />
    public double MaximumDurationSeconds => 10;

    /// <inheritdoc />
    public double MaximumThrottlePercent => 100;

    /// <inheritdoc />
    public event EventHandler<MotorTestStateChangedEventArgs>? StateChanged;

    /// <inheritdoc />
    public bool SupportsMotorTest(FirmwareFamily family)
    {
        return family is FirmwareFamily.ArduCopter or FirmwareFamily.Rover or FirmwareFamily.ArduSub or FirmwareFamily.Blimp;
    }

    /// <inheritdoc />
    public EscCalibrationGuidance GetEscCalibrationGuidance(VehicleId vehicleId)
    {
        var parameters = parameterRegistry.GetAllParameters(vehicleId);
        var pwmType = parameters.TryGetValue("MOT_PWM_TYPE", out var parameter) ? (int)Math.Round(parameter.Value) : 0;
        // MOT_PWM_TYPE 0/1 are analog PWM/OneShot that require throttle-endpoint calibration; DShot variants do not.
        return pwmType is 0 or 1
            ? new EscCalibrationGuidance(true, pwmType == 0 ? "Normal PWM" : "OneShot",
                "Analog ESCs learn their throttle endpoints during a manual all-at-once calibration.",
                [
                    "Remove all propellers and disconnect the flight battery.",
                    "Set the throttle stick to maximum, then connect the battery.",
                    "Wait for the ESC tones, then lower the throttle to minimum.",
                    "Confirm the completion tones, then disconnect and reconnect power."
                ])
            : new EscCalibrationGuidance(false, $"Digital protocol ({pwmType})",
                "Digital ESC protocols such as DShot use fixed throttle endpoints and do not require calibration.",
                []);
    }

    /// <inheritdoc />
    public Task<MotorTestResult> TestMotorAsync(VehicleId vehicleId, MotorTestRequest request, CancellationToken cancellationToken = default)
    {
        return request.TestOrder < 1
            ? Task.FromResult(new MotorTestResult(false, "Motor test order must be one or greater."))
            : !TryNormalizeThrottle(request.ThrottleType, request.ThrottleValue, out var throttleType, out var throttleValue, out var throttleError)
                ? Task.FromResult(new MotorTestResult(false, throttleError))
                : !TryBoundDuration(request.DurationSeconds, out var duration, out var durationError)
                    ? Task.FromResult(new MotorTestResult(false, durationError))
                    : RunAsync(vehicleId,
                        [request.TestOrder, (float)throttleType, throttleValue, (float)duration, 1, (float)MotorTestOrder.Board, 0],
                        request.TestOrder, duration,
                        $"Motor test {request.TestOrder} at {request.ThrottleValue:0.#} {(request.ThrottleType == MotorThrottleType.Percent ? "%" : "us")} for {duration:0.#}s",
                        cancellationToken);
    }

    /// <inheritdoc />
    public Task<MotorTestResult> TestSequenceAsync(VehicleId vehicleId, double throttlePercent, double durationSecondsPerMotor, int motorCount, CancellationToken cancellationToken = default)
    {
        return motorCount < 1
            ? Task.FromResult(new MotorTestResult(false, "Motor count must be one or greater."))
            : !TryNormalizeThrottle(MotorThrottleType.Percent, throttlePercent, out var throttleType, out var throttleValue, out var throttleError)
                ? Task.FromResult(new MotorTestResult(false, throttleError))
                : !TryBoundDuration(durationSecondsPerMotor, out var duration, out var durationError)
                    ? Task.FromResult(new MotorTestResult(false, durationError))
                    : RunAsync(vehicleId,
                        [1, (float)throttleType, throttleValue, (float)duration, motorCount, (float)MotorTestOrder.Sequence, 0],
                        null, duration * motorCount,
                        $"Sequence test of {motorCount} motors at {throttlePercent:0.#}% for {duration:0.#}s each",
                        cancellationToken);
    }

    /// <inheritdoc />
    public Task<MotorTestResult> TestAllAsync(VehicleId vehicleId, double throttlePercent, double durationSecondsPerMotor, int motorCount, CancellationToken cancellationToken = default)
    {
        return motorCount < 1
            ? Task.FromResult(new MotorTestResult(false, "Motor count must be one or greater."))
            : motorCount > 16
                ? Task.FromResult(new MotorTestResult(false, "MAV_CMD_ACTUATOR_TEST supports Motor1 through Motor16."))
                : !TryNormalizeThrottle(MotorThrottleType.Percent, throttlePercent, out _, out var throttleValue, out var throttleError)
                    ? Task.FromResult(new MotorTestResult(false, throttleError))
                    : durationSecondsPerMotor is <= 0 or > 3
                        ? Task.FromResult(new MotorTestResult(false, "Simultaneous motor-test duration must be between 0 and 3 seconds."))
                        : RunAllAsync(vehicleId, throttleValue / 100f, durationSecondsPerMotor, motorCount, cancellationToken);
    }

    /// <inheritdoc />
    public async Task EmergencyStopAsync(CancellationToken cancellationToken = default)
    {
        VehicleId? vehicleId;
        int? motor;
        lock (sync)
        {
            if (Current.State != MotorTestState.Running)
            {
                return;
            }

            vehicleId = Current.VehicleId;
            motor = Current.ActiveMotor;
        }

        CancelAutoStop();
        if (vehicleId is { } target && activeVehicle.IsOnline)
        {
            try
            {
                if (activeActuatorFunctions.Count > 0)
                {
                    foreach (var function in activeActuatorFunctions)
                    {
                        await SendCommandAsync(target, ActuatorTestCommand, [float.NaN, 0, 0, 0, (float)function, 0, 0], cancellationToken).ConfigureAwait(false);
                    }
                }
                else
                {
                    // Re-issue the motor test at zero throttle for zero seconds to halt output immediately.
                    await SendCommandAsync(target, MotorTestCommand, [motor ?? 1, (float)MotorTestThrottleType.MotorTestThrottlePercent, 0, 0, 1, (float)MotorTestOrder.Board, 0], cancellationToken).ConfigureAwait(false);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Could not send motor-test emergency stop to {VehicleId}.", target);
            }
        }

        Finish(MotorTestState.Stopped, "Actuator test stopped.", "Emergency stop", "Stopped");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        activeVehicle.Changed -= OnActiveVehicleChanged;
        CancelAutoStop();
        ReleaseLease();
    }

    private async Task<MotorTestResult> RunAsync(VehicleId vehicleId, IReadOnlyList<float> parameters, int? activeMotor, double totalDuration, string description, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var state = activeVehicle.State;
        if (!activeVehicle.IsOnline || activeVehicle.VehicleId != vehicleId || state is null)
        {
            return new MotorTestResult(false, "The target vehicle is no longer the active online vehicle.");
        }

        if (state.IsArmed)
        {
            return Reject(vehicleId, description, "Disarm the vehicle before testing actuators.");
        }

        lock (sync)
        {
            if (Current.State == MotorTestState.Running)
            {
                return new MotorTestResult(false, "An actuator test is already running. Stop it before starting another.");
            }

            if (!operationGate.TryAcquire(vehicleId, "motor test", out operationLease))
            {
                return new MotorTestResult(false, $"Cannot start a motor test while {operationGate.GetCurrentOperation(vehicleId)} is active.");
            }
        }

        var ackSignal = new TaskCompletionSource<MavResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = eventHub.SubscribeAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage, (message, _) =>
        {
            if (message is CommandAckMessage acknowledgement && acknowledgement.Command == MotorTestCommand &&
                message.SystemId == vehicleId.SystemId && message.ComponentId == vehicleId.ComponentId)
            {
                ackSignal.TrySetResult((MavResult)acknowledgement.Result);
            }

            return Task.CompletedTask;
        });
        try
        {
            await SendCommandAsync(vehicleId, MotorTestCommand, parameters, cancellationToken).ConfigureAwait(false);
            var result = await ackSignal.Task.WaitAsync(ackTimeout, cancellationToken).ConfigureAwait(false);
            if (result is not (MavResult.Accepted or MavResult.InProgress))
            {
                return Reject(vehicleId, description, $"The vehicle rejected the motor test with MAV_RESULT {result}.");
            }
        }
        catch (TimeoutException)
        {
            return Reject(vehicleId, description, "The vehicle did not acknowledge the motor test in time.");
        }
        catch (OperationCanceledException)
        {
            return Reject(vehicleId, description, "The motor test was cancelled before it started.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Motor test failed for {VehicleId}.", vehicleId);
            return Reject(vehicleId, description, exception.Message);
        }

        Transition(vehicleId, MotorTestState.Running, activeMotor, $"Running: {description}. Release or stop to halt.", description, "Started");
        ScheduleAutoStop(vehicleId, totalDuration);
        logger.LogInformation("Started actuator test for {VehicleId}: {Description}.", vehicleId, description);
        return new MotorTestResult(true, $"Started: {description}.");
    }

    private async Task<MotorTestResult> RunAllAsync(VehicleId vehicleId, float normalizedThrottle, double duration, int motorCount, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var description = $"Simultaneous test of {motorCount} motors at {normalizedThrottle * 100:0.#}% for {duration:0.#}s";
        var state = activeVehicle.State;
        if (!activeVehicle.IsOnline || activeVehicle.VehicleId != vehicleId || state is null)
        {
            return new MotorTestResult(false, "The target vehicle is no longer the active online vehicle.");
        }

        if (state.IsArmed)
        {
            return Reject(vehicleId, description, "Disarm the vehicle before testing actuators.");
        }

        lock (sync)
        {
            if (Current.State == MotorTestState.Running)
            {
                return new MotorTestResult(false, "An actuator test is already running. Stop it before starting another.");
            }

            if (!operationGate.TryAcquire(vehicleId, "motor test", out operationLease))
            {
                return new MotorTestResult(false, $"Cannot start a motor test while {operationGate.GetCurrentOperation(vehicleId)} is active.");
            }
        }

        var acknowledgementSignal = new TaskCompletionSource<MavResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var acceptedCount = 0;
        using var subscription = eventHub.SubscribeAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage, (message, _) =>
        {
            if (message is CommandAckMessage acknowledgement && acknowledgement.Command == ActuatorTestCommand &&
                message.SystemId == vehicleId.SystemId && message.ComponentId == vehicleId.ComponentId)
            {
                var result = (MavResult)acknowledgement.Result;
                if (result is not (MavResult.Accepted or MavResult.InProgress))
                {
                    acknowledgementSignal.TrySetResult(result);
                }
                else if (Interlocked.Increment(ref acceptedCount) >= motorCount)
                {
                    acknowledgementSignal.TrySetResult(MavResult.Accepted);
                }
            }

            return Task.CompletedTask;
        });

        var functions = Enumerable.Range(1, motorCount).Select(value => (ActuatorOutputFunction)value).ToArray();
        try
        {
            foreach (var function in functions)
            {
                await SendCommandAsync(vehicleId, ActuatorTestCommand,
                    [normalizedThrottle, (float)duration, 0, 0, (float)function, 0, 0], cancellationToken).ConfigureAwait(false);
            }

            var result = await acknowledgementSignal.Task.WaitAsync(ackTimeout, cancellationToken).ConfigureAwait(false);
            if (result is not (MavResult.Accepted or MavResult.InProgress))
            {
                return Reject(vehicleId, description, $"The vehicle rejected the actuator test with MAV_RESULT {result}.");
            }
        }
        catch (TimeoutException)
        {
            return Reject(vehicleId, description, "The vehicle did not acknowledge every actuator test in time.");
        }
        catch (OperationCanceledException)
        {
            return Reject(vehicleId, description, "The motor test was cancelled before it started.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Simultaneous motor test failed for {VehicleId}.", vehicleId);
            return Reject(vehicleId, description, exception.Message);
        }

        activeActuatorFunctions = functions;
        Transition(vehicleId, MotorTestState.Running, null, $"Running: {description}. Release or stop to halt.", description, "Started");
        ScheduleAutoStop(vehicleId, duration);
        return new MotorTestResult(true, $"Started: {description}.");
    }

    private void ScheduleAutoStop(VehicleId vehicleId, double totalDuration)
    {
        CancelAutoStop();
        autoStopCancellation = new CancellationTokenSource();
        var token = autoStopCancellation.Token;
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(0.1, totalDuration)), token).ConfigureAwait(false);
                if (!token.IsCancellationRequested && Current.State == MotorTestState.Running && Current.VehicleId == vehicleId)
                {
                    Finish(MotorTestState.Stopped, "Actuator test completed after its bounded duration.", "Auto stop", "Completed");
                }
            }
            catch (OperationCanceledException)
            {
            }
        }, token);
    }

    private MotorTestResult Reject(VehicleId vehicleId, string description, string reason)
    {
        Transition(vehicleId, MotorTestState.Failed, null, reason, description, $"Rejected: {reason}");
        ReleaseLease();
        return new MotorTestResult(false, reason);
    }

    private void OnActiveVehicleChanged(object? sender, ActiveVehicleChangedEventArgs args)
    {
        if (Current.State == MotorTestState.Running && (!args.Current.IsOnline || args.Current.VehicleId != Current.VehicleId))
        {
            CancelAutoStop();
            Finish(MotorTestState.Disconnected, "Vehicle disconnected during an actuator test.", "Disconnect", "Disconnected");
        }
    }

    private async Task SendCommandAsync(VehicleId vehicleId, ushort command, IReadOnlyList<float> parameters, CancellationToken cancellationToken)
    {
        var vehicleSession = vehicleRegistry.GetRequired(vehicleId) ?? throw new InvalidOperationException("The target vehicle session is unavailable.");
        var packet = encoder.EncodeCommandLong(vehicleId.SystemId, vehicleId.ComponentId, command, parameters);
        await session.Connection.SendRawAsync(packet, vehicleSession.EndPoint, cancellationToken).ConfigureAwait(false);
    }

    private void Finish(MotorTestState state, string instruction, string description, string outcome)
    {
        Transition(Current.VehicleId, state, null, instruction, description, outcome);
        activeActuatorFunctions = [];
        ReleaseLease();
    }

    private void Transition(VehicleId? vehicleId, MotorTestState state, int? activeMotor, string instruction, string description, string outcome)
    {
        lock (sync)
        {
            log.Add(new ActuatorTestLogEntry(clock.UtcNow, description, outcome));
            if (log.Count > MaximumLogEntries)
            {
                log.RemoveRange(0, log.Count - MaximumLogEntries);
            }

            Current = new MotorTestSnapshot(vehicleId, state, activeMotor, instruction, log.ToArray(),
                state is MotorTestState.Failed or MotorTestState.Disconnected ? instruction : null);
        }

        StateChanged?.Invoke(this, new MotorTestStateChangedEventArgs(Current));
    }

    private void CancelAutoStop()
    {
        autoStopCancellation?.Cancel();
        autoStopCancellation?.Dispose();
        autoStopCancellation = null;
    }

    private void ReleaseLease()
    {
        operationLease?.Dispose();
        operationLease = null;
    }

    private bool TryBoundDuration(double requested, out double duration, out string error)
    {
        duration = requested;
        if (requested <= 0)
        {
            error = "Test duration must be greater than zero.";
            return false;
        }

        if (requested > MaximumDurationSeconds)
        {
            error = $"Test duration must not exceed {MaximumDurationSeconds:0} seconds.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private bool TryNormalizeThrottle(MotorThrottleType type, double value, out MotorTestThrottleType throttleType, out float throttleValue, out string error)
    {
        throttleValue = (float)value;
        if (type == MotorThrottleType.Percent)
        {
            throttleType = MotorTestThrottleType.MotorTestThrottlePercent;
            if (value < 0 || value > MaximumThrottlePercent)
            {
                error = $"Throttle percentage must be between 0 and {MaximumThrottlePercent:0}.";
                return false;
            }
        }
        else
        {
            throttleType = MotorTestThrottleType.MotorTestThrottlePwm;
            if (value is < 1000 or > 2000)
            {
                error = "Throttle PWM must be between 1000 and 2000 microseconds.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }
}
