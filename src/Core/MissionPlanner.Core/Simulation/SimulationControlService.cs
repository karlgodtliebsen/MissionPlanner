using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.Simulation.Abstractions;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Simulation;
using MissionPlanner.Simulation.Abstractions;

namespace MissionPlanner.Core.Simulation;

/// <summary>Applies and confirms instance-specific simulation parameters with bounded fault reset.</summary>
public sealed class SimulationControlService : ISimulationControlService
{
    private readonly ISimulationControlCatalog catalog;
    private readonly ISimulationSessionManager sessionManager;
    private readonly IVehicleConnectionSession connectionSession;
    private readonly IVehicleParameterRegistry parameterRegistry;
    private readonly IVehicleRegistry vehicleRegistry;
    private readonly IDateTimeProvider clock;
    private readonly SimulationControlOptions options;
    private readonly ILogger<SimulationControlService> logger;
    private readonly ISimulationVehicleChannelRegistry? simulationChannels;
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly Lock eventLock = new();
    private readonly Queue<SimulationScenarioEvent> events = new();
    private readonly Dictionary<string, ActiveReset> activeResets = new(StringComparer.Ordinal);
    private bool disposed;

    /// <summary>Initializes the documented simulation-control service.</summary>
    /// <param name="catalog">Documented control catalog.</param>
    /// <param name="sessionManager">Current simulation session manager.</param>
    /// <param name="connectionSession">Existing MAVLink connection session.</param>
    /// <param name="parameterRegistry">Live vehicle parameter registry.</param>
    /// <param name="vehicleRegistry">Live vehicle registry.</param>
    /// <param name="clock">Application clock.</param>
    /// <param name="options">Bounded operation options.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="simulationChannels">Optional exact simulator vehicle-channel routes.</param>
    public SimulationControlService(
        ISimulationControlCatalog catalog,
        ISimulationSessionManager sessionManager,
        IVehicleConnectionSession connectionSession,
        IVehicleParameterRegistry parameterRegistry,
        IVehicleRegistry vehicleRegistry,
        IDateTimeProvider clock,
        IOptions<SimulationControlOptions> options,
        ILogger<SimulationControlService> logger,
        ISimulationVehicleChannelRegistry? simulationChannels = null)
    {
        this.catalog = catalog;
        this.sessionManager = sessionManager;
        this.connectionSession = connectionSession;
        this.parameterRegistry = parameterRegistry;
        this.vehicleRegistry = vehicleRegistry;
        this.clock = clock;
        this.options = options.Value;
        this.logger = logger;
        this.simulationChannels = simulationChannels;
    }

