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

/// <summary>Implements ArduPilot onboard compass calibration from MAG_CAL progress and report messages.</summary>
public sealed class ArduPilotCompassCalibrationService : IArduPilotCompassCalibrationService
{
    private const ushort StartCommand = (ushort)MavCmd.DoStartMagCal;
    private const ushort AcceptCommand = (ushort)MavCmd.DoAcceptMagCal;
    private const ushort CancelCommand = (ushort)MavCmd.DoCancelMagCal;
    private readonly object sync = new();
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IVehicleRegistry vehicleRegistry;
    private readonly IEventHub eventHub;
    private readonly IMavLinkConnection connection;
    private readonly IMavLinkCommandEncoder encoder;
    private readonly IVehicleOperationGate operationGate;
    private readonly ILogger<ArduPilotCompassCalibrationService> logger;
    private readonly TimeSpan startTimeout;
    private readonly SortedDictionary<int, CompassCalibrationProgress> progress = [];
    private readonly SortedDictionary<int, CompassCalibrationReport> reports = [];
    private readonly HashSet<int> expectedCompasses = [];
    private IDisposable? messageSubscription;
    private IDisposable? operationLease;
    private CancellationTokenSource? runCancellation;
    private CancellationTokenRegistration runCancellationRegistration;
    private TaskCompletionSource? startSignal;
    private bool autoSaveRequested;
    private bool disposed;

    /// <summary>Initializes the ArduPilot compass calibration service.</summary>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="vehicleRegistry">The vehicle registry used to resolve the endpoint.</param>
    /// <param name="eventHub">The decoded MAVLink event stream.</param>
    /// <param name="connection">The MAVLink connection used for protocol replies.</param>
    /// <param name="encoder">The MAVLink command encoder.</param>
    /// <param name="operationGate">The shared vehicle operation gate.</param>
    /// <param name="options">The bounded protocol wait configuration.</param>
    /// <param name="logger">The logger.</param>
    public ArduPilotCompassCalibrationService(
        IActiveVehicleContext activeVehicle,
        IVehicleRegistry vehicleRegistry,
        IEventHub eventHub,
        IMavLinkConnection connection,
        IMavLinkCommandEncoder encoder,
        IVehicleOperationGate operationGate,
        IOptions<CompassCalibrationOptions> options,
        ILogger<ArduPilotCompassCalibrationService> logger)
    {
        this.activeVehicle = activeVehicle;
        this.vehicleRegistry = vehicleRegistry;
        this.eventHub = eventHub;
        this.connection = connection;
        this.encoder = encoder;
        this.operationGate = operationGate;
        this.logger = logger;
        startTimeout = options.Value.StartTimeout > TimeSpan.Zero ? options.Value.StartTimeout : TimeSpan.FromSeconds(8);
        activeVehicle.Changed += OnActiveVehicleChanged;
    }

    /// <inheritdoc />
    public CompassCalibrationSnapshot Current { get; private set; } = CompassCalibrationSnapshot.Initial;

    /// <inheritdoc />
    public event EventHandler<CompassCalibrationStateChangedEventArgs>? StateChanged;

    /// <inheritdoc />
    public async Task StartAsync(VehicleId vehicleId, bool autoSave, CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var state = activeVehicle.State;
        if (!activeVehicle.IsOnline || activeVehicle.VehicleId != vehicleId || state is null)
        {
            throw new InvalidOperationException("The target vehicle is no longer the active online vehicle.");
        }

        if (state.IsArmed)
        {
            throw new InvalidOperationException("Disarm the vehicle before compass calibration.");
        }

        lock (sync)
        {
            if (IsActive(Current.State))
            {
                throw new InvalidOperationException("A compass calibration is already active.");
            }
        }

        if (!operationGate.TryAcquire(vehicleId, "compass calibration", out operationLease))
        {
            throw new InvalidOperationException($"Cannot start calibration while {operationGate.GetCurrentOperation(vehicleId)} is active.");
        }

        progress.Clear();
        reports.Clear();
        expectedCompasses.Clear();
        autoSaveRequested = autoSave;
        runCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, activeVehicle.ConnectionCancellationToken);
        runCancellationRegistration = runCancellation.Token.Register(() =>
        {
            if (!IsActive(Current.State))
            {
                return;
            }

            Finish(
                activeVehicle.IsOnline && activeVehicle.VehicleId == Current.VehicleId
                    ? CompassCalibrationWorkflowState.Cancelled
                    : CompassCalibrationWorkflowState.Disconnected,
                activeVehicle.IsOnline
                    ? "Compass calibration was cancelled before completion."
                    : "Vehicle disconnected during compass calibration. Reconnect and restart the workflow.");
        });
        startSignal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        messageSubscription = eventHub.SubscribeAsync<MavLinkMessage>(MavLinkEventTopics.ReceivedMessage, HandleMessageAsync);
        Transition(new CompassCalibrationSnapshot(
            vehicleId,
            CompassCalibrationWorkflowState.Preparing,
            [],
            [],
            0,
            "Waiting for the vehicle to start compass calibration. Prepare to rotate the vehicle through all axes.",
            false));
        logger.LogInformation("Starting compass calibration for {VehicleId} (autoSave={AutoSave}).", vehicleId, autoSave);

