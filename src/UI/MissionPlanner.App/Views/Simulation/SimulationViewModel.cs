using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Views.ConfigTuning;
using MissionPlanner.Core.Simulation;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.App.Views.Simulation;

/// <summary>Coordinates persisted simulator profiles and the observable simulation session.</summary>
public sealed partial class SimulationViewModel : ObservableObject, IDisposable
{
    private readonly ISimulatorProfileService profileService;
    private readonly ISimulationSessionManager sessionManager;
    private readonly ISimulationDiagnosticsService diagnosticsService;
    private readonly ParametersFileHandler fileHandler;
    private readonly IDispatcher dispatcher;
    private readonly ILogger<SimulationViewModel> logger;
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private CancellationTokenSource? timerCancellation;
    private bool initialized;
    private bool active;
    private bool disposed;

    /// <summary>Initializes the Simulation workspace view model.</summary>
    /// <param name="profileService">The persisted profile service.</param>
    /// <param name="sessionManager">The process-neutral session manager.</param>
    /// <param name="diagnosticsService">The redacted diagnostics service.</param>
    /// <param name="fileHandler">The platform file helper.</param>
    /// <param name="dispatcher">The UI dispatcher.</param>
    /// <param name="logger">The logger.</param>
    public SimulationViewModel(
        ISimulatorProfileService profileService,
        ISimulationSessionManager sessionManager,
        ISimulationDiagnosticsService diagnosticsService,
        ParametersFileHandler fileHandler,
        IDispatcher dispatcher,
        ILogger<SimulationViewModel> logger)
    {
        this.profileService = profileService;
        this.sessionManager = sessionManager;
        this.diagnosticsService = diagnosticsService;
        this.fileHandler = fileHandler;
        this.dispatcher = dispatcher;
        this.logger = logger;
        ApplySnapshot(sessionManager.Current);
    }

    /// <summary>Gets persisted profiles.</summary>
    public ObservableCollection<SimulatorProfile> Profiles { get; } = [];

    /// <summary>Gets the firmware families supported by the Simulation workspace.</summary>
    public IReadOnlyList<FirmwareFamily> FirmwareFamilies { get; } =
    [
        FirmwareFamily.ArduCopter,
        FirmwareFamily.ArduPlane,
        FirmwareFamily.Rover,
        FirmwareFamily.ArduSub
    ];

    /// <summary>Gets the bounded recent runtime output.</summary>
    public ObservableCollection<SimulatorOutputLine> RecentOutput { get; } = [];

    /// <summary>Gets or sets the selected persisted profile.</summary>
    [ObservableProperty]
    public partial SimulatorProfile? SelectedProfile { get; set; }

    /// <summary>Gets or sets the profile name.</summary>
    [ObservableProperty]
    public partial string ProfileName { get; set; } = string.Empty;

    /// <summary>Gets or sets the expected firmware family.</summary>
    [ObservableProperty]
    public partial FirmwareFamily SelectedFirmwareFamily { get; set; } = FirmwareFamily.ArduCopter;

    /// <summary>Gets or sets the frame/model identifier.</summary>
    [ObservableProperty]
    public partial string FrameModel { get; set; } = string.Empty;

    /// <summary>Gets or sets the selected simulator version label.</summary>
    [ObservableProperty]
    public partial string BinaryVersion { get; set; } = string.Empty;

    /// <summary>Gets or sets the absolute simulator executable path.</summary>
    [ObservableProperty]
    public partial string BinaryPath { get; set; } = string.Empty;

    /// <summary>Gets or sets the simulation speed multiplier.</summary>
    [ObservableProperty]
    public partial double Speedup { get; set; } = 1;

    /// <summary>Gets or sets start latitude.</summary>
    [ObservableProperty]
    public partial double Latitude { get; set; }

    /// <summary>Gets or sets start longitude.</summary>
    [ObservableProperty]
    public partial double Longitude { get; set; }

    /// <summary>Gets or sets start altitude in meters.</summary>
    [ObservableProperty]
    public partial double Altitude { get; set; }

