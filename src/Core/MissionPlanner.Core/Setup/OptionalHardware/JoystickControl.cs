namespace MissionPlanner.Core.Setup.OptionalHardware;

public sealed record JoystickDeviceDescriptor(string Id, string Name, int AxisCount, int ButtonCount, bool IsSupported = true);
public sealed record JoystickAxisState(int Index, double RawValue);
public sealed record JoystickButtonState(int Index, bool IsPressed);
public sealed record JoystickState(string DeviceId, IReadOnlyList<JoystickAxisState> Axes, IReadOnlyList<JoystickButtonState> Buttons, DateTimeOffset Timestamp);
public enum JoystickFunction { Roll, Pitch, Throttle, Yaw }
public sealed record JoystickAxisMapping(JoystickFunction Function, int AxisIndex, double Minimum = -1, double Center = 0, double Maximum = 1, double DeadZone = .05, bool Reverse = false);

public interface IJoystickDevice : IAsyncDisposable
{
    JoystickDeviceDescriptor Descriptor { get; }
    Task<JoystickState> ReadAsync(CancellationToken cancellationToken);
}
public interface IJoystickProvider
{
    bool IsSupported { get; }
    Task<IReadOnlyList<JoystickDeviceDescriptor>> EnumerateAsync(CancellationToken cancellationToken);
    Task<IJoystickDevice> OpenAsync(string deviceId, CancellationToken cancellationToken);
}
public sealed class UnsupportedJoystickProvider : IJoystickProvider
{
    public bool IsSupported => false;
    public Task<IReadOnlyList<JoystickDeviceDescriptor>> EnumerateAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<JoystickDeviceDescriptor>>([]);
    public Task<IJoystickDevice> OpenAsync(string id, CancellationToken ct) => throw new NotSupportedException("No joystick platform adapter is installed.");
}

public static class JoystickCalibration
{
    public static double Normalize(double raw, JoystickAxisMapping mapping)
    {
        var span = raw >= mapping.Center ? mapping.Maximum - mapping.Center : mapping.Center - mapping.Minimum;
        var value = span <= 0 ? 0 : (raw - mapping.Center) / span;
        value = Math.Clamp(value, -1, 1);
        if (Math.Abs(value) <= Math.Clamp(mapping.DeadZone, 0, .95)) value = 0;
        if (mapping.Reverse) value = -value;
        return value;
    }
}

public sealed record JoystickVehicleCommand(double Roll, double Pitch, double Throttle, double Yaw);
public interface IJoystickVehicleOutput
{
    Task SendManualControlAsync(JoystickVehicleCommand command, CancellationToken cancellationToken);
    Task ReleaseAsync(CancellationToken cancellationToken);
}
public sealed class DisabledJoystickVehicleOutput : IJoystickVehicleOutput
{
    public Task SendManualControlAsync(JoystickVehicleCommand command, CancellationToken ct) => throw new NotSupportedException("Vehicle joystick output requires an active MANUAL_CONTROL adapter.");
    public Task ReleaseAsync(CancellationToken ct) => Task.CompletedTask;
}