        try
        {
            // p1 mag_mask (0 = all), p2 retry, p3 autosave, p4 delay, p5 autoreboot.
            await SendCommandAsync(vehicleId, StartCommand, [0, 1, autoSave ? 1 : 0, 0, 0, 0, 0], runCancellation.Token).ConfigureAwait(false);
            await startSignal.Task.WaitAsync(startTimeout, runCancellation.Token).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            Finish(CompassCalibrationWorkflowState.Failed, "Compass calibration protocol timed out before the vehicle confirmed progress.");
        }
        catch (OperationCanceledException)
        {
            if (!activeVehicle.IsOnline || activeVehicle.VehicleId != vehicleId)
            {
                Finish(CompassCalibrationWorkflowState.Disconnected, "Vehicle disconnected during compass calibration. Reconnect and restart the workflow.");
            }
            else
            {
                Finish(CompassCalibrationWorkflowState.Cancelled, "Compass calibration was cancelled before completion.");
            }
        }
        catch
        {
            EndRun();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task AcceptAsync(CancellationToken cancellationToken = default)
    {
        VehicleId vehicleId;
        lock (sync)
        {
            if (Current.State != CompassCalibrationWorkflowState.PendingAcceptance || Current.VehicleId is not { } target)
            {
                throw new InvalidOperationException("There are no pending compass calibration results to accept.");
            }

            vehicleId = target;
        }

        await SendCommandAsync(vehicleId, AcceptCommand, [0, 0, 0, 0, 0, 0, 0], cancellationToken).ConfigureAwait(false);
        Finish(CompassCalibrationWorkflowState.Success, "Compass calibration results were accepted and saved to the vehicle.");
    }

    /// <inheritdoc />
    public async Task CancelAsync(CancellationToken cancellationToken = default)
    {
        VehicleId? vehicleId;
        var sendCancel = false;
        lock (sync)
        {
            if (!IsActive(Current.State))
            {
                return;
            }

            vehicleId = Current.VehicleId;
            sendCancel = activeVehicle.IsOnline;
        }

        if (sendCancel && vehicleId is { } target)
        {
            try
            {
                await SendCommandAsync(target, CancelCommand, [0, 0, 0, 0, 0, 0, 0], cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Could not send compass calibration cancel to {VehicleId}.", target);
            }
        }

        Finish(CompassCalibrationWorkflowState.Cancelled, "Compass calibration cancelled. Restart when ready.");
    }

    /// <inheritdoc />
    public void Reset()
    {
        lock (sync)
        {
            if (IsActive(Current.State))
            {
                throw new InvalidOperationException("Cancel the active compass calibration before resetting it.");
            }
        }

        progress.Clear();
        reports.Clear();
        expectedCompasses.Clear();
        Transition(CompassCalibrationSnapshot.Initial);
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

    private Task HandleMessageAsync(MavLinkMessage message, CancellationToken cancellationToken)
    {
        if (Current.VehicleId is not { } vehicleId || message.SystemId != vehicleId.SystemId || message.ComponentId != vehicleId.ComponentId)
        {
            return Task.CompletedTask;
        }

        switch (message)
        {
            case CommandAckMessage acknowledgement when acknowledgement.Command == StartCommand:
                HandleStartAcknowledgement(acknowledgement);
                break;
            case MagCalProgressMessage progressMessage:
                HandleProgress(progressMessage);
                break;
            case MagCalReportMessage reportMessage:
                HandleReport(reportMessage);
                break;
        }

        return Task.CompletedTask;
    }

    private void HandleStartAcknowledgement(CommandAckMessage acknowledgement)
    {
        var result = (MavResult)acknowledgement.Result;
        if (result is MavResult.Accepted or MavResult.InProgress)
        {
            startSignal?.TrySetResult();
            lock (sync)
            {
                if (Current.State == CompassCalibrationWorkflowState.Preparing)
                {
                    Transition(Current with
                    {
                        State = CompassCalibrationWorkflowState.Running,
                        Instruction = "Rotate the vehicle so that each side points down toward the earth in turn until each compass completes."
                    });
                }
            }

            return;
        }

        startSignal?.TrySetResult();
        Finish(
            result == MavResult.Cancelled ? CompassCalibrationWorkflowState.Cancelled : CompassCalibrationWorkflowState.Failed,
            $"The vehicle rejected compass calibration startup with MAV_RESULT {result}.");
    }

    private void HandleProgress(MagCalProgressMessage message)
    {
        startSignal?.TrySetResult();
        lock (sync)
        {
            if (!IsActive(Current.State))
            {
                return;
            }

            RegisterExpected(message.CompassId, message.CalMask);
            progress[message.CompassId] = new CompassCalibrationProgress(
                message.CompassId,
                MapStatus(message.CalStatus),
                Math.Clamp((int)message.CompletionPct, 0, 100),
                message.Attempt);

            Transition(Current with
            {
                State = CompassCalibrationWorkflowState.Running,
                Progress = progress.Values.ToArray(),
                OverallProgress = CalculateOverallProgress(),
                Instruction = "Keep rotating the vehicle through all orientations until every compass reaches one hundred percent."
            });
        }
    }

    private void HandleReport(MagCalReportMessage message)
    {
        lock (sync)
        {
            if (!IsActive(Current.State))
            {
                return;
            }

            RegisterExpected(message.CompassId, message.CalMask);
            var status = (MagCalStatus)message.CalStatus;
            var success = status == MagCalStatus.MagCalSuccess;
            reports[message.CompassId] = new CompassCalibrationReport(
                message.CompassId,
                success,
                message.Autosaved != 0,
                message.Fitness,
                message.OfsX,
                message.OfsY,
                message.OfsZ,
                message.OldOrientation,
                message.NewOrientation,
                message.OrientationConfidence);
            progress[message.CompassId] = new CompassCalibrationProgress(
                message.CompassId,
                MapStatus(message.CalStatus),
                100,
                progress.TryGetValue(message.CompassId, out var existing) ? existing.Attempt : 0);

            Transition(Current with
            {
                Progress = progress.Values.ToArray(),
                Reports = reports.Values.ToArray(),
                OverallProgress = CalculateOverallProgress()
            });

            TryFinalize();
        }
    }

    private void TryFinalize()
    {
        if (expectedCompasses.Count == 0 || !expectedCompasses.All(reports.ContainsKey))
        {
            return;
        }

        var completed = expectedCompasses.Select(id => reports[id]).ToArray();
        var summary = BuildQualitySummary(completed);
        if (completed.Any(report => !report.Success))
        {
            var failed = string.Join(", ", completed.Where(report => !report.Success).Select(report => $"compass {report.CompassId + 1}"));
            FinishWithSummary(CompassCalibrationWorkflowState.Failed, $"Calibration failed for {failed}. Review interference and retry.", summary);
            return;
        }

        if (completed.All(report => report.Autosaved) || autoSaveRequested)
        {
            FinishWithSummary(CompassCalibrationWorkflowState.Success, "All compasses calibrated and saved.", summary);
            return;
        }

        Transition(Current with
        {
            State = CompassCalibrationWorkflowState.PendingAcceptance,
            OverallProgress = 1,
            RequiresAcceptance = true,
            Instruction = "Calibration succeeded. Review the quality summary and accept to save the new offsets.",
            QualitySummary = summary
        });
    }

    private void RegisterExpected(int compassId, byte calMask)
    {
        expectedCompasses.Add(compassId);
        for (var bit = 0; bit < 8; bit++)
        {
            if ((calMask & (1 << bit)) != 0)
            {
                expectedCompasses.Add(bit);
            }
        }
    }

    private double CalculateOverallProgress()
    {
        if (expectedCompasses.Count == 0)
        {
            return progress.Count == 0 ? 0 : progress.Values.Average(item => item.CompletionPercent) / 100d;
        }

        var total = 0d;
        foreach (var id in expectedCompasses)
        {
            total += reports.ContainsKey(id) ? 100d : progress.TryGetValue(id, out var item) ? item.CompletionPercent : 0d;
        }

        return Math.Clamp(total / (expectedCompasses.Count * 100d), 0, 1);
    }

    private void OnActiveVehicleChanged(object? sender, ActiveVehicleChangedEventArgs args)
    {
        if (IsActive(Current.State) &&
            (!args.Current.IsOnline || args.Current.VehicleId != Current.VehicleId))
        {
            Finish(CompassCalibrationWorkflowState.Disconnected, "Vehicle disconnected during compass calibration. Reconnect and restart the workflow.");
        }
    }

    private async Task SendCommandAsync(VehicleId vehicleId, ushort command, IReadOnlyList<float> parameters, CancellationToken cancellationToken)
    {
        var session = vehicleRegistry.GetRequired(vehicleId) ?? throw new InvalidOperationException("The target vehicle session is unavailable.");
        var packet = encoder.EncodeCommandLong(vehicleId.SystemId, vehicleId.ComponentId, command, parameters);
        await connection.SendRawAsync(packet, session.EndPoint, cancellationToken).ConfigureAwait(false);
    }

    private void Finish(CompassCalibrationWorkflowState state, string message) => FinishWithSummary(state, message, Current.QualitySummary);

    private void FinishWithSummary(CompassCalibrationWorkflowState state, string message, string? summary)
    {
        if (!IsActive(Current.State))
        {
            return;
        }

        var success = state == CompassCalibrationWorkflowState.Success;
        Transition(Current with
        {
            State = state,
            Progress = progress.Values.ToArray(),
            Reports = reports.Values.ToArray(),
            OverallProgress = success ? 1 : Current.OverallProgress,
            RequiresAcceptance = false,
            Instruction = message,
            QualitySummary = summary,
            FailureReason = success ? null : message
        });
        startSignal?.TrySetResult();
        logger.LogInformation("Compass calibration for {VehicleId} finished in state {CompassState}.", Current.VehicleId, state);
        EndRun();
    }

    private void Transition(CompassCalibrationSnapshot snapshot)
    {
        Current = snapshot;
        StateChanged?.Invoke(this, new CompassCalibrationStateChangedEventArgs(snapshot));
    }

    private void EndRun()
    {
        messageSubscription?.Dispose();
        messageSubscription = null;
        runCancellationRegistration.Unregister();
        runCancellation?.Cancel();
        runCancellation?.Dispose();
        runCancellation = null;
        operationLease?.Dispose();
        operationLease = null;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

    private static string BuildQualitySummary(IReadOnlyList<CompassCalibrationReport> completed)
    {
        var lines = completed
            .OrderBy(report => report.CompassId)
            .Select(report => $"Compass {report.CompassId + 1}: fitness {report.Fitness:F1} mGauss ({QualityLabel(report.Fitness)})" +
                (report.OldOrientation != report.NewOrientation ? $", orientation corrected to {report.NewOrientation}" : string.Empty));
        return string.Join(Environment.NewLine, lines);
    }

    private static string QualityLabel(double fitness) => fitness switch
    {
        <= 8 => "good",
        <= 16 => "acceptable",
        _ => "poor, consider recalibrating"
    };

    private static CompassCalibrationStatus MapStatus(byte status) => (MagCalStatus)status switch
    {
        MagCalStatus.MagCalNotStarted => CompassCalibrationStatus.NotStarted,
        MagCalStatus.MagCalWaitingToStart => CompassCalibrationStatus.WaitingToStart,
        MagCalStatus.MagCalRunningStepOne or MagCalStatus.MagCalRunningStepTwo => CompassCalibrationStatus.Running,
        MagCalStatus.MagCalSuccess => CompassCalibrationStatus.Success,
        MagCalStatus.MagCalFailedOrientation => CompassCalibrationStatus.BadOrientation,
        MagCalStatus.MagCalFailedRadius => CompassCalibrationStatus.BadRadius,
        _ => CompassCalibrationStatus.Failed
    };

    private static bool IsActive(CompassCalibrationWorkflowState state) => state is
        CompassCalibrationWorkflowState.Preparing or CompassCalibrationWorkflowState.Running or
        CompassCalibrationWorkflowState.PendingAcceptance;
}
