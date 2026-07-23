using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Views.ConfigTuning;
using MissionPlanner.Core.Firmware;
using MissionPlanner.Core.Simulation;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.App.Views.Simulation;

/// <summary>Coordinates persisted simulator profiles and the observable simulation session.</summary>
public sealed partial class SimulationViewModel : ObservableObject, IDisposable
{
    private readonly ISimulatorProfileService profileService;
    private readonly ISimulationSessionManager sessionManager;
    private readonly ISimulationDiagnosticsService diagnosticsService;
    private readonly ISitlInstallationService installationService;
    private readonly ISitlPlatformService platformService;
    private readonly IArduPilotFrameCatalog frameCatalog;
    private readonly ISimulationControlCatalog controlCatalog;
    private readonly ISimulationControlService controlService;
    private readonly ISimulationScenarioPresetService scenarioPresetService;
    private readonly ISimulationScenarioParser scenarioParser;
    private readonly ISimulationScenarioRunner scenarioRunner;
    private readonly ISimulationScenarioReportExporter scenarioReportExporter;
    private readonly ISimulationFleetManager? fleetManager;
    private readonly ParametersFileHandler fileHandler;
    private readonly IDispatcher dispatcher;
    private readonly ILogger<SimulationViewModel> logger;
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private CancellationTokenSource? operationCancellation;
    private CancellationTokenSource? timerCancellation;
    private bool initialized;
    private bool active;
    private bool disposed;
    private readonly Dictionary<string, SimulationPresetControlValue> stagedControls = new(StringComparer.Ordinal);
    private bool refreshingFleetSelection;

    /// <summary>Initializes the Simulation workspace view model.</summary>
    /// <param name="profileService">The persisted profile service.</param>
    /// <param name="sessionManager">The process-neutral session manager.</param>
    /// <param name="diagnosticsService">The redacted diagnostics service.</param>
    /// <param name="installationService">The verified SITL installation service.</param>
    /// <param name="platformService">The host SITL capability service.</param>
    /// <param name="frameCatalog">The supported ArduPilot frame/model catalog.</param>
    /// <param name="controlCatalog">The documented runtime-control and location catalog.</param>
    /// <param name="controlService">The instance-scoped runtime-control service.</param>
    /// <param name="scenarioPresetService">The separate scenario-preset service.</param>
    /// <param name="scenarioParser">The closed declarative scenario parser.</param>
    /// <param name="scenarioRunner">The exact-target scenario runner.</param>
    /// <param name="scenarioReportExporter">The machine/readable report exporter.</param>
    /// <param name="fileHandler">The platform file helper.</param>
    /// <param name="dispatcher">The UI dispatcher.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="fleetManager">Optional multi-instance fleet coordinator.</param>
    public SimulationViewModel(
        ISimulatorProfileService profileService,
        ISimulationSessionManager sessionManager,
        ISimulationDiagnosticsService diagnosticsService,
        ISitlInstallationService installationService,
        ISitlPlatformService platformService,
        IArduPilotFrameCatalog frameCatalog,
        ISimulationControlCatalog controlCatalog,
        ISimulationControlService controlService,
        ISimulationScenarioPresetService scenarioPresetService,
        ISimulationScenarioParser scenarioParser,
        ISimulationScenarioRunner scenarioRunner,
        ISimulationScenarioReportExporter scenarioReportExporter,
        ParametersFileHandler fileHandler,
        IDispatcher dispatcher,
        ILogger<SimulationViewModel> logger,
        ISimulationFleetManager? fleetManager = null)
    {
        this.profileService = profileService;
        this.sessionManager = sessionManager;
        this.diagnosticsService = diagnosticsService;
        this.installationService = installationService;
        this.platformService = platformService;
        this.frameCatalog = frameCatalog;
        this.controlCatalog = controlCatalog;
        this.controlService = controlService;
        this.scenarioPresetService = scenarioPresetService;
        this.scenarioParser = scenarioParser;
        this.scenarioRunner = scenarioRunner;
        this.scenarioReportExporter = scenarioReportExporter;
        this.fileHandler = fileHandler;
        this.dispatcher = dispatcher;
        this.logger = logger;
        this.fleetManager = fleetManager;
        ApplySnapshot(sessionManager.Current);
        PlatformCapability = platformService.Current.Message;
        ScenarioRunnerStatus = scenarioRunner.Current?.Message ?? "No scenario is running.";
        scenarioRunner.Changed += OnScenarioRunnerChanged;
        foreach (var location in controlCatalog.Locations)
        {
            LocationPresets.Add(location);
        }

        RefreshFrames();
    }

