using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Generated;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.Core.Setup;

/// <summary>Implements ArduPilot accelerometer calibration from command and orientation protocol messages.</summary>
public sealed class ArduPilotCalibrationService : IArduPilotCalibrationService
{
    private const ushort PreflightCalibrationCommand = (ushort)MavCmd.PreflightCalibration;
    private const ushort AccelerometerPositionCommand = (ushort)MavCmd.AccelcalVehiclePos;
    private static readonly string[] calibrationParameters =
    [
        "INS_ACCOFFS_X", "INS_ACCOFFS_Y", "INS_ACCOFFS_Z",
        "INS_ACCSCAL_X", "INS_ACCSCAL_Y", "INS_ACCSCAL_Z",
        "AHRS_TRIM_X", "AHRS_TRIM_Y"
    ];
    private readonly object sync = new();
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IVehicleRegistry vehicleRegistry;
    private readonly IEventHub eventHub;
    private readonly IMavLinkConnection connection;
    private readonly IMavLinkCommandEncoder encoder;
    private readonly IVehicleOperationGate operationGate;
    private readonly IVehicleMessageStore messageStore;
    private readonly IVehicleParameterRegistry parameterRegistry;
    private readonly IVehicleParameterService parameterService;
    private readonly ILogger<ArduPilotCalibrationService> logger;
    private readonly TimeSpan startTimeout;
    private readonly TimeSpan levelTimeout;
    private readonly HashSet<CalibrationOrientation> completedOrientations = [];
    private IDisposable? messageSubscription;
    private IDisposable? operationLease;
    private CancellationTokenSource? runCancellation;
    private CancellationTokenRegistration runCancellationRegistration;
    private TaskCompletionSource? startSignal;
    private TaskCompletionSource? terminalSignal;
    private CalibrationOrientation? samplingOrientation;
    private bool disposed;

    /// <summary>Initializes the ArduPilot calibration service.</summary>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="vehicleRegistry">The vehicle registry used to resolve the endpoint.</param>
    /// <param name="eventHub">The decoded MAVLink event stream.</param>
    /// <param name="connection">The MAVLink connection used for protocol replies.</param>
    /// <param name="encoder">The MAVLink command encoder.</param>
    /// <param name="operationGate">The shared vehicle operation gate.</param>
    /// <param name="messageStore">The bounded status-text store.</param>
    /// <param name="parameterRegistry">The live parameter registry.</param>
    /// <param name="parameterService">The parameter request service.</param>
    /// <param name="options">The bounded protocol wait configuration.</param>
    /// <param name="logger">The logger.</param>
    public ArduPilotCalibrationService(
        IActiveVehicleContext activeVehicle,
        IVehicleRegistry vehicleRegistry,
        IEventHub eventHub,
        IMavLinkConnection connection,
        IMavLinkCommandEncoder encoder,
        IVehicleOperationGate operationGate,
        IVehicleMessageStore messageStore,
        IVehicleParameterRegistry parameterRegistry,
        IVehicleParameterService parameterService,
        IOptions<CalibrationOptions> options,
        ILogger<ArduPilotCalibrationService> logger)
    {
        this.activeVehicle = activeVehicle;
        this.vehicleRegistry = vehicleRegistry;
        this.eventHub = eventHub;
        this.connection = connection;
        this.encoder = encoder;
        this.operationGate = operationGate;
        this.messageStore = messageStore;
        this.parameterRegistry = parameterRegistry;
        this.parameterService = parameterService;
        this.logger = logger;
        startTimeout = options.Value.StartTimeout > TimeSpan.Zero ? options.Value.StartTimeout : TimeSpan.FromSeconds(8);
        levelTimeout = options.Value.LevelTimeout > TimeSpan.Zero ? options.Value.LevelTimeout : TimeSpan.FromSeconds(30);
        activeVehicle.Changed += OnActiveVehicleChanged;
    }

    /// <inheritdoc />
    public CalibrationSnapshot Current { get; private set; } = CalibrationSnapshot.Initial;

    /// <inheritdoc />
    public event EventHandler<CalibrationStateChangedEventArgs>? StateChanged;

    /// <inheritdoc />
    public Task StartSixPositionAsync(VehicleId vehicleId, CancellationToken cancellationToken = default) =>
        StartAsync(vehicleId, AccelerometerCalibrationKind.SixPosition, cancellationToken);

    /// <inheritdoc />
    public Task StartLevelAsync(VehicleId vehicleId, CancellationToken cancellationToken = default) =>
        StartAsync(vehicleId, AccelerometerCalibrationKind.Level, cancellationToken);

