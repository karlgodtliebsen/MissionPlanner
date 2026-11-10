using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.OptionalHardware.Motor;

/// <summary>One physical output and its user-observed position on the frame.</summary>
public sealed record MotorOrderAssignment(string ParameterName, int OutputChannel, int OriginalFunction,
    MotorLayoutMotor TestMotor, MotorLayoutMotor? ObservedMotor = null)
{
    public int Function => MotorOrderService.MotorFunction(ObservedMotor?.MotorNumber ?? TestMotor.MotorNumber);
}

/// <summary>Identifies physical outputs without changing assignments during observation.</summary>
public sealed class MotorOrderService(
    IActiveVehicleContext activeVehicle,
    IVehicleParameterRegistry parameters,
    IVehicleParameterService protocol,
    MotorLayoutResolver resolver,
    IActuatorTestService actuators,
    IVehicleOperationGate gate) : IDisposable
{
    private readonly SemaphoreSlim operation = new(1, 1);
    private readonly List<MotorOrderAssignment> assignments = [];
    private Dictionary<string, float> original = [];
    private VehicleId? vehicleId;
    private CancellationTokenSource? lifetime;
    private bool tested;
    private bool finished;
    private bool contextInvalidated;
    private bool pulseInFlight;
    private CancellationToken connectionToken;

    public MotorLayout? Layout { get; private set; }
    public IReadOnlyList<MotorOrderAssignment> Assignments => assignments.ToArray();
    public MotorOrderAssignment? Current => assignments.FirstOrDefault(item => item.ObservedMotor is null);
    public bool NeedsRepair { get; private set; }
    public string? LastWriteMessage { get; private set; }
    /// <summary>Raised after acknowledgement, permitting selection during a pulse.</summary>
    public event Action? ObservationReady;
    public bool IsComplete => assignments.Count > 0 && Current is null;
    public bool CanObserve => tested && !finished && !NeedsRepair;
    public bool HasSession => !finished && vehicleId is { } id && activeVehicle.VehicleId == id &&
        activeVehicle.IsOnline && lifetime?.IsCancellationRequested == false;

    public void Start(bool propsRemoved)
    {
        if (!propsRemoved)
            throw new InvalidOperationException("Confirm that all propellers are removed before starting.");
        if (operation.CurrentCount == 0)
            throw new InvalidOperationException("Wait for the current operation to finish.");
        if (!activeVehicle.IsOnline || activeVehicle.VehicleId is not { } id || activeVehicle.State?.IsArmed != false)
            throw new InvalidOperationException("Connect and disarm the vehicle before setting motor order.");
        if (actuators.Current?.State == MotorTestState.Running || gate.GetCurrentOperation(id) is not null)
            throw new InvalidOperationException("Stop the current vehicle operation before setting motor order.");

        var values = parameters.GetAllParameters(id);
        if (parameters.GetParameterCount(id) is not > 0 || values.Count < parameters.GetParameterCount(id))
            throw new InvalidOperationException("Wait for the complete parameter download before identifying motor outputs.");
        // Plane/other vehicle motor-test commands do not share Copter's sequence semantics.
        if (activeVehicle.State.Autopilot != 3 || values.ContainsKey("Q_FRAME_CLASS"))
            throw new InvalidOperationException("Motor order requires an ArduPilot Copter matrix frame.");
        Layout = resolver.Resolve(values) ?? throw new InvalidOperationException("A supported frame layout is required.");
        if (Layout.Motors.GroupBy(motor => (motor.Roll, motor.Pitch)).Any(group => group.Count() != 1))
            throw new InvalidOperationException("This frame has stacked motors. Their positions cannot be identified with this top-view assistant.");

        var candidates = MotorParameters(values);
        if (candidates.Count != Layout.Motors.Count)
            throw new InvalidOperationException($"The frame needs {Layout.Motors.Count} motor outputs, but {candidates.Count} are assigned. Assign the connected motor outputs before starting.");
        var byFunction = Layout.Motors.ToDictionary(motor => MotorFunction(motor.MotorNumber));
        NeedsRepair = candidates.Values.Distinct().Count() != candidates.Count || candidates.Values.Any(value => !byFunction.ContainsKey((int)value));
        assignments.Clear();
        var orderedMotors = Layout.Motors.OrderBy(motor => motor.MotorNumber).ToArray();
        foreach (var pair in candidates.OrderBy(pair => OutputChannel(pair.Key)))
        {
            var motor = NeedsRepair ? orderedMotors[assignments.Count] : byFunction[(int)pair.Value];
            assignments.Add(new(pair.Key, OutputChannel(pair.Key), (int)pair.Value, motor));
        }
        original = candidates;
        lifetime?.Cancel();
        lifetime?.Dispose();
        connectionToken = activeVehicle.ConnectionCancellationToken;
        lifetime = CancellationTokenSource.CreateLinkedTokenSource(connectionToken);
        vehicleId = id;
        finished = tested = false;
        contextInvalidated = false;
        LastWriteMessage = null;
        activeVehicle.Changed -= VehicleChanged;
        activeVehicle.Changed += VehicleChanged;
    }

    /// <summary>Pulses the current output once; only a successful bounded pulse permits a click.</summary>
    public async Task PulseAsync(double percent, CancellationToken cancellationToken = default)
    {
        if (!double.IsFinite(percent) || percent is < 1 or > 20)
            throw new InvalidOperationException("Use between 1% and 20% throttle for motor identification.");
        if (!await operation.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Wait for the current pulse or write.");
        try
        {
            var id = EnsureReady(requireDisarmed: false);
            EnsureUnchanged();
            if (NeedsRepair || Current is not { } item)
                throw new InvalidOperationException("Repair the output assignments before testing, or finish this run.");
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime!.Token);
            // An armed heartbeat from the preceding test can arrive during the pause.
            await MotorTestTelemetry.WaitUntilDisarmedAsync(activeVehicle, id, linked.Token).ConfigureAwait(false);
            EnsureReady();
            EnsureUnchanged();
            pulseInFlight = true;
            var result = await actuators.TestMotorAsync(id,
                new(item.TestMotor.TestOrder, MotorThrottleType.Percent, percent, 1), linked.Token).ConfigureAwait(false);
            if (!result.Success) throw new InvalidOperationException(result.Message);
            EnsureReady(requireDisarmed: false);
            tested = true;
            ObservationReady?.Invoke();
            await Task.Delay(TimeSpan.FromSeconds(1.2), linked.Token).ConfigureAwait(false);
            EnsureReady(requireDisarmed: false);
            EnsureUnchanged();
            tested = true;
        }
        finally
        {
            try { await StopOriginalVehicleAsync().ConfigureAwait(false); }
            finally { operation.Release(); }
        }
    }

    public void Observe(int motorNumber)
    {
        EnsureReady();
        EnsureUnchanged();
        if (!CanObserve || operation.CurrentCount == 0 || Current is not { } current)
            throw new InvalidOperationException("Pulse the output before selecting the motor that moved.");
        var motor = Layout!.Motors.SingleOrDefault(item => item.MotorNumber == motorNumber)
            ?? throw new InvalidOperationException("Select a motor in this frame.");
        if (assignments.Any(item => item.ObservedMotor?.MotorNumber == motorNumber))
            throw new InvalidOperationException("That motor has already been selected. Restart if an earlier selection was incorrect.");
        assignments[assignments.IndexOf(current)] = current with { ObservedMotor = motor };
        tested = false;
    }

    public void Restart()
    {
        EnsureReady();
        EnsureUnchanged();
        if (operation.CurrentCount == 0) throw new InvalidOperationException("Wait for the current operation.");
        for (var i = 0; i < assignments.Count; i++) assignments[i] = assignments[i] with { ObservedMotor = null };
        tested = false;
    }

    /// <summary>Writes a complete permutation, or explicitly repairs duplicate assignments before a new run.</summary>
    public async Task<MotorTestResult> SaveAsync(bool repair = false, CancellationToken cancellationToken = default)
    {
        if (!await operation.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            return new(false, "Wait for the current operation.");
        var attempted = new List<MotorOrderAssignment>();
        IDisposable? lease = null;
        try
        {
            var id = EnsureReady();
            EnsureUnchanged();
            if (repair ? !NeedsRepair : NeedsRepair || !IsComplete)
                return new(false, "Identify every motor before saving the order.");
            await StopOriginalVehicleAsync().ConfigureAwait(false);
            if (!gate.TryAcquire(id, "motor output assignments", out lease))
                return new(false, "Another vehicle operation is active.");
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime!.Token);
            var expected = new Dictionary<string, float>(original);
            linked.Token.ThrowIfCancellationRequested();
            foreach (var item in assignments.Where(item => item.Function != item.OriginalFunction))
            {
                linked.Token.ThrowIfCancellationRequested();
                EnsureReady();
                EnsureMapping(expected);
                attempted.Add(item); // Even a failed acknowledgement may have changed the controller.
                await WriteAsync(id, item.ParameterName, item.Function, linked.Token).ConfigureAwait(false);
                expected[item.ParameterName] = item.Function;
            }
            EnsureReady();
            linked.Token.ThrowIfCancellationRequested();
            EnsureMapping(expected);
            finished = true;
            LastWriteMessage = repair
                ? "Unique motor assignments saved and verified. Reboot the flight controller, reconnect, and return to Wizard setup to identify the wiring."
                : "Motor order saved and verified. Reboot the flight controller, then verify each motor with propellers removed before flight.";
            return new(true, LastWriteMessage);
        }
        catch (Exception exception)
        {
            var restored = true;
            foreach (var item in attempted.AsEnumerable().Reverse())
            {
                try
                {
                    EnsureReadyForRecovery();
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    await WriteAsync(vehicleId!.Value, item.ParameterName, item.OriginalFunction, timeout.Token).ConfigureAwait(false);
                }
                catch { restored = false; }
            }
            if (attempted.Count > 0)
            {
                try { EnsureMapping(original); }
                catch { restored = false; }
            }
            finished = attempted.Count > 0;
            var recovery = attempted.Count == 0 ? " No assignments were written." : restored
                ? " Original assignments were restored and verified. Restart the assistant."
                : " Recovery could not be verified. Do not fly. Reconnect and restore these original assignments:\n" +
                  string.Join("\n", assignments.Select(item => $"{item.ParameterName} = {item.OriginalFunction}"));
            LastWriteMessage = exception.Message + recovery;
            return new(false, LastWriteMessage);
        }
        finally
        {
            lease?.Dispose();
            operation.Release();
        }
    }

    public async Task CancelAsync()
    {
        lifetime?.Cancel();
        await operation.WaitAsync().ConfigureAwait(false);
        try
        {
            tested = false;
            finished = true;
            await StopOriginalVehicleAsync().ConfigureAwait(false);
        }
        finally { operation.Release(); }
    }

    private async Task WriteAsync(VehicleId id, string name, int value, CancellationToken token)
    {
        var parameter = parameters.GetParameter(id, name) ?? throw new InvalidOperationException($"{name} is unavailable.");
        if (!await protocol.SetParameterAsync(id, name, value, parameter.Type, token).ConfigureAwait(false))
            throw new InvalidOperationException($"Could not write {name}.");
        // Require fresh readback, including when the cache already contains the requested value.
        var readback = new TaskCompletionSource<float>(TaskCreationOptions.RunContinuationsAsynchronously);
        void Changed(VehicleParameterChangedEventArgs args)
        {
            if (args.VehicleId == id && args.Parameter?.Name == name) readback.TrySetResult(args.Parameter.Value);
        }
        parameters.Changed += Changed;
        try
        {
            if (!await protocol.RequestParameterAsync(id, name, token).ConfigureAwait(false) ||
                await readback.Task.WaitAsync(TimeSpan.FromSeconds(4), token).ConfigureAwait(false) != value)
                throw new InvalidOperationException($"The controller did not confirm {name} = {value}.");
        }
        finally { parameters.Changed -= Changed; }
    }

    private VehicleId EnsureReady(bool requireDisarmed = true)
    {
        if (!HasSession) throw new InvalidOperationException("The run has ended or its vehicle connection changed. Reconnect, disarm, and start again.");
        if (requireDisarmed && activeVehicle.State?.IsArmed != false)
            throw new InvalidOperationException("Wait for the controller to report disarmed before continuing.");
        return vehicleId!.Value;
    }

    private void EnsureReadyForRecovery()
    {
        if (contextInvalidated || connectionToken.IsCancellationRequested || vehicleId is not { } id ||
            activeVehicle.VehicleId != id || !activeVehicle.IsOnline || activeVehicle.State?.IsArmed != false)
            throw new InvalidOperationException("The original disarmed vehicle is unavailable for recovery.");
    }

    private void EnsureUnchanged() => EnsureMapping(original);

    private void EnsureMapping(IReadOnlyDictionary<string, float> expected)
    {
        var values = parameters.GetAllParameters(vehicleId!.Value);
        var currentLayout = resolver.Resolve(values);
        var current = MotorParameters(values);
        if (currentLayout?.FrameClass != Layout?.FrameClass || currentLayout?.FrameType != Layout?.FrameType ||
            parameters.GetParameterCount(vehicleId.Value) is not > 0 || values.Count < parameters.GetParameterCount(vehicleId.Value) ||
            values.ContainsKey("Q_FRAME_CLASS") || current.Count != expected.Count ||
            expected.Any(pair => !current.TryGetValue(pair.Key, out var value) || value != pair.Value))
        {
            lifetime?.Cancel();
            throw new InvalidOperationException("Frame or motor assignments changed. Close and restart the assistant.");
        }
    }

    private async Task StopOriginalVehicleAsync()
    {
        if (!pulseInFlight) return;
        try
        {
            if (vehicleId is { } id && activeVehicle.VehicleId == id && activeVehicle.IsOnline && !connectionToken.IsCancellationRequested)
            {
                await actuators.EmergencyStopAsync().ConfigureAwait(false);
                await MotorTestTelemetry.WaitUntilDisarmedAsync(activeVehicle, id, connectionToken).ConfigureAwait(false);
            }
        }
        finally { pulseInFlight = false; }
    }

    private void VehicleChanged(ActiveVehicleChangedEventArgs args)
    {
        if (args.Current.VehicleId != vehicleId || !args.Current.IsOnline)
        {
            contextInvalidated = true;
            lifetime?.Cancel();
        }
    }

    private static Dictionary<string, float> MotorParameters(IReadOnlyDictionary<string, VehicleParameter> values) => values
        .Where(pair => OutputChannel(pair.Key) > 0 && Enumerable.Range(1, 32).Any(motor => pair.Value.Value == MotorFunction(motor)))
        .ToDictionary(pair => pair.Key, pair => pair.Value.Value);

    private static int OutputChannel(string name) => name.StartsWith("SERVO", StringComparison.Ordinal) &&
        name.EndsWith("_FUNCTION", StringComparison.Ordinal) && name.Length > 14 &&
        int.TryParse(name.AsSpan(5, name.Length - 14), out var output) ? output : 0;

    public static int MotorFunction(int motor) => motor switch
    {
        >= 1 and <= 8 => 32 + motor,
        >= 9 and <= 12 => 73 + motor,
        >= 13 and <= 32 => 147 + motor,
        _ => throw new ArgumentOutOfRangeException(nameof(motor))
    };

    public void Dispose()
    {
        activeVehicle.Changed -= VehicleChanged;
        lifetime?.Cancel();
        lifetime?.Dispose();
        lifetime = null;
    }
}
