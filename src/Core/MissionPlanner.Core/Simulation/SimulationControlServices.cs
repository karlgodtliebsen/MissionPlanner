using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Simulation;

/// <summary>Provides controls backed only by documented ArduPilot SITL parameters.</summary>
public sealed class SimulationControlCatalog : ISimulationControlCatalog
{
    private static readonly IReadOnlySet<FirmwareFamily> allSitlFamilies = new HashSet<FirmwareFamily>
    {
        FirmwareFamily.ArduCopter,
        FirmwareFamily.ArduPlane,
        FirmwareFamily.Rover,
        FirmwareFamily.ArduSub
    };
    private static readonly Uri simulationParametersDocumentation =
        new("https://ardupilot.org/dev/docs/SITL_simulation_parameters.html");
    private static readonly Uri sourceParameterDocumentation =
        new("https://github.com/ArduPilot/ardupilot/blob/master/libraries/SITL/SITL.cpp");

    /// <inheritdoc />
    public IReadOnlyList<SimulationControlDescriptor> Controls { get; } =
    [
        Value("wind-speed", "Wind speed", "Horizontal simulated wind speed.", "m/s", 0, 100, "SIM_WIND_SPD"),
        Value("wind-direction", "Wind direction", "True direction the simulated wind comes from.", "deg", 0, 360, "SIM_WIND_DIR"),
        Value("wind-turbulence", "Wind turbulence", "Random simulated wind variation.", "m/s", 0, 100, "SIM_WIND_TURB"),
        Fault(
            "gps-failure",
            "GPS signal loss",
            "Disables the primary simulated GPS for a bounded interval.",
            [new SimulationParameterBinding("SIM_GPS1_ENABLE", 0, 1), new SimulationParameterBinding("SIM_GPS_DISABLE", 1, 0)]),
        Fault(
            "compass-failure",
            "Compass 1 failure",
            "Injects the documented primary simulated compass failure.",
            [new SimulationParameterBinding("SIM_MAG1_FAIL", 1, 0)]),
        Fault(
            "rc-failure",
            "RC signal loss",
            "Simulates complete loss of RC input; RC failsafe behavior remains firmware-configured.",
            [new SimulationParameterBinding("SIM_RC_FAIL", 1, 0)]),
        new SimulationControlDescriptor(
            "battery-voltage",
            "Battery voltage",
            "Overrides simulated resting battery voltage temporarily and may trigger configured battery failsafes.",
            SimulationControlCategory.Fault,
            "V",
            0,
            100,
            true,
            TimeSpan.FromMinutes(5),
            [new SimulationParameterBinding("SIM_BATT_VOLTAGE")],
            allSitlFamilies,
            sourceParameterDocumentation),
        new SimulationControlDescriptor(
            "rangefinder-failure",
            "Rangefinder failure",
            "No bounded general-purpose rangefinder failure parameter is documented; availability remains explicit.",
            SimulationControlCategory.Sensor,
            string.Empty,
            0,
            1,
            true,
            TimeSpan.FromSeconds(30),
            [],
            allSitlFamilies,
            new Uri("https://ardupilot.org/dev/docs/adding_simulated_devices.html"))
    ];

    /// <inheritdoc />
    public IReadOnlyList<SimulationLocationPreset> Locations { get; } =
    [
        new("canberra-cmac", "Canberra — CMAC", new SimulationLocation(-35.363261, 149.165230, 584, 353)),
        new("copenhagen", "Copenhagen", new SimulationLocation(55.6761, 12.5683, 5, 0)),
        new("zero", "Equator / prime meridian", new SimulationLocation(0, 0, 0, 0))
    ];

    private static SimulationControlDescriptor Value(
        string key,
        string name,
        string description,
        string unit,
        double minimum,
        double maximum,
        string parameterName) =>
        new(
            key,
            name,
            description,
            SimulationControlCategory.Environment,
            unit,
            minimum,
            maximum,
            false,
            null,
            [new SimulationParameterBinding(parameterName)],
            allSitlFamilies,
            simulationParametersDocumentation);