    /// <summary>Gets or sets start heading in degrees.</summary>
    [ObservableProperty]
    public partial double Heading { get; set; }

    /// <summary>Gets or sets the local MAVLink UDP port.</summary>
    [ObservableProperty]
    public partial int MavLinkPort { get; set; } = 14550;

    /// <summary>Gets or sets the simulator console TCP port.</summary>
    [ObservableProperty]
    public partial int ConsolePort { get; set; } = 5760;

    /// <summary>Gets or sets additional argument tokens, one token per line.</summary>
    [ObservableProperty]
    public partial string AdditionalArgumentsText { get; set; } = string.Empty;

    /// <summary>Gets or sets environment entries as one NAME=VALUE pair per line.</summary>
    [ObservableProperty]
    public partial string EnvironmentText { get; set; } = string.Empty;

    /// <summary>Gets the current session state.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    [NotifyPropertyChangedFor(nameof(CanStop))]
    [NotifyPropertyChangedFor(nameof(CanRestart))]
    public partial SimulationSessionState SessionState { get; private set; } = SimulationSessionState.Stopped;

    /// <summary>Gets the current status message.</summary>
    [ObservableProperty]
    public partial string StatusMessage { get; private set; } = "No simulation is running.";

    /// <summary>Gets the current failure detail.</summary>
    [ObservableProperty]
    public partial string? FailureMessage { get; private set; }

    /// <summary>Gets the runtime identity or PID description.</summary>
    [ObservableProperty]
    public partial string RuntimeIdentity { get; private set; } = "Not started";

    /// <summary>Gets the runtime-confirmed endpoint display.</summary>
    [ObservableProperty]
    public partial string ConnectionEndpoints { get; private set; } = "No runtime endpoints";

    /// <summary>Gets the elapsed runtime text.</summary>
    [ObservableProperty]
    public partial string Elapsed { get; private set; } = "00:00:00";

    /// <summary>Gets whether a UI command is executing.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanStart))]
    [NotifyPropertyChangedFor(nameof(CanStop))]
    [NotifyPropertyChangedFor(nameof(CanRestart))]
    public partial bool IsBusy { get; private set; }

    /// <summary>Gets whether a new simulation may be started.</summary>
    public bool CanStart => !IsBusy && SessionState is SimulationSessionState.Stopped or
        SimulationSessionState.Completed or SimulationSessionState.Failed;

    /// <summary>Gets whether the owned simulation may be stopped.</summary>
    public bool CanStop => SessionState is SimulationSessionState.Starting or
        SimulationSessionState.WaitingForHeartbeat or SimulationSessionState.Running;

    /// <summary>Gets whether the selected or last profile may be restarted.</summary>
    public bool CanRestart => !IsBusy && sessionManager.Current.Profile is not null &&
        SessionState is not SimulationSessionState.Validating and
        not SimulationSessionState.Starting and
        not SimulationSessionState.WaitingForHeartbeat and
        not SimulationSessionState.Stopping;

    /// <summary>Activates workspace observation without changing simulator ownership.</summary>
    public void Activate()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (active)
        {
            return;
        }