    /// <inheritdoc />
    public IReadOnlyList<SimulationScenarioEvent> Events
    {
        get
        {
            lock (eventLock)
            {
                return events.ToArray();
            }
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SimulationControlCapability>> DiscoverAsync(
        CancellationToken cancellationToken = default)
    {
        return await DiscoverCoreAsync(GetTarget(), cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SimulationControlCapability>> DiscoverAsync(
        Guid sessionId,
        VehicleId vehicleId,
        CancellationToken cancellationToken = default)
    {
        return await DiscoverCoreAsync(GetTarget(sessionId, vehicleId), cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<SimulationControlCapability>> DiscoverCoreAsync(
        SimulationTarget target,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var parameterService = GetParameterService(target.VehicleId);
        var requested = false;
        foreach (var name in catalog.Controls.SelectMany(item => item.ParameterBindings).Select(item => item.Name).Distinct())
        {
            if (parameterRegistry.GetParameter(target.VehicleId, name) is null)
            {
                requested |= await parameterService.RequestParameterAsync(
                    target.VehicleId,
                    name,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        if (requested)
        {
            await Task.Delay(
                TimeSpan.FromMilliseconds(Math.Clamp(options.DiscoveryWaitMilliseconds, 0, 5000)),
                cancellationToken).ConfigureAwait(false);
        }

        var firmware = vehicleRegistry.GetRequired(target.VehicleId)?.State.Identity.Firmware;
        return catalog.Controls.Select(descriptor => ResolveCapability(target, descriptor, firmware)).ToArray();
    }

    /// <inheritdoc />
    public async Task ApplyAsync(
        string controlKey,
        double requestedValue,
        TimeSpan? duration,
        bool confirmed,
        CancellationToken cancellationToken = default)
    {
        await ApplyCoreAsync(
            GetTarget(),
            controlKey,
            requestedValue,
            duration,
            confirmed,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ApplyAsync(
        Guid sessionId,
        VehicleId vehicleId,
        string controlKey,
        double requestedValue,
        TimeSpan? duration,
        bool confirmed,
        CancellationToken cancellationToken = default)
    {
        await ApplyCoreAsync(
            GetTarget(sessionId, vehicleId),
            controlKey,
            requestedValue,
            duration,
            confirmed,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task ApplyCoreAsync(
        SimulationTarget target,
        string controlKey,
        double requestedValue,
        TimeSpan? duration,
        bool confirmed,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var descriptor = GetDescriptor(controlKey);
            ValidateRequest(descriptor, requestedValue, duration, confirmed);
            var capability = ResolveCapability(
                target,
                descriptor,
                vehicleRegistry.GetRequired(target.VehicleId)?.State.Identity.Firmware);
            if (!capability.IsAvailable || capability.ParameterName is null || capability.ParameterType is null ||
                capability.CurrentValue is null)
            {
                throw new InvalidOperationException(capability.Reason);
            }

            var resetKey = ResetKey(target, controlKey);
            if (activeResets.TryGetValue(resetKey, out var previous))
            {
                await ResetCoreAsync(previous, SimulationScenarioEventResult.Reset, cancellationToken).ConfigureAwait(false);
            }

            var binding = descriptor.ParameterBindings.First(item =>
                item.Name.Equals(capability.ParameterName, StringComparison.OrdinalIgnoreCase));
            var value = binding.ActiveValue ?? requestedValue;
            var reset = binding.ResetValue ?? capability.CurrentValue.Value;
            try
            {
                await SetConfirmedAsync(
                    target,
                    controlKey,
                    capability.ParameterName,
                    (MavParamType)capability.ParameterType,
                    value,
                    cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                if (descriptor.MaximumDuration is not null)
                {
                    await BestEffortSetAsync(
                        target,
                        capability.ParameterName,
                        (MavParamType)capability.ParameterType,
                        reset).ConfigureAwait(false);
                }

                throw;
            }

            AddEvent(target, controlKey, capability.ParameterName, value, SimulationScenarioEventResult.Applied, "Value confirmed by parameter readback.");
            logger.LogInformation(
                "Applied simulation control {ControlKey} to {VehicleId} using {ParameterName}={Value}.",
                controlKey,
                target.VehicleId,
                capability.ParameterName,
                value);
            if (descriptor.MaximumDuration is { } maximumDuration)
            {
                var resetCancellation = new CancellationTokenSource();
                var active = new ActiveReset(
                    target,
                    controlKey,
                    capability.ParameterName,
                    (MavParamType)capability.ParameterType,
                    reset,
                    resetCancellation);
                activeResets[resetKey] = active;
                _ = AutoResetAsync(active, duration!.Value, maximumDuration);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task ResetAsync(string controlKey, CancellationToken cancellationToken = default)
    {
        await ResetTargetAsync(GetTarget(), controlKey, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task ResetAsync(
        Guid sessionId,
        VehicleId vehicleId,
        string controlKey,
        CancellationToken cancellationToken = default)
    {
        await ResetTargetAsync(GetTarget(sessionId, vehicleId), controlKey, cancellationToken).ConfigureAwait(false);
    }

    private async Task ResetTargetAsync(
        SimulationTarget target,
        string controlKey,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (activeResets.TryGetValue(ResetKey(target, controlKey), out var active))
            {
                await ResetCoreAsync(active, SimulationScenarioEventResult.Reset, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public async Task ResetAllAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (var active in activeResets.Values.ToArray())
            {
                await ResetCoreAsync(active, SimulationScenarioEventResult.Reset, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        try
        {
            await ResetAllAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "One or more active simulation controls could not be reset during disposal.");
        }

        disposed = true;
        foreach (var active in activeResets.Values)
        {
            active.Cancellation.Cancel();
            active.Cancellation.Dispose();
        }

        activeResets.Clear();
        gate.Dispose();
    }

    private async Task AutoResetAsync(ActiveReset active, TimeSpan duration, TimeSpan maximumDuration)
    {
        try
        {
            await Task.Delay(duration <= maximumDuration ? duration : maximumDuration, active.Cancellation.Token)
                .ConfigureAwait(false);
            await gate.WaitAsync(active.Cancellation.Token).ConfigureAwait(false);
            try
            {
                if (activeResets.TryGetValue(ResetKey(active.Target, active.ControlKey), out var current) && ReferenceEquals(current, active))
                {
                    await ResetCoreAsync(active, SimulationScenarioEventResult.AutoReset, CancellationToken.None)
                        .ConfigureAwait(false);
                }
            }
            finally
            {
                gate.Release();
            }
        }
        catch (OperationCanceledException) when (active.Cancellation.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Automatic reset failed for simulation control {ControlKey}.", active.ControlKey);
            AddEvent(
                active.Target,
                active.ControlKey,
                active.ParameterName,
                active.ResetValue,
                SimulationScenarioEventResult.Failed,
                $"Automatic reset failed: {exception.Message}");
        }
    }

    private async Task ResetCoreAsync(
        ActiveReset active,
        SimulationScenarioEventResult result,
        CancellationToken cancellationToken)
    {
        active.Cancellation.Cancel();
        if (!IsSameTarget(active.Target))
        {
            AddEvent(
                active.Target,
                active.ControlKey,
                active.ParameterName,
                active.ResetValue,
                SimulationScenarioEventResult.Failed,
                "Reset skipped because the exact simulation session is no longer connected.");
            activeResets.Remove(ResetKey(active.Target, active.ControlKey));
            active.Cancellation.Dispose();
            return;
        }

        await SetConfirmedAsync(
            active.Target,
            active.ControlKey,
            active.ParameterName,
            active.ParameterType,
            active.ResetValue,
            cancellationToken).ConfigureAwait(false);
        AddEvent(
            active.Target,
            active.ControlKey,
            active.ParameterName,
            active.ResetValue,
            result,
            result == SimulationScenarioEventResult.AutoReset
                ? "Hazard duration elapsed; safe value confirmed."
                : "Safe value confirmed by parameter readback.");
        activeResets.Remove(ResetKey(active.Target, active.ControlKey));
        active.Cancellation.Dispose();
    }

    private async Task SetConfirmedAsync(SimulationTarget target, string controlKey, string parameterName, MavParamType parameterType, double value, CancellationToken cancellationToken)
    {
        EnsureSameTarget(target);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        void OnChanged(VehicleParameterChangedEventArgs args)
        {
            if (args.VehicleId == target.VehicleId &&
                args.Parameter?.Name.Equals(parameterName, StringComparison.OrdinalIgnoreCase) == true &&
                NearlyEqual(args.Parameter.Value, value))
            {
                completion.TrySetResult();
            }
        }

        parameterRegistry.Changed += OnChanged;
        try
        {
            var sent = await GetParameterService(target.VehicleId).SetParameterAsync(
                target.VehicleId,
                parameterName,
                (float)value,
                parameterType,
                cancellationToken).ConfigureAwait(false);
            if (!sent)
            {
                throw new InvalidOperationException($"Vehicle rejected simulation parameter write {parameterName}.");
            }

            if (parameterRegistry.GetParameter(target.VehicleId, parameterName) is { } current &&
                NearlyEqual(current.Value, value))
            {
                return;
            }

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(options.ReadbackTimeoutSeconds, 1, 30)));
            try
            {
                await completion.Task.WaitAsync(timeout.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                AddEvent(
                    target,
                    controlKey,
                    parameterName,
                    value,
                    SimulationScenarioEventResult.Failed,
                    "Timed out waiting for parameter readback.");
                throw new TimeoutException($"Timed out confirming {parameterName}={value:0.###}.");
            }
        }
        finally
        {
            parameterRegistry.Changed -= OnChanged;
        }
    }

    private async Task BestEffortSetAsync(
        SimulationTarget target,
        string parameterName,
        MavParamType parameterType,
        double value)
    {
        try
        {
            if (IsSameTarget(target))
            {
                await GetParameterService(target.VehicleId).SetParameterAsync(
                    target.VehicleId,
                    parameterName,
                    (float)value,
                    parameterType,
                    CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Best-effort reset failed for {ParameterName}.", parameterName);
        }
    }

    private SimulationControlCapability ResolveCapability(SimulationTarget target, SimulationControlDescriptor descriptor, VehicleFirmwareIdentity? firmware)
    {
        if (!descriptor.SupportedFamilies.Contains(target.Profile.FirmwareFamily))
        {
            return Unavailable(descriptor, firmware, $"{target.Profile.FirmwareFamily} is not supported by this control.");
        }

        foreach (var binding in descriptor.ParameterBindings)
        {
            if (parameterRegistry.GetParameter(target.VehicleId, binding.Name) is { } parameter)
            {
                return new SimulationControlCapability(
                    descriptor,
                    true,
                    parameter.Name,
                    parameter.Type,
                    parameter.Value,
                    $"Available as {parameter.Name} on the connected firmware.",
                    firmware?.FlightVersion);
            }
        }

        var reason = descriptor.ParameterBindings.Count == 0
            ? descriptor.Description
            : $"None of the documented parameter variants ({string.Join(", ", descriptor.ParameterBindings.Select(item => item.Name))}) " +
              "is present on the connected firmware.";
        return Unavailable(descriptor, firmware, reason);
    }

    private static SimulationControlCapability Unavailable(
        SimulationControlDescriptor descriptor,
        VehicleFirmwareIdentity? firmware,
        string reason)
    {
        return new SimulationControlCapability(descriptor, false, null, null, null, reason, firmware?.FlightVersion);
    }

    private SimulationTarget GetTarget()
    {
        var snapshot = sessionManager.Current;
        if (snapshot.State != SimulationSessionState.Running || snapshot.Profile is null || snapshot.VehicleId is null)
        {
            throw new InvalidOperationException("A verified running simulation vehicle is required.");
        }

        var vehicle = vehicleRegistry.GetRequired(snapshot.VehicleId.Value);
        return vehicle is null || vehicle.State.Connection.State != VehicleConnectionState.Online
            ? throw new InvalidOperationException("The simulator vehicle is not online.")
            : new SimulationTarget(
                snapshot.SessionId,
                snapshot.VehicleId.Value,
                snapshot.Profile,
                snapshot.StartedAt ?? clock.UtcNow);
    }

    private SimulationTarget GetTarget(Guid sessionId, VehicleId vehicleId)
    {
        var snapshot = sessionManager.Current;
        if (snapshot.State == SimulationSessionState.Running &&
            snapshot.SessionId == sessionId &&
            snapshot.VehicleId == vehicleId &&
            snapshot.Profile is not null)
        {
            return new SimulationTarget(
                sessionId,
                vehicleId,
                snapshot.Profile,
                snapshot.StartedAt ?? clock.UtcNow);
        }

        var channel = simulationChannels?.Find(vehicleId);
        if (channel?.SessionId != sessionId)
        {
            throw new InvalidOperationException("The simulation session and VehicleId do not identify the same running target.");
        }

        var vehicle = vehicleRegistry.GetRequired(vehicleId);
        return vehicle is null || vehicle.State.Connection.State != VehicleConnectionState.Online
            ? throw new InvalidOperationException("The simulator vehicle is not online.")
            : new SimulationTarget(sessionId, vehicleId, channel.Profile, channel.StartedAt);
    }

    private void EnsureSameTarget(SimulationTarget target)
    {
        if (!IsSameTarget(target))
        {
            throw new InvalidOperationException("The simulation session or target vehicle changed before the control write.");
        }
    }

    private bool IsSameTarget(SimulationTarget target)
    {
        var snapshot = sessionManager.Current;
        var singleSessionMatches = snapshot.State == SimulationSessionState.Running &&
                                   snapshot.SessionId == target.SessionId && snapshot.VehicleId == target.VehicleId;
        var fleetChannelMatches = simulationChannels?.Find(target.VehicleId)?.SessionId == target.SessionId;
        return singleSessionMatches || fleetChannelMatches;
    }

    private SimulationControlDescriptor GetDescriptor(string controlKey)
    {
        return catalog.Controls.FirstOrDefault(item => item.Key.Equals(controlKey, StringComparison.Ordinal)) ??
               throw new KeyNotFoundException($"Unknown simulation control '{controlKey}'.");
    }

    private static void ValidateRequest(
        SimulationControlDescriptor descriptor,
        double value,
        TimeSpan? duration,
        bool confirmed)
    {
        if (!double.IsFinite(value) || value < descriptor.Minimum || value > descriptor.Maximum)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                $"{descriptor.DisplayName} must be between {descriptor.Minimum} and {descriptor.Maximum} {descriptor.Unit}.".TrimEnd());
        }

        if (descriptor.RequiresConfirmation && !confirmed)
        {
            throw new InvalidOperationException($"{descriptor.DisplayName} requires explicit hazardous-action confirmation.");
        }

        if (descriptor.MaximumDuration is { } maximum &&
            (duration is null || duration <= TimeSpan.Zero || duration > maximum))
        {
            throw new ArgumentOutOfRangeException(
                nameof(duration),
                $"{descriptor.DisplayName} duration must be greater than zero and at most {maximum.TotalSeconds:0} seconds.");
        }
    }

    private void AddEvent(
        SimulationTarget target,
        string controlKey,
        string parameterName,
        double value,
        SimulationScenarioEventResult result,
        string message)
    {
        var now = clock.UtcNow;
        var item = new SimulationScenarioEvent(
            now,
            now >= target.StartedAt ? now - target.StartedAt : TimeSpan.Zero,
            target.SessionId,
            target.VehicleId,
            controlKey,
            parameterName,
            value,
            result,
            message);
        lock (eventLock)
        {
            events.Enqueue(item);
            var capacity = Math.Clamp(options.EventCapacity, 1, 10000);
            while (events.Count > capacity)
            {
                events.Dequeue();
            }
        }
    }

    private static bool NearlyEqual(double first, double second)
    {
        return Math.Abs(first - second) <= Math.Max(0.0001, Math.Abs(second) * 0.00001);
    }

    private static string ResetKey(SimulationTarget target, string controlKey)
    {
        return $"{target.SessionId:N}:{target.VehicleId.SystemId}:{target.VehicleId.ComponentId}:{controlKey}";
    }

    private IVehicleParameterService GetParameterService(VehicleId vehicleId)
    {
        return simulationChannels?.Find(vehicleId)?.ConnectionSession.ParameterService ?? connectionSession.ParameterService;
    }

    private sealed record SimulationTarget(
        Guid SessionId,
        VehicleId VehicleId,
        SimulatorProfile Profile,
        DateTimeOffset StartedAt);

    private sealed record ActiveReset(
        SimulationTarget Target,
        string ControlKey,
        string ParameterName,
        MavParamType ParameterType,
        double ResetValue,
        CancellationTokenSource Cancellation);
}