    private static SimulationControlDescriptor Fault(
        string key,
        string name,
        string description,
        IReadOnlyList<SimulationParameterBinding> parameters) =>
        new(
            key,
            name,
            description,
            SimulationControlCategory.Fault,
            string.Empty,
            0,
            1,
            true,
            TimeSpan.FromSeconds(60),
            parameters,
            allSitlFamilies,
            simulationParametersDocumentation);
}

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
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly object eventLock = new();
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
    public SimulationControlService(
        ISimulationControlCatalog catalog,
        ISimulationSessionManager sessionManager,
        IVehicleConnectionSession connectionSession,
        IVehicleParameterRegistry parameterRegistry,
        IVehicleRegistry vehicleRegistry,
        IDateTimeProvider clock,
        IOptions<SimulationControlOptions> options,
        ILogger<SimulationControlService> logger)
    {
        this.catalog = catalog;
        this.sessionManager = sessionManager;
        this.connectionSession = connectionSession;
        this.parameterRegistry = parameterRegistry;
        this.vehicleRegistry = vehicleRegistry;
        this.clock = clock;
        this.options = options.Value;
        this.logger = logger;
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
        ObjectDisposedException.ThrowIf(disposed, this);
        var target = GetTarget();
        var parameterService = connectionSession.ParameterService;
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
        ObjectDisposedException.ThrowIf(disposed, this);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var target = GetTarget();
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

            if (activeResets.TryGetValue(controlKey, out var previous))
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
                activeResets[controlKey] = active;
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
        ObjectDisposedException.ThrowIf(disposed, this);
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (activeResets.TryGetValue(controlKey, out var active))
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
                if (activeResets.TryGetValue(active.ControlKey, out var current) && ReferenceEquals(current, active))
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
        var snapshot = sessionManager.Current;
        if (snapshot.SessionId != active.Target.SessionId || snapshot.VehicleId != active.Target.VehicleId ||
            snapshot.State != SimulationSessionState.Running)
        {
            AddEvent(
                active.Target,
                active.ControlKey,
                active.ParameterName,
                active.ResetValue,
                SimulationScenarioEventResult.Failed,
                "Reset skipped because the exact simulation session is no longer connected.");
            activeResets.Remove(active.ControlKey);
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
        activeResets.Remove(active.ControlKey);
        active.Cancellation.Dispose();
    }

    private async Task SetConfirmedAsync(
        SimulationTarget target,
        string controlKey,
        string parameterName,
        MavParamType parameterType,
        double value,
        CancellationToken cancellationToken)
    {
        EnsureSameTarget(target);
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void OnChanged(object? sender, VehicleParameterChangedEventArgs args)
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
            var sent = await connectionSession.ParameterService.SetParameterAsync(
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
            if (sessionManager.Current.SessionId == target.SessionId &&
                sessionManager.Current.VehicleId == target.VehicleId)
            {
                await connectionSession.ParameterService.SetParameterAsync(
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

    private SimulationControlCapability ResolveCapability(
        SimulationTarget target,
        SimulationControlDescriptor descriptor,
        VehicleFirmwareIdentity? firmware)
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
        string reason) =>
        new(descriptor, false, null, null, null, reason, firmware?.FlightVersion);

    private SimulationTarget GetTarget()
    {
        var snapshot = sessionManager.Current;
        if (snapshot.State != SimulationSessionState.Running || snapshot.Profile is null || snapshot.VehicleId is null)
        {
            throw new InvalidOperationException("A verified running simulation vehicle is required.");
        }

        var vehicle = vehicleRegistry.GetRequired(snapshot.VehicleId.Value);
        if (vehicle is null || vehicle.State.Connection.State != VehicleConnectionState.Online)
        {
            throw new InvalidOperationException("The simulator vehicle is not online.");
        }

        return new SimulationTarget(
            snapshot.SessionId,
            snapshot.VehicleId.Value,
            snapshot.Profile,
            snapshot.StartedAt ?? clock.UtcNow);
    }

    private void EnsureSameTarget(SimulationTarget target)
    {
        var snapshot = sessionManager.Current;
        if (snapshot.State != SimulationSessionState.Running ||
            snapshot.SessionId != target.SessionId ||
            snapshot.VehicleId != target.VehicleId)
        {
            throw new InvalidOperationException("The simulation session or target vehicle changed before the control write.");
        }
    }

    private SimulationControlDescriptor GetDescriptor(string controlKey) =>
        catalog.Controls.FirstOrDefault(item => item.Key.Equals(controlKey, StringComparison.Ordinal)) ??
        throw new KeyNotFoundException($"Unknown simulation control '{controlKey}'.");

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

    private static bool NearlyEqual(double first, double second) =>
        Math.Abs(first - second) <= Math.Max(0.0001, Math.Abs(second) * 0.00001);

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

/// <summary>Loads and persists schema-versioned simulation scenario presets.</summary>
public sealed class SimulationScenarioPresetService(
    ISimulationScenarioPresetStore store,
    ILogger<SimulationScenarioPresetService> logger) : ISimulationScenarioPresetService
{
    private const int SchemaVersion = 1;
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private IReadOnlyList<SimulationScenarioPreset> presets = [];
    private bool initialized;

    /// <inheritdoc />
    public IReadOnlyList<SimulationScenarioPreset> Presets => presets;

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<SimulationScenarioPreset>> InitializeAsync(
        CancellationToken cancellationToken = default)
    {
        if (initialized)
        {
            return presets;
        }

        var document = await store.ReadAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(document))
        {
            try
            {
                var persisted = JsonSerializer.Deserialize<PresetDocument>(document, jsonOptions);
                if (persisted is { Version: SchemaVersion } && persisted.Presets.All(IsValid))
                {
                    presets = persisted.Presets;
                }
                else
                {
                    logger.LogWarning("Simulation scenario presets had an unsupported schema or invalid content.");
                }
            }
            catch (JsonException exception)
            {
                logger.LogWarning(exception, "Simulation scenario preset persistence was corrupt; using an empty set.");
            }
        }

        initialized = true;
        return presets;
    }

    /// <inheritdoc />
    public async ValueTask SaveAsync(
        SimulationScenarioPreset preset,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preset);
        if (!IsValid(preset))
        {
            throw new ArgumentException("The simulation scenario preset is invalid.", nameof(preset));
        }

        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        presets = presets.Where(item => item.Id != preset.Id).Append(preset)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToArray();
        await PersistAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DeleteAsync(Guid presetId, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken).ConfigureAwait(false);
        presets = presets.Where(item => item.Id != presetId).ToArray();
        await PersistAsync(cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (!initialized)
        {
            await InitializeAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private ValueTask PersistAsync(CancellationToken cancellationToken) =>
        store.WriteAsync(
            JsonSerializer.Serialize(new PresetDocument(SchemaVersion, presets), jsonOptions),
            cancellationToken);

    private static bool IsValid(SimulationScenarioPreset preset) =>
        preset.Id != Guid.Empty &&
        !string.IsNullOrWhiteSpace(preset.Name) &&
        preset.Controls is not null &&
        preset.Controls.All(control =>
            !string.IsNullOrWhiteSpace(control.ControlKey) &&
            double.IsFinite(control.Value) &&
            (control.Duration is null || control.Duration > TimeSpan.Zero));

    private sealed record PresetDocument(int Version, IReadOnlyList<SimulationScenarioPreset> Presets);
}
