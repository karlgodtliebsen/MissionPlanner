namespace MissionPlanner.Core.Setup.OptionalHardware;

/// <summary>Persisted local mapping; enabling vehicle output is deliberately not persisted.</summary>
public sealed record JoystickProfile
{
    /// <summary>Gets the device name for reviewing saved calibration compatibility.</summary>
    public string DeviceName { get; init; } = string.Empty;
    /// <summary>Gets four axis mappings.</summary>
    public IReadOnlyList<JoystickAxisMapping> Axes { get; init; } = Enum.GetValues<JoystickFunction>().Select((function, index) => new JoystickAxisMapping(function, index)).ToArray();
    /// <summary>Gets the button which must remain held while output is enabled.</summary>
    public int DeadmanButton { get; init; }
    /// <summary>Gets physical button indices for MANUAL_CONTROL bits, or -1 for unused bits.</summary>
    public IReadOnlyList<int> Buttons { get; init; } = [];
    /// <summary>Gets the chosen output protocol.</summary>
    public JoystickOutputMode OutputMode { get; init; }
    /// <summary>Gets whether the operator reviewed the live calibrated axes.</summary>
    public bool CalibrationReviewed { get; init; }

    /// <summary>Validates this profile against an actually opened device.</summary>
    public void Validate(JoystickDeviceDescriptor device)
    {
        if (DeviceName is null || DeviceName.Length > 256 || Axes is null || Axes.Count != 4 || Axes.Any(axis => axis is null) || Axes.Select(axis => axis.Function).Distinct().Count() != 4 ||
            Axes.Any(axis => !Enum.IsDefined(axis.Function) || axis.AxisIndex < 0 || axis.AxisIndex >= device.AxisCount) ||
            Axes.Select(axis => axis.AxisIndex).Distinct().Count() != 4 || !Enum.IsDefined(OutputMode) ||
            DeadmanButton < 0 || DeadmanButton >= device.ButtonCount || Buttons is null || Buttons.Count > 32 ||
            Buttons.Any(button => button < -1 || button >= device.ButtonCount || button == DeadmanButton))
            throw new ArgumentException("Choose four distinct valid axes, a deadman button, and valid optional button mappings which exclude the deadman.");
        foreach (var axis in Axes) _ = JoystickCalibration.Normalize(axis.Center, axis);
    }
}

