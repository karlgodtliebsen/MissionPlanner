namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Describes a joystick or gamepad the platform can open.</summary>
/// <param name="Id">The platform device identifier.</param>
/// <param name="Name">The display name.</param>
/// <param name="AxisCount">The number of axes.</param>
/// <param name="ButtonCount">The number of buttons.</param>
/// <param name="IsSupported">Whether the device can be used for manual control.</param>
public sealed record JoystickDeviceDescriptor(string Id, string Name, int AxisCount, int ButtonCount, bool IsSupported = true);
/// <summary>The raw value of one joystick axis.</summary>
/// <param name="Index">The zero-based axis index.</param>
/// <param name="RawValue">The raw axis reading.</param>
public sealed record JoystickAxisState(int Index, double RawValue);
/// <summary>The state of one joystick button.</summary>
/// <param name="Index">The zero-based button index.</param>
/// <param name="IsPressed">Whether the button is pressed.</param>
public sealed record JoystickButtonState(int Index, bool IsPressed);
/// <summary>A snapshot of every axis and button on a joystick.</summary>
/// <param name="DeviceId">The device the state was read from.</param>
/// <param name="Axes">The axis readings.</param>
/// <param name="Buttons">The button states.</param>
/// <param name="Timestamp">When the state was read.</param>
public sealed record JoystickState(string DeviceId, IReadOnlyList<JoystickAxisState> Axes, IReadOnlyList<JoystickButtonState> Buttons, DateTimeOffset Timestamp);
/// <summary>The manual-control function an axis is assigned to.</summary>
public enum JoystickFunction
{
    /// <summary>Roll command.</summary>
    Roll,
    /// <summary>Pitch command.</summary>
    Pitch,
    /// <summary>Throttle command.</summary>
    Throttle,
    /// <summary>Yaw command.</summary>
    Yaw,
}
/// <summary>Maps a joystick axis to a manual-control function with its calibration.</summary>
/// <param name="Function">The function the axis controls.</param>
/// <param name="AxisIndex">The zero-based axis index.</param>
/// <param name="Minimum">The raw value at full negative deflection.</param>
/// <param name="Center">The raw value at rest.</param>
/// <param name="Maximum">The raw value at full positive deflection.</param>
/// <param name="DeadZone">The normalized dead zone around the center.</param>
/// <param name="Reverse">Whether the axis direction is inverted.</param>
public sealed record JoystickAxisMapping(JoystickFunction Function, int AxisIndex, double Minimum = -1, double Center = 0, double Maximum = 1, double DeadZone = .05, bool Reverse = false);

/// <summary>A joystick opened for reading.</summary>
public interface IJoystickDevice : IAsyncDisposable
{
    /// <summary>Gets the device description.</summary>
    JoystickDeviceDescriptor Descriptor { get; }
    /// <summary>Reads the current joystick state.</summary>
    Task<JoystickState> ReadAsync(CancellationToken cancellationToken);
}
/// <summary>Enumerates and opens joysticks provided by the platform.</summary>
public interface IJoystickProvider
{
    /// <summary>Gets whether the platform provides joystick access.</summary>
    bool IsSupported { get; }
    /// <summary>Lists the joysticks that are available.</summary>
    Task<IReadOnlyList<JoystickDeviceDescriptor>> EnumerateAsync(CancellationToken cancellationToken);
    /// <summary>Opens a joystick for reading.</summary>
    Task<IJoystickDevice> OpenAsync(string deviceId, CancellationToken cancellationToken);
}
/// <summary>Joystick provider used where no platform adapter is installed; it reports no devices.</summary>
public sealed class UnsupportedJoystickProvider : IJoystickProvider
{
    /// <summary>Gets a value indicating that joystick access is unavailable.</summary>
    public bool IsSupported => false;
    /// <summary>Returns an empty device list.</summary>
    public Task<IReadOnlyList<JoystickDeviceDescriptor>> EnumerateAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<JoystickDeviceDescriptor>>([]);
    /// <summary>Always throws because no joystick platform adapter is installed.</summary>
    public Task<IJoystickDevice> OpenAsync(string id, CancellationToken ct) => throw new NotSupportedException("No joystick platform adapter is installed.");
}

/// <summary>Converts raw axis readings into normalized manual-control values.</summary>
public static class JoystickCalibration
{
    /// <summary>Normalizes a raw axis reading to the range -1 to 1, applying the center, dead zone and reversal of the mapping.</summary>
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

/// <summary>A normalized manual-control command built from joystick axes.</summary>
/// <param name="Roll">The roll command from -1 to 1.</param>
/// <param name="Pitch">The pitch command from -1 to 1.</param>
/// <param name="Throttle">The throttle command from -1 to 1.</param>
/// <param name="Yaw">The yaw command from -1 to 1.</param>
public sealed record JoystickVehicleCommand(double Roll, double Pitch, double Throttle, double Yaw);
/// <summary>Sends joystick commands to a vehicle.</summary>
public interface IJoystickVehicleOutput
{
    /// <summary>Sends a manual-control command to the vehicle.</summary>
    Task SendManualControlAsync(JoystickVehicleCommand command, CancellationToken cancellationToken);
    /// <summary>Stops sending manual-control commands and returns control to the vehicle.</summary>
    Task ReleaseAsync(CancellationToken cancellationToken);
}
/// <summary>Joystick output that is disabled; it refuses to send commands.</summary>
public sealed class DisabledJoystickVehicleOutput : IJoystickVehicleOutput
{
    /// <summary>Always throws because vehicle joystick output is disabled.</summary>
    public Task SendManualControlAsync(JoystickVehicleCommand command, CancellationToken ct) => throw new NotSupportedException("Vehicle joystick output requires an active MANUAL_CONTROL adapter.");
    /// <summary>Does nothing because no commands are being sent.</summary>
    public Task ReleaseAsync(CancellationToken ct) => Task.CompletedTask;
}