    /// <inheritdoc />
    public async Task ConfirmOrientationAsync(CancellationToken cancellationToken = default)
    {
        CalibrationOrientation orientation;
        VehicleId vehicleId;
        lock (sync)
        {
            if (Current.State != CalibrationWorkflowState.WaitingForOrientation || Current.RequiredOrientation is not { } required ||
                Current.VehicleId is not { } target)
            {
                throw new InvalidOperationException("Wait for an explicit vehicle orientation request before continuing.");
            }

            orientation = required;
            vehicleId = target;
            samplingOrientation = orientation;
        }

        await SendCommandAsync(vehicleId, AccelerometerPositionCommand, [(float)orientation], cancellationToken).ConfigureAwait(false);
        Transition(Current with
        {
            State = CalibrationWorkflowState.Sampling,
            Progress = Math.Max(Current.Progress, Math.Min(0.99, (completedOrientations.Count + 0.5) / 6d)),
            Instruction = $"Keep the vehicle {OrientationText(orientation)} and motionless while it samples."
        });
    }

    /// <inheritdoc />
    public async Task CancelAsync(CancellationToken cancellationToken = default)
    {
        VehicleId? vehicleId;
        var sendAbort = false;
        lock (sync)
        {
            if (!IsActive(Current.State))
            {
                return;
            }

            vehicleId = Current.VehicleId;
            sendAbort = Current.Kind == AccelerometerCalibrationKind.SixPosition && activeVehicle.IsOnline;
        }

        if (sendAbort && vehicleId is { } target)
        {
            try
            {
                await SendCommandAsync(target, AccelerometerPositionCommand, [(float)AccelcalVehiclePos.Failed], cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Could not send accelerometer calibration abort to {VehicleId}.", target);
            }
        }

        Finish(CalibrationWorkflowState.Cancelled, "Calibration cancelled. Keep the vehicle disarmed and restart when ready.");
    }

    /// <inheritdoc />
    public void Reset()
    {
        lock (sync)
        {
            if (IsActive(Current.State))
            {
                throw new InvalidOperationException("Cancel the active calibration before resetting it.");
            }
        }

        completedOrientations.Clear();
        samplingOrientation = null;
        Transition(CalibrationSnapshot.Initial);
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
        EndRun();
    }

    private async Task StartAsync(VehicleId vehicleId, AccelerometerCalibrationKind kind, CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        var state = activeVehicle.State;
        if (!activeVehicle.IsOnline || activeVehicle.VehicleId != vehicleId || state is null)
        {
            throw new InvalidOperationException("The target vehicle is no longer the active online vehicle.");
        }

        if (state.IsArmed)
        {
            throw new InvalidOperationException("Disarm the vehicle before accelerometer calibration.");
        }

        lock (sync)
        {
            if (IsActive(Current.State))
            {
                throw new InvalidOperationException("A calibration is already active.");
            }
        }

        if (!operationGate.TryAcquire(vehicleId, kind == AccelerometerCalibrationKind.Level ? "level calibration" : "accelerometer calibration", out operationLease))
        {
            throw new InvalidOperationException($"Cannot start calibration while {operationGate.GetCurrentOperation(vehicleId)} is active.");
        }

        completedOrientations.Clear();
        samplingOrientation = null;
        runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, activeVehicle.ConnectionCancellationToken);
        runCancellationRegistration = runCancellation.Token.Register(() =>
        {
            if (!IsActive(Current.State))
            {
                return;
            }

            Finish(
                activeVehicle.IsOnline && activeVehicle.VehicleId == Current.VehicleId
                    ? CalibrationWorkflowState.Cancelled
                    : CalibrationWorkflowState.Disconnected,
                activeVehicle.IsOnline
                    ? "Calibration was cancelled before completion."
                    : "Vehicle disconnected during calibration. Reconnect and restart the workflow.");
        });
        startSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        terminalSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        messageSubscription = eventHub.SubscribeAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage, HandleMessageAsync);
        messageStore.MessageAdded += OnStatusTextAdded;
        Transition(new CalibrationSnapshot(
            vehicleId,
            kind,
            CalibrationWorkflowState.Preparing,
            null,
            new HashSet<CalibrationOrientation>(),
            0,
            kind == AccelerometerCalibrationKind.Level
                ? "Keep the vehicle level and motionless while level calibration starts."
                : "Keep the vehicle disarmed. Waiting for the first orientation request."));
        logger.LogInformation("Starting {CalibrationKind} calibration for {VehicleId}.", kind, vehicleId);