/// <summary>A bounded preview and optional control loop; input loss releases the captured vehicle.</summary>
public sealed class JoystickControlSession(IJoystickProvider provider, IJoystickVehicleOutput output) : IAsyncDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private CancellationTokenSource? lifetime;
    private Task? worker;
    private JoystickProfile? profile;
    private IJoystickDevice? device;
    private JoystickState? latest;
    /// <summary>Gets whether local input sampling is running.</summary>
    public bool IsRunning => worker is { IsCompleted: false };
    /// <summary>Gets whether vehicle output is enabled.</summary>
    public bool OutputEnabled => output.IsEnabled;
    /// <summary>Reports sampled input and normalized command; subscribers must marshal UI updates.</summary>
    public event Action<JoystickState, JoystickVehicleCommand>? Sampled;
    /// <summary>Reports lifecycle/error messages without an automatic restart.</summary>
    public event Action<string>? StatusChanged;

    /// <summary>Starts local preview with a validated immutable profile, leaving output disabled.</summary>
    public async Task StartAsync(string deviceId, JoystickProfile selectedProfile, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (IsRunning) throw new InvalidOperationException("Stop the current preview before changing the device or mapping.");
            await output.ReleaseAsync(cancellationToken).ConfigureAwait(false);
            device = await provider.OpenAsync(deviceId, cancellationToken).ConfigureAwait(false);
            try { selectedProfile.Validate(device.Descriptor); }
            catch { await device.DisposeAsync().ConfigureAwait(false); device = null; throw; }
            profile = selectedProfile;
            latest = null;
            lifetime?.Dispose();
            lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = lifetime.Token;
            var selectedDevice = device;
            worker = Task.Run(() => RunAsync(selectedDevice, selectedProfile, token), CancellationToken.None);
        }
        finally { gate.Release(); }
    }

    /// <summary>Enables output only with fresh, low-throttle input, reviewed calibration and a held deadman.</summary>
    public async Task EnableAsync(bool confirmed, CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var settings = profile ?? throw new InvalidOperationException("Start and review local preview first.");
            var state = Volatile.Read(ref latest);
            if (!IsRunning || state is null || DateTimeOffset.UtcNow - state.Timestamp > TimeSpan.FromMilliseconds(250) ||
                !settings.CalibrationReviewed || !state.Buttons.Any(button => button.Index == settings.DeadmanButton && button.IsPressed) ||
                Map(state, settings).Throttle > -.9)
                throw new InvalidOperationException("Review calibration, hold the deadman button and lower throttle below 5% before enabling output.");
            await output.EnableAsync(settings.OutputMode, confirmed, cancellationToken).ConfigureAwait(false);
            if (!IsRunning || Volatile.Read(ref latest) is null)
            {
                await output.ReleaseAsync(CancellationToken.None).ConfigureAwait(false);
                throw new IOException("The input device stopped while vehicle control was being enabled.");
            }
            StatusChanged?.Invoke("Vehicle control enabled. Release the deadman button to disable and release overrides.");
        }
        finally { gate.Release(); }
    }

    /// <summary>Disables and releases vehicle output while retaining local preview.</summary>
    public Task DisableAsync(CancellationToken cancellationToken) => output.ReleaseAsync(cancellationToken);

    private async Task RunAsync(IJoystickDevice opened, JoystickProfile settings, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                using var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                readTimeout.CancelAfter(TimeSpan.FromMilliseconds(250));
                var state = await opened.ReadAsync(readTimeout.Token).WaitAsync(readTimeout.Token).ConfigureAwait(false);
                if (state.DeviceId != opened.Descriptor.Id || DateTimeOffset.UtcNow - state.Timestamp > TimeSpan.FromMilliseconds(250) || state.Timestamp > DateTimeOffset.UtcNow.AddMilliseconds(50))
                    throw new IOException("Joystick input became stale or changed identity.");
                var command = Map(state, settings);
                Volatile.Write(ref latest, state);
                if (output.IsEnabled)
                {
                    if (!state.Buttons.Any(button => button.Index == settings.DeadmanButton && button.IsPressed))
                    {
                        await output.ReleaseAsync(token).ConfigureAwait(false);
                        StatusChanged?.Invoke("Deadman released: vehicle output disabled. Explicitly enable again to resume.");
                    }
                    else { await output.SendManualControlAsync(command, token).ConfigureAwait(false); }
                }
                Sampled?.Invoke(state, command);
                await Task.Delay(50, token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) { StatusChanged?.Invoke(token.IsCancellationRequested ? "Joystick preview stopped." : "Joystick input timed out; output stopped."); }
        catch (Exception ex) { StatusChanged?.Invoke($"Joystick stopped: {ex.Message}"); }
        finally
        {
            Volatile.Write(ref latest, null);
            try { await output.ReleaseAsync(CancellationToken.None).ConfigureAwait(false); }
            catch (Exception) { StatusChanged?.Invoke("Output stopped; release could not be delivered. The vehicle's configured RC override timeout remains the fallback."); }
            await opened.DisposeAsync().ConfigureAwait(false);
        }
    }

    /// <summary>Maps a complete finite sample using the reviewed calibration and optional button bit assignments.</summary>
    public static JoystickVehicleCommand Map(JoystickState state, JoystickProfile settings)
    {
        double Axis(JoystickFunction function)
        {
            var mapping = settings.Axes.Single(axis => axis.Function == function);
            var sample = state.Axes.SingleOrDefault(axis => axis.Index == mapping.AxisIndex)
                ?? throw new IOException("A mapped joystick axis is missing.");
            return JoystickCalibration.Normalize(sample.RawValue, mapping);
        }
        uint buttons = 0;
        for (var bit = 0; bit < settings.Buttons.Count; bit++)
            if (settings.Buttons[bit] >= 0 && state.Buttons.Any(button => button.Index == settings.Buttons[bit] && button.IsPressed)) buttons |= 1u << bit;
        return new(Axis(JoystickFunction.Roll), Axis(JoystickFunction.Pitch), Axis(JoystickFunction.Throttle), Axis(JoystickFunction.Yaw)) { Buttons = buttons };
    }

    /// <summary>Stops preview, releases output and closes the input device.</summary>
    public async Task StopAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try
        {
            lifetime?.Cancel();
            if (worker is not null) await worker.ConfigureAwait(false);
            worker = null;
            device = null;
        }
        finally { gate.Release(); }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        lifetime?.Dispose();
    }
}