    /// <summary>Gets persisted profiles.</summary>
    public ObservableCollection<SimulatorProfile> Profiles { get; } = [];

    /// <summary>Gets discovered external and verified cached SITL installations.</summary>
    public ObservableCollection<SitlInstallation> Installations { get; } = [];

    /// <summary>Gets compatible verified releases from the configured manifest.</summary>
    public ObservableCollection<SitlManifestEntry> AvailableReleases { get; } = [];

    /// <summary>Gets documented runtime control capabilities for the connected simulator.</summary>
    public ObservableCollection<SimulationControlCapability> ControlCapabilities { get; } = [];

    /// <summary>Gets built-in typed start-location presets.</summary>
    public ObservableCollection<SimulationLocationPreset> LocationPresets { get; } = [];

    /// <summary>Gets persisted scenario presets separate from launch profiles.</summary>
    public ObservableCollection<SimulationScenarioPreset> ScenarioPresets { get; } = [];

    /// <summary>Gets auditable runtime control events.</summary>
    public ObservableCollection<SimulationScenarioEvent> ScenarioEvents { get; } = [];

    /// <summary>Gets available release channels.</summary>
    public IReadOnlyList<FirmwareReleaseChannel> ReleaseChannels { get; } = Enum.GetValues<FirmwareReleaseChannel>();

    /// <summary>Gets the firmware families supported by the Simulation workspace.</summary>
    public IReadOnlyList<FirmwareFamily> FirmwareFamilies { get; } =
    [
        FirmwareFamily.ArduCopter,
        FirmwareFamily.ArduPlane,
        FirmwareFamily.Rover,
        FirmwareFamily.ArduSub
    ];

    /// <summary>Gets supported direct-SITL frames for the selected firmware family.</summary>
    public ObservableCollection<string> AvailableFrames { get; } = [];

    /// <summary>Gets the bounded recent runtime output.</summary>
    public ObservableCollection<SimulatorOutputLine> RecentOutput { get; } = [];

    /// <summary>Gets every independently owned simulator fleet member.</summary>
    public ObservableCollection<SimulationFleetSessionSnapshot> FleetSessions { get; } = [];

    /// <summary>Gets or sets the explicitly selected simulator fleet member.</summary>
    [ObservableProperty]
    public partial SimulationFleetSessionSnapshot? SelectedFleetSession { get; set; }

    /// <summary>Gets or sets the requested fleet size.</summary>
    [ObservableProperty]
    public partial int FleetCount { get; set; } = 2;

    /// <summary>Gets or sets north/south launch spacing in metres.</summary>
    [ObservableProperty]
    public partial double FleetSpacingMeters { get; set; } = 10;

    /// <summary>Gets or sets the deterministic port increment per fleet member.</summary>
    [ObservableProperty]
    public partial int FleetPortStride { get; set; } = 10;

    /// <summary>Gets or sets the bounded number of concurrent fleet lifecycle operations.</summary>
    [ObservableProperty]
    public partial int FleetMaximumConcurrency { get; set; } = 3;

    /// <summary>Gets the latest fleet-level operation summary.</summary>
    [ObservableProperty]
    public partial string FleetStatus { get; private set; } = "No simulator fleet is allocated.";

    /// <summary>Gets or sets the selected discovered installation.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRemoveInstallation))]
    public partial SitlInstallation? SelectedInstallation { get; set; }

    /// <summary>Gets or sets the selected manifest release.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstallRelease))]
    public partial SitlManifestEntry? SelectedRelease { get; set; }

    /// <summary>Gets or sets the requested manifest channel.</summary>
    [ObservableProperty]
    public partial FirmwareReleaseChannel SelectedReleaseChannel { get; set; } = FirmwareReleaseChannel.Stable;

    /// <summary>Gets the detected platform capability.</summary>
    [ObservableProperty]
    public partial string PlatformCapability { get; private set; } = string.Empty;

    /// <summary>Gets install/download progress from zero to one.</summary>
    [ObservableProperty]
    public partial double InstallProgress { get; private set; }

    /// <summary>Gets installation discovery or download status.</summary>
    [ObservableProperty]
    public partial string InstallationStatus { get; private set; } = "SITL installations have not been scanned.";

    /// <summary>Gets or sets the selected runtime control capability.</summary>
    [ObservableProperty]
    public partial SimulationControlCapability? SelectedControl { get; set; }

    /// <summary>Gets or sets the requested value for the selected control.</summary>
    [ObservableProperty]
    public partial double ControlRequestedValue { get; set; }

    /// <summary>Gets or sets the bounded hazardous-control duration in seconds.</summary>
    [ObservableProperty]
    public partial double FaultDurationSeconds { get; set; } = 10;