        try
        {
            var action = kind == AccelerometerCalibrationKind.Level
                ? PreflightCalibrationAccelerometer.Trim
                : PreflightCalibrationAccelerometer.Full;
            await SendCommandAsync(vehicleId, PreflightCalibrationCommand, [0, 0, 0, 0, (float)action, 0, 0], runCancellation.Token).ConfigureAwait(false);
            await startSignal.Task.WaitAsync(startTimeout, runCancellation.Token).ConfigureAwait(false);
            if (kind == AccelerometerCalibrationKind.Level && IsActive(Current.State))
            {
                await terminalSignal.Task.WaitAsync(levelTimeout, runCancellation.Token).ConfigureAwait(false);
            }
        }
        catch (TimeoutException)
        {
            Finish(CalibrationWorkflowState.Failed, "Calibration protocol timed out before the vehicle confirmed progress.");
        }
        catch (OperationCanceledException)
        {
            if (!activeVehicle.IsOnline || activeVehicle.VehicleId != vehicleId)
            {
                Finish(CalibrationWorkflowState.Disconnected, "Vehicle disconnected during calibration. Reconnect and restart the workflow.");
            }
            else
            {
                Finish(CalibrationWorkflowState.Cancelled, "Calibration was cancelled before completion.");
            }
        }
        catch
        {
            EndRun();
            throw;
        }
    }

    private Task HandleMessageAsync(MavLinkMessage message, CancellationToken cancellationToken)
    {
        if (Current.VehicleId is not { } vehicleId || message.SystemId != vehicleId.SystemId || message.ComponentId != vehicleId.ComponentId)
        {
            return Task.CompletedTask;
        }

        switch (message)
        {
            case CommandAckMessage acknowledgement when acknowledgement.Command == PreflightCalibrationCommand:
                HandleCalibrationAcknowledgement(acknowledgement);
                break;
            case CommandLongMessage command when command.Command == AccelerometerPositionCommand:
                HandleOrientationSignal(command.Param1);
                break;
        }

        return Task.CompletedTask;
    }

    private void HandleCalibrationAcknowledgement(CommandAckMessage acknowledgement)
    {
        if (acknowledgement.Progress <= 100 && acknowledgement.Progress / 100d > Current.Progress)
        {
            Transition(Current with { Progress = acknowledgement.Progress / 100d });
        }

        var result = (MavResult)acknowledgement.Result;
        if (result == MavResult.InProgress)
        {
            startSignal?.TrySetResult();
            if (Current.Kind == AccelerometerCalibrationKind.Level)
            {
                Transition(Current with
                {
                    State = CalibrationWorkflowState.Completing,
                    Instruction = "Keep the vehicle level and motionless until completion is acknowledged."
                });
            }
            else if (Current.State == CalibrationWorkflowState.Preparing)
            {
                Transition(Current with { Instruction = "Calibration accepted. Waiting for the first orientation request." });
            }

            return;
        }

        if (result == MavResult.Accepted)
        {
            startSignal?.TrySetResult();
            if (Current.Kind == AccelerometerCalibrationKind.Level)
            {
                Finish(CalibrationWorkflowState.Success, "Level calibration was confirmed by COMMAND_ACK.");
            }
            else if (Current.State == CalibrationWorkflowState.Preparing)
            {
                Transition(Current with
                {
                    State = CalibrationWorkflowState.WaitingForOrientation,
                    Instruction = "Calibration accepted. Waiting for the vehicle to request an orientation."
                });
            }

            return;
        }

        var reason = $"Calibration command failed with MAV_RESULT {result} ({acknowledgement.ResultParameter2}).";
        Finish(result == MavResult.Cancelled ? CalibrationWorkflowState.Cancelled : CalibrationWorkflowState.Failed, reason);
    }

    private void HandleOrientationSignal(float rawPosition)
    {
        var value = checked((uint)Math.Round(rawPosition));
        if (value == (uint)AccelcalVehiclePos.Success)
        {
            if (samplingOrientation is { } sampled)
            {
                completedOrientations.Add(sampled);
            }

            Finish(CalibrationWorkflowState.Success, "Six-position accelerometer calibration was explicitly confirmed by the vehicle.");
            return;
        }

        if (value == (uint)AccelcalVehiclePos.Failed)
        {
            Finish(CalibrationWorkflowState.Failed, "The vehicle explicitly reported accelerometer calibration failure.");
            return;
        }

        if (!Enum.IsDefined(typeof(CalibrationOrientation), (int)value))
        {
            return;
        }

        var orientation = (CalibrationOrientation)value;
        startSignal?.TrySetResult();
        lock (sync)
        {
            if (!IsActive(Current.State) || completedOrientations.Contains(orientation))
            {
                return;
            }

            if (Current.State == CalibrationWorkflowState.Sampling && samplingOrientation is { } sampling)
            {
                if (orientation != sampling)
                {
                    completedOrientations.Add(sampling);
                }
            }
            else if (Current.State == CalibrationWorkflowState.WaitingForOrientation &&
                Current.RequiredOrientation is { } current && current != orientation)
            {
                return;
            }

            samplingOrientation = null;
        }

        Transition(Current with
        {
            State = CalibrationWorkflowState.WaitingForOrientation,
            RequiredOrientation = orientation,
            CompletedOrientations = new HashSet<CalibrationOrientation>(completedOrientations),
            Progress = Math.Max(Current.Progress, completedOrientations.Count / 6d),
            Instruction = $"Place the vehicle {OrientationText(orientation)}, keep it motionless, then select Confirm orientation."
        });
    }

    private void OnStatusTextAdded(object? sender, VehicleStatusTextAddedEventArgs args)
    {
        if (args.Message.VehicleId != Current.VehicleId ||
            !args.Message.Text.Contains("calib", StringComparison.OrdinalIgnoreCase) &&
            !args.Message.Text.Contains("place vehicle", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        Transition(Current with { SupplementalStatus = args.Message.Text });
    }

    private void OnActiveVehicleChanged(object? sender, ActiveVehicleChangedEventArgs args)
    {
        if (IsActive(Current.State) &&
            (!args.Current.IsOnline || args.Current.VehicleId != Current.VehicleId))
        {
            Finish(CalibrationWorkflowState.Disconnected, "Vehicle disconnected during calibration. Reconnect and restart the workflow.");
        }
    }

    private async Task SendCommandAsync(VehicleId vehicleId, ushort command, IReadOnlyList<float> parameters, CancellationToken cancellationToken)
    {
        var session = vehicleRegistry.GetRequired(vehicleId) ?? throw new InvalidOperationException("The target vehicle session is unavailable.");
        var packet = encoder.EncodeCommandLong(vehicleId.SystemId, vehicleId.ComponentId, command, parameters);
        await connection.SendRawAsync(packet, session.EndPoint, cancellationToken).ConfigureAwait(false);
    }

    private void Finish(CalibrationWorkflowState state, string message)
    {
        if (!IsActive(Current.State))
        {
            return;
        }

        var success = state == CalibrationWorkflowState.Success;
        Transition(Current with
        {
            State = state,
            RequiredOrientation = null,
            CompletedOrientations = new HashSet<CalibrationOrientation>(completedOrientations),
            Progress = success ? 1 : Current.Progress,
            Instruction = message,
            FailureReason = success ? null : message
        });
        terminalSignal?.TrySetResult();
        startSignal?.TrySetResult();
        logger.LogInformation("Calibration for {VehicleId} finished in state {CalibrationState}.", Current.VehicleId, state);
        EndRun();
        if (success && Current.VehicleId is { } vehicleId)
        {
            _ = RefreshCalibrationParametersAsync(vehicleId);
        }
    }

    private async Task RefreshCalibrationParametersAsync(VehicleId vehicleId)
    {
        try
        {
            foreach (var name in calibrationParameters)
            {
                if (parameterRegistry.GetParameter(vehicleId, name) is not null)
                {
                    await parameterService.RequestParameterAsync(vehicleId, name).ConfigureAwait(false);
                }
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not refresh calibration parameters for {VehicleId}.", vehicleId);
        }
    }

    private void Transition(CalibrationSnapshot snapshot)
    {
        Current = snapshot;
        StateChanged?.Invoke(this, new CalibrationStateChangedEventArgs(snapshot));
    }

    private void EndRun()
    {
        messageSubscription?.Dispose();
        messageSubscription = null;
        messageStore.MessageAdded -= OnStatusTextAdded;
        runCancellationRegistration.Unregister();
        runCancellation?.Cancel();
        runCancellation?.Dispose();
        runCancellation = null;
        operationLease?.Dispose();
        operationLease = null;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

    private static bool IsActive(CalibrationWorkflowState state) => state is
        CalibrationWorkflowState.Preparing or CalibrationWorkflowState.WaitingForOrientation or
        CalibrationWorkflowState.Sampling or CalibrationWorkflowState.Completing;

    private static string OrientationText(CalibrationOrientation orientation) => orientation switch
    {
        CalibrationOrientation.Level => "level on its landing gear",
        CalibrationOrientation.Left => "on its left side",
        CalibrationOrientation.Right => "on its right side",
        CalibrationOrientation.NoseDown => "with its nose pointing straight down",
        CalibrationOrientation.NoseUp => "with its nose pointing straight up",
        CalibrationOrientation.Back => "upside down on its back",
        _ => orientation.ToString()
    };
}
