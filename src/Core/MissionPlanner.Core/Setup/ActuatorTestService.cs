using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Generated;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.Core.Setup;

/// <summary>Runs bounded, safety-gated motor tests from MAV_CMD_DO_MOTOR_TEST and reports ESC guidance.</summary>
public sealed class ActuatorTestService : IActuatorTestService
{
    private const ushort MotorTestCommand = (ushort)MavCmd.DoMotorTest;
    private const int MaximumLogEntries = 50;
    private static readonly TimeSpan ackTimeout = TimeSpan.FromSeconds(3);
    private readonly object sync = new();
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IVehicleRegistry vehicleRegistry;
    private readonly IEventHub eventHub;
    private readonly IMavLinkConnection connection;
    private readonly IMavLinkCommandEncoder encoder;
    private readonly IVehicleOperationGate operationGate;
    private readonly IVehicleParameterRegistry parameterRegistry;
    private readonly IDateTimeProvider clock;
    private readonly ILogger<ActuatorTestService> logger;
    private readonly List<ActuatorTestLogEntry> log = [];
    private IDisposable? operationLease;
    private CancellationTokenSource? autoStopCancellation;
    private bool disposed;

    /// <summary>Initializes the actuator-test service.</summary>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="vehicleRegistry">The vehicle registry used to resolve the endpoint.</param>
    /// <param name="eventHub">The decoded MAVLink event stream.</param>
    /// <param name="connection">The MAVLink connection used for protocol commands.</param>
    /// <param name="encoder">The MAVLink command encoder.</param>
    /// <param name="operationGate">The shared vehicle operation gate.</param>
    /// <param name="parameterRegistry">The live parameter registry.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="logger">The logger.</param>
    public ActuatorTestService(
        IActiveVehicleContext activeVehicle,
        IVehicleRegistry vehicleRegistry,
        IEventHub eventHub,
        IMavLinkConnection connection,
        IMavLinkCommandEncoder encoder,
        IVehicleOperationGate operationGate,
        IVehicleParameterRegistry parameterRegistry,
        IDateTimeProvider clock,
        ILogger<ActuatorTestService> logger)
    {
        this.activeVehicle = activeVehicle;
        this.vehicleRegistry = vehicleRegistry;
        this.eventHub = eventHub;
        this.connection = connection;
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
    public bool SupportsMotorTest(FirmwareFamily family) =>
        family is FirmwareFamily.ArduCopter or FirmwareFamily.Rover or FirmwareFamily.ArduSub or FirmwareFamily.Blimp;

    /// <inheritdoc />
    public EscCalibrationGuidance GetEscCalibrationGuidance(VehicleId vehicleId)
    {
        var parameters = parameterRegistry.GetAllParameters(vehicleId);
        var pwmType = parameters.TryGetValue("MOT_PWM_TYPE", out var parameter) ? (int)Math.Round(parameter.Value) : 0;
        // MOT_PWM_TYPE 0/1 are analog PWM/OneShot that require throttle-endpoint calibration; DShot variants do not.
        if (pwmType is 0 or 1)
        {
            return new EscCalibrationGuidance(true, pwmType == 0 ? "Normal PWM" : "OneShot",
                "Analog ESCs learn their throttle endpoints during a manual all-at-once calibration.",
                [
                    "Remove all propellers and disconnect the flight battery.",
                    "Set the throttle stick to maximum, then connect the battery.",
                    "Wait for the ESC tones, then lower the throttle to minimum.",
                    "Confirm the completion tones, then disconnect and reconnect power."
                ]);
        }

        return new EscCalibrationGuidance(false, $"Digital protocol ({pwmType})",
            "Digital ESC protocols such as DShot use fixed throttle endpoints and do not require calibration.",
            []);
    }

    /// <inheritdoc />
    public Task<MotorTestResult> TestMotorAsync(VehicleId vehicleId, MotorTestRequest request, CancellationToken cancellationToken = default)
    {
        if (request.MotorIndex < 1)
        {
            return Task.FromResult(new MotorTestResult(false, "Motor index must be one or greater."));
        }

        if (!TryNormalizeThrottle(request.ThrottleType, request.ThrottleValue, out var throttleType, out var throttleValue, out var throttleError))
        {
            return Task.FromResult(new MotorTestResult(false, throttleError));
        }

        if (!TryBoundDuration(request.DurationSeconds, out var duration, out var durationError))
        {
            return Task.FromResult(new MotorTestResult(false, durationError));
        }

        return RunAsync(vehicleId,
            [request.MotorIndex, (float)throttleType, throttleValue, (float)duration, 1, (float)MotorTestOrder.Board, 0],
            request.MotorIndex, duration,
            $"Motor {request.MotorIndex} at {request.ThrottleValue:0.#} {(request.ThrottleType == MotorThrottleType.Percent ? "%" : "us")} for {duration:0.#}s",
            cancellationToken);
    }

    /// <inheritdoc />
    public Task<MotorTestResult> TestSequenceAsync(VehicleId vehicleId, double throttlePercent, double durationSecondsPerMotor, int motorCount, CancellationToken cancellationToken = default)
    {
        if (motorCount < 1)
        {
            return Task.FromResult(new MotorTestResult(false, "Motor count must be one or greater."));
        }

        if (!TryNormalizeThrottle(MotorThrottleType.Percent, throttlePercent, out var throttleType, out var throttleValue, out var throttleError))
        {
            return Task.FromResult(new MotorTestResult(false, throttleError));
        }

        if (!TryBoundDuration(durationSecondsPerMotor, out var duration, out var durationError))
        {
            return Task.FromResult(new MotorTestResult(false, durationError));
        }

        return RunAsync(vehicleId,
            [1, (float)throttleType, throttleValue, (float)duration, motorCount, (float)MotorTestOrder.Sequence, 0],
            null, duration * motorCount,
            $"Sequence test of {motorCount} motors at {throttlePercent:0.#}% for {duration:0.#}s each",
            cancellationToken);
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
                // Re-issue the motor test at zero throttle for zero seconds to halt output immediately.
                await SendCommandAsync(target, [motor ?? 1, (float)MotorTestThrottleType.MotorTestThrottlePercent, 0, 0, 1, (float)MotorTestOrder.Board, 0], cancellationToken).ConfigureAwait(false);
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
            await SendCommandAsync(vehicleId, parameters, cancellationToken).ConfigureAwait(false);
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

    private async Task SendCommandAsync(VehicleId vehicleId, IReadOnlyList<float> parameters, CancellationToken cancellationToken)
    {
        var session = vehicleRegistry.GetRequired(vehicleId) ?? throw new InvalidOperationException("The target vehicle session is unavailable.");
        var packet = encoder.EncodeCommandLong(vehicleId.SystemId, vehicleId.ComponentId, MotorTestCommand, parameters);
        await connection.SendRawAsync(packet, session.EndPoint, cancellationToken).ConfigureAwait(false);
    }

    private void Finish(MotorTestState state, string instruction, string description, string outcome)
    {
        Transition(Current.VehicleId, state, null, instruction, description, outcome);
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
            if (value < 1000 || value > 2000)
            {
                error = "Throttle PWM must be between 1000 and 2000 microseconds.";
                return false;
            }
        }

        error = string.Empty;
        return true;
    }
}