        active = true;
        sessionManager.Changed += OnSessionChanged;
        timerCancellation = new CancellationTokenSource();
        _ = UpdateElapsedAsync(timerCancellation.Token);
        if (!initialized)
        {
            _ = InitializeAsync();
        }
        else
        {
            ApplySnapshot(sessionManager.Current);
        }
    }

    /// <summary>Detaches navigation-scoped observation while leaving owned sessions running.</summary>
    public void Deactivate()
    {
        if (!active)
        {
            return;
        }

        active = false;
        sessionManager.Changed -= OnSessionChanged;
        timerCancellation?.Cancel();
        timerCancellation?.Dispose();
        timerCancellation = null;
    }

    /// <summary>Loads persisted profiles.</summary>
    /// <returns>A task representing initialization.</returns>
    [RelayCommand]
    public Task InitializeAsync() => RunAsync(async cancellationToken =>
    {
        var profiles = await profileService.InitializeAsync(cancellationToken);
        Profiles.Clear();
        foreach (var profile in profiles)
        {
            Profiles.Add(profile);
        }

        SelectedProfile = Profiles.FirstOrDefault();
        initialized = true;
    });

    /// <summary>Creates a new editable profile from safe defaults.</summary>
    [RelayCommand]
    public void NewProfile()
    {
        SelectedProfile = SimulatorProfile.CreateDefault() with { Name = "New simulation profile" };
    }

    /// <summary>Saves the current profile editor.</summary>
    /// <returns>A task representing persistence.</returns>
    [RelayCommand]
    public Task SaveProfileAsync() => RunAsync(async cancellationToken =>
    {
        var profile = CreateProfile();
        await profileService.SaveAsync(profile, cancellationToken);
        ReplaceProfiles(profileService.Profiles, profile.Id);
        StatusMessage = $"Profile '{profile.Name}' saved.";
    });

    /// <summary>Deletes the selected profile.</summary>
    /// <returns>A task representing persistence.</returns>
    [RelayCommand]
    public Task DeleteProfileAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedProfile is null)
        {
            return;
        }

        await profileService.DeleteAsync(SelectedProfile.Id, cancellationToken);
        ReplaceProfiles(profileService.Profiles, profileService.Profiles[0].Id);
        StatusMessage = "Simulation profile deleted.";
    });

    /// <summary>Saves, validates, and starts the edited profile.</summary>
    /// <returns>A task representing startup through heartbeat readiness.</returns>
    [RelayCommand]
    public Task StartAsync() => RunAsync(async cancellationToken =>
    {
        var profile = CreateProfile();
        await profileService.SaveAsync(profile, cancellationToken);
        ReplaceProfiles(profileService.Profiles, profile.Id);
        await sessionManager.StartAsync(profile, cancellationToken);
    });

    /// <summary>Stops only the exact runtime session owned by the workspace.</summary>
    /// <returns>A task representing shutdown.</returns>
    [RelayCommand]
    public async Task StopAsync()
    {
        try
        {
            await sessionManager.StopAsync();
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Simulation stop failed.");
            StatusMessage = "Simulation stop failed.";
            FailureMessage = exception.Message;
        }
    }

    /// <summary>Restarts the current session profile.</summary>
    /// <returns>A task representing restart.</returns>
    [RelayCommand]
    public Task RestartAsync() => RunAsync(async cancellationToken =>
    {
        await sessionManager.RestartAsync(cancellationToken);
    });

    /// <summary>Exports a redacted structured diagnostic bundle.</summary>
    /// <returns>A task representing file export.</returns>
    [RelayCommand]
    public Task ExportDiagnosticsAsync() => RunAsync(async cancellationToken =>
    {
        var path = await fileHandler.SaveTextFileAsync(
            $"simulation-{sessionManager.Current.SessionId:N}-diagnostics.json",
            diagnosticsService.CreateBundle(sessionManager.Current),
            cancellationToken);
        StatusMessage = path is null ? "Diagnostic export cancelled." : $"Diagnostics exported to {path}.";
    });

    /// <inheritdoc />
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        Deactivate();
        operationGate.Dispose();
    }

    partial void OnSelectedProfileChanged(SimulatorProfile? value)
    {
        if (value is not null)
        {
            LoadProfile(value);
        }

        OnPropertyChanged(nameof(CanRestart));
    }

    private async Task RunAsync(Func<CancellationToken, Task> operation)
    {
        if (!await operationGate.WaitAsync(0))
        {
            return;
        }

        IsBusy = true;
        try
        {
            await operation(CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "Simulation operation cancelled.";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Simulation workspace operation failed.");
            StatusMessage = "Simulation operation failed.";
            FailureMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
            operationGate.Release();
        }
    }

    private void OnSessionChanged(object? sender, SimulationSessionChangedEventArgs e) =>
        dispatcher.Dispatch(() => ApplySnapshot(e.Snapshot));

    private void ApplySnapshot(SimulationSessionSnapshot snapshot)
    {
        SessionState = snapshot.State;
        StatusMessage = snapshot.Message;
        FailureMessage = snapshot.Failure;
        RuntimeIdentity = snapshot.RuntimeIdentity is null
            ? "Not started"
            : snapshot.RuntimeIdentity.ProcessId is { } processId
                ? $"{snapshot.RuntimeIdentity.Adapter} — {snapshot.RuntimeIdentity.RuntimeId} — PID {processId}"
                : $"{snapshot.RuntimeIdentity.Adapter} — {snapshot.RuntimeIdentity.RuntimeId}";
        ConnectionEndpoints = snapshot.ConnectionEndpoints.Count == 0
            ? "No runtime endpoints"
            : string.Join(Environment.NewLine, snapshot.ConnectionEndpoints.Select(item => item.DisplayText));
        RecentOutput.Clear();
        foreach (var line in snapshot.RecentOutput)
        {
            RecentOutput.Add(line);
        }

        UpdateElapsed(snapshot);
    }

    private async Task UpdateElapsedAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                dispatcher.Dispatch(() => UpdateElapsed(sessionManager.Current));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }

    private void UpdateElapsed(SimulationSessionSnapshot snapshot)
    {
        var end = snapshot.EndedAt ?? DateTimeOffset.UtcNow;
        var elapsed = snapshot.StartedAt is null ? TimeSpan.Zero : end - snapshot.StartedAt.Value;
        Elapsed = elapsed.ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);
    }

    private SimulatorProfile CreateProfile()
    {
        var id = SelectedProfile?.Id ?? Guid.NewGuid();
        var arguments = AdditionalArgumentsText.Split(
            ['\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var environment = EnvironmentText.Split(
                ['\r', '\n'],
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('=', 2))
            .Where(parts => parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]))
            .ToDictionary(parts => parts[0].Trim(), parts => parts[1], StringComparer.OrdinalIgnoreCase);
        return new SimulatorProfile(
            id,
            ProfileName.Trim(),
            SelectedFirmwareFamily,
            FrameModel.Trim(),
            new SimulationLocation(Latitude, Longitude, Altitude, Heading),
            Speedup,
            [
                new SimulationEndpoint("MAVLink", SimulationEndpointTransport.Udp, "127.0.0.1", MavLinkPort),
                new SimulationEndpoint("Console", SimulationEndpointTransport.Tcp, "127.0.0.1", ConsolePort)
            ],
            new SimulatorBinaryReference(BinaryVersion.Trim(), BinaryPath.Trim(), SelectedProfile?.Binary.Source ?? "external"),
            arguments,
            environment);
    }

    private void LoadProfile(SimulatorProfile profile)
    {
        ProfileName = profile.Name;
        SelectedFirmwareFamily = profile.FirmwareFamily;
        FrameModel = profile.FrameModel;
        BinaryVersion = profile.Binary.Version;
        BinaryPath = profile.Binary.ExecutablePath;
        Speedup = profile.Speedup;
        Latitude = profile.Location.LatitudeDegrees;
        Longitude = profile.Location.LongitudeDegrees;
        Altitude = profile.Location.AltitudeMeters;
        Heading = profile.Location.HeadingDegrees;
        MavLinkPort = profile.Endpoints.FirstOrDefault(item => item.Name.Equals("MAVLink", StringComparison.OrdinalIgnoreCase))?.Port ?? 14550;
        ConsolePort = profile.Endpoints.FirstOrDefault(item => item.Name.Equals("Console", StringComparison.OrdinalIgnoreCase))?.Port ?? 5760;
        AdditionalArgumentsText = string.Join(Environment.NewLine, profile.AdditionalArguments);
        EnvironmentText = string.Join(
            Environment.NewLine,
            profile.Environment.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .Select(item => $"{item.Key}={item.Value}"));
    }

    private void ReplaceProfiles(IReadOnlyList<SimulatorProfile> profiles, Guid selectedId)
    {
        Profiles.Clear();
        foreach (var profile in profiles)
        {
            Profiles.Add(profile);
        }

        SelectedProfile = Profiles.FirstOrDefault(item => item.Id == selectedId) ?? Profiles.FirstOrDefault();
    }
}