    /// <summary>Gets or sets explicit hazardous-action confirmation.</summary>
    [ObservableProperty]
    public partial bool HazardConfirmed { get; set; }

    /// <summary>Gets or sets the selected launch-location preset.</summary>
    [ObservableProperty]
    public partial SimulationLocationPreset? SelectedLocationPreset { get; set; }

    /// <summary>Gets or sets the selected persisted scenario preset.</summary>
    [ObservableProperty]
    public partial SimulationScenarioPreset? SelectedScenarioPreset { get; set; }

    /// <summary>Gets or sets the scenario preset editor name.</summary>
    [ObservableProperty]
    public partial string ScenarioPresetName { get; set; } = "Simulation scenario";

    /// <summary>Gets or sets the closed-schema scenario JSON editor.</summary>
    [ObservableProperty]
    public partial string ScenarioDocumentText { get; set; } = ExampleScenarioJson;

    /// <summary>Gets or sets explicit confirmation for hazardous scenario actions.</summary>
    [ObservableProperty]
    public partial bool ScenarioHazardsConfirmed { get; set; }

    /// <summary>Gets the current scenario runner status.</summary>
    [ObservableProperty]
    public partial string ScenarioRunnerStatus { get; private set; } = "No scenario is running.";

    /// <summary>Gets the last dry-run or execution report.</summary>
    [ObservableProperty]
    public partial SimulationScenarioRunReport? LastScenarioReport { get; private set; }

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

    /// <summary>Gets or sets the zero-based SITL instance number.</summary>
    [ObservableProperty]
    public partial int InstanceNumber { get; set; }

    /// <summary>Gets or sets the expected MAVLink system ID.</summary>
    [ObservableProperty]
    public partial byte SystemId { get; set; } = 1;

    /// <summary>Gets or sets absolute defaults/parameter file paths, one per line.</summary>
    [ObservableProperty]
    public partial string DefaultsFilesText { get; set; } = string.Empty;

    /// <summary>Gets or sets additional typed serial endpoints, one index,transport,host,port entry per line.</summary>
    [ObservableProperty]
    public partial string SerialEndpointsText { get; set; } = string.Empty;

    /// <summary>Gets or sets whether SITL persistent state is wiped at launch.</summary>
    [ObservableProperty]
    public partial bool WipeState { get; set; }

    /// <summary>Gets or sets whether the local process may display its console window.</summary>
    [ObservableProperty]
    public partial bool ShowConsoleWindow { get; set; }

    /// <summary>Gets or sets whether live MissionPlanner map integration is enabled.</summary>
    [ObservableProperty]
    public partial bool EnableMapIntegration { get; set; } = true;

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
    [NotifyPropertyChangedFor(nameof(CanInstallRelease))]
    [NotifyPropertyChangedFor(nameof(CanRemoveInstallation))]
    [NotifyCanExecuteChangedFor(nameof(CancelOperationCommand))]
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

    /// <summary>Gets whether a verified manifest release can be installed.</summary>
    public bool CanInstallRelease => !IsBusy && SelectedRelease is not null;

    /// <summary>Gets whether the selected installation is owned and removable.</summary>
    public bool CanRemoveInstallation => !IsBusy && SelectedInstallation?.Source == SitlInstallationSource.VerifiedCache;

    /// <summary>Cancels the active profile, discovery, download, or start operation.</summary>
    [RelayCommand(CanExecute = nameof(CanCancelOperation))]
    public void CancelOperation() => operationCancellation?.Cancel();

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
        if (fleetManager is not null)
        {
            fleetManager.Changed += OnFleetChanged;
            RefreshFleetSessions();
        }
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
        if (fleetManager is not null)
        {
            fleetManager.Changed -= OnFleetChanged;
        }
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
        await scenarioPresetService.InitializeAsync(cancellationToken);
        ReplaceScenarioPresets();
        await RefreshInstallationsCoreAsync(cancellationToken);
        if (sessionManager.Current.State == SimulationSessionState.Running)
        {
            await RefreshControlsCoreAsync(cancellationToken);
        }

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

    /// <summary>Refreshes configured, cached, and manifest SITL choices.</summary>
    /// <returns>A task representing discovery.</returns>
    [RelayCommand]
    public Task RefreshInstallationsAsync() => RunAsync(RefreshInstallationsCoreAsync);

