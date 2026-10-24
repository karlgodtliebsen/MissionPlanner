using MissionPlanner.Core.Commands;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;

namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Selects the explicit joystick output protocol.</summary>
public enum JoystickOutputMode
{
    /// <summary>Normalized axes and mapped button bits.</summary>
    ManualControl,
    /// <summary>PWM overrides for the reported RCMAP channels and calibrated RC limits.</summary>
    RcOverride,
}

/// <summary>Transmits joystick input to one captured connection; never implicitly enables output.</summary>
public sealed class MavLinkJoystickOutput(IActiveVehicleContext active, IVehicleRegistry registry,
    IVehicleParameterRegistry parameters, IMavLinkConnection connection, IMavLinkWireMessageEncoder encoder,
    IVehicleOperationGate operations) : IJoystickVehicleOutput
{
    private readonly SemaphoreSlim serial = new(1, 1);
    private VehicleSession? target;
    private CancellationToken boundary;
    private IDisposable? lease;
    private JoystickOutputMode mode;
    private readonly List<(int Channel, double Minimum, double Trim, double Maximum)> channels = [];

    /// <summary>Gets whether an explicit output session is enabled.</summary>
    public bool IsEnabled => target is not null;

    /// <summary>Captures a disarmed ArduPilot target and its RC mapping after operator confirmation.</summary>
    public async Task EnableAsync(JoystickOutputMode selectedMode, bool confirmed, CancellationToken cancellationToken)
    {
        await serial.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (target is not null) throw new InvalidOperationException("Disable joystick output before changing its configuration.");
            if (!confirmed || !Enum.IsDefined(selectedMode) || !active.IsOnline || active.VehicleId is not { } id || active.State is not { IsArmed: false, Autopilot: 3 } state ||
                DateTimeOffset.UtcNow - state.Connection.LastHeartbeatAt > TimeSpan.FromSeconds(3))
                throw new InvalidOperationException("Select a connected, disarmed ArduPilot vehicle and confirm joystick control.");
            var session = registry.GetRequired(id) ?? throw new InvalidOperationException("Vehicle session unavailable.");
            channels.Clear();
            var values = parameters.GetAllParameters(id);
            if (selectedMode == JoystickOutputMode.RcOverride)
            {
                foreach (var function in new[] { "ROLL", "PITCH", "THROTTLE", "YAW" })
                {
                    double Read(string name) => values.TryGetValue(name, out var parameter) && double.IsFinite(parameter.Value)
                        ? parameter.Value : throw new InvalidOperationException($"Read the reported {name} parameter before enabling RC override.");
                    var channelValue = Read("RCMAP_" + function);
                    if (channelValue != Math.Truncate(channelValue) || channelValue is < 1 or > 18)
                        throw new InvalidOperationException("RCMAP contains an invalid channel.");
                    var channel = (int)channelValue;
                    var minimum = Read($"RC{channel}_MIN");
                    var maximum = Read($"RC{channel}_MAX");
                    var trim = Read($"RC{channel}_TRIM");
                    if (minimum < 800 || maximum > 2200 || minimum >= trim || trim >= maximum)
                        throw new InvalidOperationException("Calibrate the vehicle RC channel ranges before enabling RC override.");
                    channels.Add((channel, minimum, trim, maximum));
                }
                if (channels.Select(item => item.Channel).Distinct().Count() != 4)
                    throw new InvalidOperationException("RCMAP functions must use four distinct channels.");
            }
            if (!operations.TryAcquire(id, "Joystick vehicle control", out lease))
                throw new InvalidOperationException("Another vehicle operation is in progress.");
            boundary = active.ConnectionCancellationToken;
            mode = selectedMode;
            target = session;
        }
        finally { serial.Release(); }
    }

    /// <inheritdoc />
    public async Task SendManualControlAsync(JoystickVehicleCommand command, CancellationToken cancellationToken)
    {
        await serial.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var session = target ?? throw new InvalidOperationException("Explicitly enable joystick output first.");
            if (boundary.IsCancellationRequested || boundary != active.ConnectionCancellationToken || active.VehicleId != session.Id || !active.IsOnline ||
                !ReferenceEquals(registry.GetRequired(session.Id), session) || DateTimeOffset.UtcNow - session.State.Connection.LastHeartbeatAt > TimeSpan.FromSeconds(3))
                throw new IOException("The joystick target connection is no longer current.");
            var values = new[] { command.Roll, command.Pitch, command.Throttle, command.Yaw };
            if (values.Any(value => !double.IsFinite(value) || value is < -1 or > 1))
                throw new ArgumentException("Joystick axes must be finite normalized values.");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, boundary);
            timeout.CancelAfter(TimeSpan.FromMilliseconds(250));
            GeneratedMavLinkMessage message;
            if (mode == JoystickOutputMode.ManualControl)
            {
                message = new ManualControlMessage(255, 190, session.EndPoint, session.Id.SystemId,
                    (short)Math.Round(-command.Pitch * 1000), (short)Math.Round(command.Roll * 1000),
                    (short)Math.Round((command.Throttle + 1) * 500), (short)Math.Round(command.Yaw * 1000),
                    (ushort)command.Buttons, (ushort)(command.Buttons >> 16), 0, 0, 0, 0, 0, 0, 0, 0, 0, DateTimeOffset.UtcNow);
            }
            else
            {
                var pwm = Enumerable.Repeat(ushort.MaxValue, 18).ToArray();
                for (var index = 0; index < channels.Count; index++)
                {
                    var map = channels[index];
                    var value = values[index];
                    var scaled = index == 2 ? map.Minimum + (value + 1) / 2 * (map.Maximum - map.Minimum)
                        : value >= 0 ? map.Trim + value * (map.Maximum - map.Trim) : map.Trim + value * (map.Trim - map.Minimum);
                    pwm[map.Channel - 1] = (ushort)Math.Round(scaled);
                }
                message = Override(session, pwm);
            }
            await connection.SendRawAsync(encoder.Encode(message), session.EndPoint, timeout.Token).ConfigureAwait(false);
        }
        finally { serial.Release(); }
    }

    /// <inheritdoc />
    public async Task ReleaseAsync(CancellationToken cancellationToken)
    {
        await serial.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var session = target;
            target = null;
            try
            {
                if (session is not null && !boundary.IsCancellationRequested && boundary == active.ConnectionCancellationToken && active.VehicleId == session.Id)
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, boundary);
                    timeout.CancelAfter(TimeSpan.FromMilliseconds(500));
                    // MANUAL_CONTROL is translated into RC overrides by ArduPilot; explicit releases clear those inputs.
                    var values = Enumerable.Range(0, 18).Select(index => index < 8 ? (ushort)0 : (ushort)65534).ToArray();
                    await connection.SendRawAsync(encoder.Encode(Override(session, values)), session.EndPoint, timeout.Token).ConfigureAwait(false);
                }
            }
            finally { lease?.Dispose(); lease = null; }
        }
        finally { serial.Release(); }
    }

    private static RcChannelsOverrideMessage Override(VehicleSession session, ushort[] values) => new(
        255, 190, session.EndPoint, session.Id.SystemId, session.Id.ComponentId,
        values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7], values[8],
        values[9], values[10], values[11], values[12], values[13], values[14], values[15], values[16], values[17], DateTimeOffset.UtcNow);
}