    /// <summary>Downloads, verifies, and atomically installs the selected release.</summary>
    /// <returns>A task representing installation.</returns>
    [RelayCommand]
    public Task InstallReleaseAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedRelease is null)
        {
            InstallationStatus = "Select a compatible verified SITL release first.";
            return;
        }

        InstallProgress = 0;
        InstallationStatus = $"Downloading and verifying SITL {SelectedRelease.Version}.";
        var progress = new Progress<double>(value => dispatcher.Dispatch(() => InstallProgress = value));
        var installed = await installationService.InstallAsync(SelectedRelease, progress, cancellationToken);
        await RefreshInstallationsCoreAsync(cancellationToken);
        SelectedInstallation = Installations.FirstOrDefault(item => item.InstallationId == installed.InstallationId);
        UseSelectedInstallation();
        InstallationStatus = $"Verified SITL {installed.Version} is installed and selected.";
    });

    /// <summary>Pins the profile editor to the selected available installation.</summary>
    [RelayCommand]
    public void UseSelectedInstallation()
    {
        if (SelectedInstallation is not { State: SitlInstallationState.Available } installation)
        {
            InstallationStatus = "Select an available SITL installation.";
            return;
        }

        SelectedFirmwareFamily = installation.Family;
        BinaryVersion = installation.Version;
        BinaryPath = installation.ExecutablePath;
        InstallationStatus = $"Profile is pinned to {installation.DisplayName}. Save the profile to persist the pin.";
    }

    /// <summary>Removes only the selected MissionPlanner-owned cached installation.</summary>
    /// <returns>A task representing cache removal.</returns>
    [RelayCommand]
    public Task RemoveInstallationAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedInstallation is null)
        {
            return;
        }

        await installationService.RemoveAsync(SelectedInstallation, cancellationToken);
        await RefreshInstallationsCoreAsync(cancellationToken);
        InstallationStatus = "MissionPlanner-owned SITL cache entry removed. External installations were not touched.";
    });

    /// <summary>Applies the selected typed location preset to the launch-profile editor.</summary>
    [RelayCommand]
    public void ApplyLocationPreset()
    {
        if (SelectedLocationPreset is null)
        {
            return;
        }

        ApplyLocation(SelectedLocationPreset.Location);
        StatusMessage = $"Applied location preset '{SelectedLocationPreset.Name}' to the profile editor.";
    }

    /// <summary>Handles an unavoidable map integration click for launch-location selection.</summary>
    /// <param name="latitude">Selected latitude.</param>
    /// <param name="longitude">Selected longitude.</param>
    public void HandleMapLocationClick(double latitude, double longitude)
    {
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            return;
        }

        Latitude = latitude;
        Longitude = longitude;
        StatusMessage = "Map location applied to the launch-profile editor.";
    }

    /// <summary>Refreshes documented controls against the exact connected simulator.</summary>
    /// <returns>A task representing capability discovery.</returns>
    [RelayCommand]
    public Task RefreshControlsAsync() => RunAsync(RefreshControlsCoreAsync);

    /// <summary>Applies the selected environment value or bounded hazardous control.</summary>
    /// <returns>A task representing confirmed parameter write/readback.</returns>
    [RelayCommand]
    public Task ApplyControlAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedControl is null)
        {
            return;
        }

        TimeSpan? duration = SelectedControl.Descriptor.MaximumDuration is null
            ? null
            : TimeSpan.FromSeconds(FaultDurationSeconds);
        await controlService.ApplyAsync(
            SelectedControl.Descriptor.Key,
            ControlRequestedValue,
            duration,
            HazardConfirmed,
            cancellationToken);
        HazardConfirmed = false;
        await RefreshControlsCoreAsync(cancellationToken);
        StatusMessage = $"Simulation control '{SelectedControl.Descriptor.DisplayName}' applied and confirmed.";
    });

    /// <summary>Resets the selected active hazardous control.</summary>
    /// <returns>A task representing confirmed reset readback.</returns>
    [RelayCommand]
    public Task ResetControlAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedControl is null)
        {
            return;
        }

        await controlService.ResetAsync(SelectedControl.Descriptor.Key, cancellationToken);
        HazardConfirmed = false;
        await RefreshControlsCoreAsync(cancellationToken);
        StatusMessage = $"Simulation control '{SelectedControl.Descriptor.DisplayName}' reset.";
    });

    /// <summary>Saves staged location/control values as a scenario preset separate from launch profiles.</summary>
    /// <returns>A task representing preset persistence.</returns>
    [RelayCommand]
    public Task SaveScenarioPresetAsync() => RunAsync(async cancellationToken =>
    {
        StoreControlEdit(SelectedControl);
        var preset = new SimulationScenarioPreset(
            SelectedScenarioPreset?.Id ?? Guid.NewGuid(),
            ScenarioPresetName.Trim(),
            new SimulationLocation(Latitude, Longitude, Altitude, Heading),
            stagedControls.Values.OrderBy(item => item.ControlKey, StringComparer.Ordinal).ToArray());
        await scenarioPresetService.SaveAsync(preset, cancellationToken);
        ReplaceScenarioPresets(preset.Id);
        StatusMessage = $"Scenario preset '{preset.Name}' saved separately from launch profiles.";
    });

    /// <summary>Loads the selected scenario preset into the staged editor without executing faults.</summary>
    [RelayCommand]
    public void LoadScenarioPreset()
    {
        if (SelectedScenarioPreset is null)
        {
            return;
        }

        ScenarioPresetName = SelectedScenarioPreset.Name;
        if (SelectedScenarioPreset.Location is { } location)
        {
            ApplyLocation(location);
        }

        stagedControls.Clear();
        foreach (var control in SelectedScenarioPreset.Controls)
        {
            stagedControls[control.ControlKey] = control;
        }

        SelectedControl = ControlCapabilities.FirstOrDefault(item =>
            stagedControls.ContainsKey(item.Descriptor.Key)) ?? ControlCapabilities.FirstOrDefault();
        LoadControlEdit(SelectedControl);
        StatusMessage = "Scenario preset loaded into the editor; no runtime fault was executed.";
    }

    /// <summary>Deletes the selected scenario preset.</summary>
    /// <returns>A task representing preset persistence.</returns>
    [RelayCommand]
    public Task DeleteScenarioPresetAsync() => RunAsync(async cancellationToken =>
    {
        if (SelectedScenarioPreset is null)
        {
            return;
        }

        await scenarioPresetService.DeleteAsync(SelectedScenarioPreset.Id, cancellationToken);
        ReplaceScenarioPresets();
        StatusMessage = "Scenario preset deleted.";
    });

    /// <summary>Loads a declarative scenario JSON file into the editor without executing it.</summary>
    /// <returns>A task representing file selection.</returns>
    [RelayCommand]
    public Task LoadScenarioDocumentAsync() => RunAsync(async cancellationToken =>
    {
        var document = await fileHandler.LoadTextFileAsync("Select a simulation scenario JSON file", cancellationToken);
        if (document is not null)
        {
            ScenarioDocumentText = document;
            ScenarioRunnerStatus = "Scenario document loaded; use Dry run before execution.";
        }
    });

    /// <summary>Validates the scenario and exact target without changing the vehicle.</summary>
    /// <returns>A task representing dry-run capability validation.</returns>
    [RelayCommand]
    public Task DryRunScenarioAsync() => ExecuteScenarioAsync(dryRun: true);

    /// <summary>Executes the scenario against the exact running simulator vehicle.</summary>
    /// <returns>A task representing bounded execution.</returns>
    [RelayCommand]
    public Task RunScenarioAsync() => ExecuteScenarioAsync(dryRun: false);

    /// <summary>Requests a pause after the active scenario step reaches a safe boundary.</summary>
    [RelayCommand]
    public void PauseScenario() => ScenarioRunnerStatus = scenarioRunner.Pause()
        ? "Pause requested at the next safe step boundary."
        : "The scenario is not currently in a pausable step.";

    /// <summary>Resumes a scenario paused between steps.</summary>
    [RelayCommand]
    public void ResumeScenario() => ScenarioRunnerStatus = scenarioRunner.Resume()
        ? "Scenario resumed."
        : "The scenario is not paused.";

    /// <summary>Exports the last run as versioned machine-readable JSON.</summary>
    /// <returns>A task representing file export.</returns>
    [RelayCommand]
    public Task ExportScenarioJsonAsync() => ExportScenarioReportAsync(machineReadable: true);

    /// <summary>Exports the last run as a human-readable text report.</summary>
    /// <returns>A task representing file export.</returns>
    [RelayCommand]
    public Task ExportScenarioTextAsync() => ExportScenarioReportAsync(machineReadable: false);

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
        var started = await sessionManager.StartAsync(profile, cancellationToken);
        if (started.State == SimulationSessionState.Running)
        {
            await RefreshControlsCoreAsync(cancellationToken);
        }
    });

    /// <summary>Allocates and starts all requested fleet members with bounded concurrency.</summary>
    /// <returns>A task representing the fleet start operation.</returns>
    [RelayCommand]
    public Task StartAllAsync() => RunAsync(async cancellationToken =>
    {
        if (fleetManager is null)
        {
            throw new InvalidOperationException("Multi-instance simulation is unavailable.");
        }

        var profile = CreateProfile();
        var request = new SimulationFleetLaunchRequest(
            profile,
            FleetCount,
            SimulationFormationProfile.CreateLine(FleetCount, FleetSpacingMeters),
            FleetPortStride,
            FleetMaximumConcurrency);
        var report = await fleetManager.StartAllAsync(request, cancellationToken);
        RefreshFleetSessions();
        FleetStatus = report.Succeeded
            ? $"All {report.Results.Count} simulator sessions are running."
            : $"{report.Results.Count(result => result.Succeeded)} of {report.Results.Count} simulator sessions started; inspect per-session failures.";
    });

    /// <summary>Stops all exact fleet members with bounded concurrency.</summary>
    /// <returns>A task representing the fleet stop operation.</returns>
    [RelayCommand]
    public Task StopAllAsync() => RunAsync(async cancellationToken =>
    {
        if (fleetManager is null)
        {
            return;
        }

        var report = await fleetManager.StopAllAsync(FleetMaximumConcurrency, cancellationToken);
        RefreshFleetSessions();
        FleetStatus = report.Succeeded
            ? "All simulator fleet sessions stopped."
            : $"{report.Results.Count(result => !result.Succeeded)} simulator session(s) did not stop cleanly.";
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
        scenarioRunner.Changed -= OnScenarioRunnerChanged;
        if (fleetManager is not null)
        {
            fleetManager.Changed -= OnFleetChanged;
        }
        operationCancellation?.Cancel();
    }

    partial void OnSelectedProfileChanged(SimulatorProfile? value)
    {
        if (value is not null)
        {
            LoadProfile(value);
        }

        OnPropertyChanged(nameof(CanRestart));
    }

    partial void OnSelectedFirmwareFamilyChanged(FirmwareFamily value)
    {
        RefreshFrames();
        if (initialized && active)
        {
            _ = RefreshReleasesAsync();
        }
    }

    partial void OnSelectedReleaseChannelChanged(FirmwareReleaseChannel value)
    {
        if (initialized && active)
        {
            _ = RefreshReleasesAsync();
        }
    }

    partial void OnSelectedControlChanging(
        SimulationControlCapability? oldValue,
        SimulationControlCapability? newValue) => StoreControlEdit(oldValue);

    partial void OnSelectedControlChanged(SimulationControlCapability? value) => LoadControlEdit(value);

    partial void OnSelectedFleetSessionChanged(SimulationFleetSessionSnapshot? value)
    {
        if (refreshingFleetSelection || value is null || fleetManager is null)
        {
            return;
        }

        fleetManager.Select(value.Allocation.FleetSessionId);
        ApplySnapshot(value.Session);
    }

    partial void OnControlRequestedValueChanged(double value) => StoreControlEdit(SelectedControl);

    partial void OnFaultDurationSecondsChanged(double value) => StoreControlEdit(SelectedControl);

    partial void OnBinaryPathChanged(string value)
    {
        if (SelectedInstallation is { } installation &&
            !string.Equals(
                installation.ExecutablePath,
                value,
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            SelectedInstallation = null;
        }
    }

    private async Task RunAsync(Func<CancellationToken, Task> operation)
    {
        if (!await operationGate.WaitAsync(0))
        {
            return;
        }

        IsBusy = true;
        using var cancellation = new CancellationTokenSource();
        operationCancellation = cancellation;
        try
        {
            await operation(cancellation.Token);
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
            if (ReferenceEquals(operationCancellation, cancellation))
            {
                operationCancellation = null;
            }

            IsBusy = false;
            operationGate.Release();
        }
    }

    private void OnSessionChanged(object? sender, SimulationSessionChangedEventArgs e) =>
        dispatcher.Dispatch(() => ApplySnapshot(e.Snapshot));

    private void OnFleetChanged(object? sender, SimulationFleetChangedEventArgs e) =>
        dispatcher.Dispatch(() =>
        {
            RefreshFleetSessions();
            if (e.Session.IsSelected)
            {
                ApplySnapshot(e.Session.Session);
            }
        });

    private void RefreshFleetSessions()
    {
        if (fleetManager is null)
        {
            return;
        }

        refreshingFleetSelection = true;
        try
        {
            FleetSessions.Clear();
            foreach (var session in fleetManager.Sessions)
            {
                FleetSessions.Add(session);
            }

            SelectedFleetSession = FleetSessions.FirstOrDefault(session => session.IsSelected);
        }
        finally
        {
            refreshingFleetSelection = false;
        }
    }

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
            new SimulatorBinaryReference(
                BinaryVersion.Trim(),
                BinaryPath.Trim(),
                SelectedInstallation?.Source.ToString() ?? "external",
                SelectedInstallation?.InstallationId),
            arguments,
            environment,
            new ArduPilotLaunchSettings(
                InstanceNumber,
                SystemId,
                DefaultsFilesText.Split(
                    ['\r', '\n'],
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                WipeState,
                ShowConsoleWindow,
                EnableMapIntegration,
                ParseSerialEndpoints()));
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
        var launchSettings = profile.EffectiveLaunchSettings;
        InstanceNumber = launchSettings.Instance;
        SystemId = launchSettings.SystemId;
        DefaultsFilesText = string.Join(Environment.NewLine, launchSettings.DefaultsFiles);
        SerialEndpointsText = string.Join(
            Environment.NewLine,
            launchSettings.EffectiveSerialEndpoints.Select(item =>
                $"{item.Index},{item.Transport},{item.Host},{item.Port.ToString(CultureInfo.InvariantCulture)}"));
        WipeState = launchSettings.WipeState;
        ShowConsoleWindow = launchSettings.ShowConsoleWindow;
        EnableMapIntegration = launchSettings.EnableMapIntegration;
        SelectedInstallation = Installations.FirstOrDefault(item =>
            item.InstallationId.Equals(profile.Binary.InstallationId, StringComparison.Ordinal));
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

    private async Task RefreshInstallationsCoreAsync(CancellationToken cancellationToken)
    {
        var installations = await installationService.DiscoverAsync(cancellationToken);
        Installations.Clear();
        foreach (var installation in installations)
        {
            Installations.Add(installation);
        }

        SelectedInstallation = SelectedProfile?.Binary.InstallationId is { } id
            ? Installations.FirstOrDefault(item => item.InstallationId == id)
            : Installations.FirstOrDefault(item =>
                SelectedProfile is not null &&
                item.Family == SelectedProfile.FirmwareFamily &&
                item.Version.Equals(SelectedProfile.Binary.Version, StringComparison.OrdinalIgnoreCase));
        await RefreshReleasesCoreAsync(cancellationToken);
        InstallationStatus = Installations.Count == 0
            ? "No configured or verified cached SITL installation was found. Configure an official manifest or external installation."
            : $"Found {Installations.Count} SITL installation(s).";
    }

    private Task RefreshReleasesAsync() => RunAsync(RefreshReleasesCoreAsync);

    private bool CanCancelOperation() => IsBusy;

    private async Task RefreshReleasesCoreAsync(CancellationToken cancellationToken)
    {
        var releases = await installationService.GetReleasesAsync(
            SelectedFirmwareFamily,
            SelectedReleaseChannel,
            cancellationToken);
        AvailableReleases.Clear();
        foreach (var release in releases)
        {
            AvailableReleases.Add(release);
        }

        SelectedRelease = AvailableReleases.FirstOrDefault();
    }

    private async Task RefreshControlsCoreAsync(CancellationToken cancellationToken)
    {
        var selectedKey = SelectedControl?.Descriptor.Key;
        var capabilities = await controlService.DiscoverAsync(cancellationToken);
        ControlCapabilities.Clear();
        foreach (var capability in capabilities)
        {
            ControlCapabilities.Add(capability);
        }

        SelectedControl = ControlCapabilities.FirstOrDefault(item => item.Descriptor.Key == selectedKey) ??
            ControlCapabilities.FirstOrDefault(item => item.IsAvailable) ??
            ControlCapabilities.FirstOrDefault();
        ScenarioEvents.Clear();
        foreach (var item in controlService.Events)
        {
            ScenarioEvents.Add(item);
        }
    }

    private Task ExecuteScenarioAsync(bool dryRun) => RunAsync(async cancellationToken =>
    {
        var snapshot = SelectedFleetSession is { Session.State: SimulationSessionState.Running, Session.VehicleId: not null }
            ? SelectedFleetSession.Session
            : sessionManager.Current;
        if (snapshot.State != SimulationSessionState.Running || snapshot.VehicleId is null)
        {
            throw new InvalidOperationException("Start and connect an exact simulator vehicle before running a scenario.");
        }

        var document = scenarioParser.Parse(ScenarioDocumentText);
        LastScenarioReport = await scenarioRunner.RunAsync(
            new SimulationScenarioRunRequest(
                document,
                snapshot.SessionId,
                snapshot.VehicleId.Value,
                dryRun,
                ScenarioHazardsConfirmed),
            cancellationToken);
        ScenarioHazardsConfirmed = false;
        ScenarioRunnerStatus = LastScenarioReport.Summary;
        StatusMessage = LastScenarioReport.Summary;
    });

    private Task ExportScenarioReportAsync(bool machineReadable) => RunAsync(async cancellationToken =>
    {
        if (LastScenarioReport is null)
        {
            throw new InvalidOperationException("Run or dry-run a scenario before exporting its report.");
        }

        var extension = machineReadable ? "json" : "txt";
        var content = machineReadable
            ? scenarioReportExporter.ToJson(LastScenarioReport)
            : scenarioReportExporter.ToText(LastScenarioReport);
        var path = await fileHandler.SaveTextFileAsync(
            $"simulation-scenario-{LastScenarioReport.RunId:N}.{extension}",
            content,
            cancellationToken);
        StatusMessage = path is null ? "Scenario report export cancelled." : $"Scenario report exported to {path}.";
    });

    private void OnScenarioRunnerChanged(object? sender, SimulationScenarioRunnerChangedEventArgs args)
    {
        dispatcher.Dispatch(() => ScenarioRunnerStatus = args.Snapshot.Message);
    }

    private void RefreshFrames()
    {
        var current = FrameModel;
        AvailableFrames.Clear();
        foreach (var frame in frameCatalog.GetFrames(SelectedFirmwareFamily))
        {
            AvailableFrames.Add(frame);
        }

        if (!AvailableFrames.Contains(current, StringComparer.OrdinalIgnoreCase))
        {
            FrameModel = AvailableFrames.FirstOrDefault() ?? string.Empty;
        }
    }

    private IReadOnlyList<ArduPilotSerialEndpoint> ParseSerialEndpoints()
    {
        var result = new List<ArduPilotSerialEndpoint>();
        foreach (var line in SerialEndpointsText.Split(
                     ['\r', '\n'],
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var fields = line.Split(',', StringSplitOptions.TrimEntries);
            if (fields.Length != 4 ||
                !int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) ||
                !Enum.TryParse<ArduPilotSerialTransport>(fields[1], ignoreCase: true, out var transport) ||
                !int.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var port))
            {
                throw new InvalidOperationException(
                    $"Serial endpoint '{line}' must use index,UdpClient|TcpClient,host,port format.");
            }

            result.Add(new ArduPilotSerialEndpoint(index, transport, fields[2], port));
        }

        return result;
    }

    private void ApplyLocation(SimulationLocation location)
    {
        Latitude = location.LatitudeDegrees;
        Longitude = location.LongitudeDegrees;
        Altitude = location.AltitudeMeters;
        Heading = location.HeadingDegrees;
    }

    private void StoreControlEdit(SimulationControlCapability? capability)
    {
        if (capability is null || !double.IsFinite(ControlRequestedValue))
        {
            return;
        }

        stagedControls[capability.Descriptor.Key] = new SimulationPresetControlValue(
            capability.Descriptor.Key,
            ControlRequestedValue,
            capability.Descriptor.MaximumDuration is null || !double.IsFinite(FaultDurationSeconds) || FaultDurationSeconds <= 0
                ? null
                : TimeSpan.FromSeconds(FaultDurationSeconds));
    }

    private void LoadControlEdit(SimulationControlCapability? capability)
    {
        if (capability is null)
        {
            return;
        }

        if (stagedControls.TryGetValue(capability.Descriptor.Key, out var staged))
        {
            ControlRequestedValue = staged.Value;
            FaultDurationSeconds = staged.Duration?.TotalSeconds ?? 10;
            return;
        }

        ControlRequestedValue = capability.CurrentValue ?? capability.Descriptor.Minimum;
        FaultDurationSeconds = Math.Min(10, capability.Descriptor.MaximumDuration?.TotalSeconds ?? 10);
    }

    private void ReplaceScenarioPresets(Guid? selectedId = null)
    {
        ScenarioPresets.Clear();
        foreach (var preset in scenarioPresetService.Presets)
        {
            ScenarioPresets.Add(preset);
        }

        SelectedScenarioPreset = selectedId is null
            ? ScenarioPresets.FirstOrDefault()
            : ScenarioPresets.FirstOrDefault(item => item.Id == selectedId) ?? ScenarioPresets.FirstOrDefault();
    }

    private const string ExampleScenarioJson = """
        {
          "schemaVersion": 1,
          "id": "6d9f5f05-2906-4c36-af7c-0a8d6abf4d40",
          "name": "Connected simulator check",
          "variables": {},
          "steps": [
            {
              "id": "online",
              "kind": "waitForState",
              "name": "Wait for exact simulator",
              "timeoutSeconds": 10,
              "state": "online"
            },
            {
              "id": "disarmed",
              "kind": "assertTelemetry",
              "name": "Confirm safe disarmed state",
              "timeoutSeconds": 5,
              "condition": {
                "metric": "armed",
                "operator": "equal",
                "expected": { "kind": "boolean", "booleanValue": false }
              }
            }
          ]
        }
        """;
}
