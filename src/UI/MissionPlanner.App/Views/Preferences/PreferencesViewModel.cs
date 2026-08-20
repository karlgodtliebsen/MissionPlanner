using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Services;
using MissionPlanner.App.Theming;
using MissionPlanner.App.Views.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Maps.Catalog;
using MissionPlanner.Maps.Credentials;
using MissionPlanner.Maps.Http;
using MissionPlanner.Maps.Offline;
using MissionPlanner.Maps.Settings;

namespace MissionPlanner.App.Views.Preferences;

/// <summary>
/// Edits versioned local MissionPlanner preferences without changing vehicle parameters.
/// </summary>
public sealed partial class PreferencesViewModel : ObservableObject, IDisposable
{
    private readonly IPlannerSettingsService settingsService;
    private readonly IThemeManager themeManager;
    private readonly ParametersFileHandler fileHandler;
    private readonly IUserConfirmationService confirmation;
    private readonly ILogger<PreferencesViewModel> logger;
    private readonly IMapSecretStore mapSecretStore;
    private readonly IOfflineMapPackRepository offlinePacks;
    private readonly IOfflineMapPackManager offlinePackManager;
    private readonly IOfflineMapPackValidator offlinePackValidator;
    private readonly MapHttpDiskCache mapCache;
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private bool loading;
    private string? selectedOfflineSourceId;

    /// <summary>
    /// Initializes the Planner preferences page.
    /// </summary>
    /// <param name="settingsService">The versioned settings service.</param>
    /// <param name="themeManager">The application theme manager.</param>
    /// <param name="fileHandler">The platform file helper.</param>
    /// <param name="confirmation">The confirmation service.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="mapSecretStore">The secure map credential store.</param>
    /// <param name="offlinePacks">The installed offline-pack repository.</param>
    /// <param name="offlinePackManager">The active-source-aware offline-pack manager.</param>
    /// <param name="offlinePackValidator">The offline-pack validator.</param>
    /// <param name="mapCache">The bounded map HTTP cache.</param>
    public PreferencesViewModel(
        IPlannerSettingsService settingsService,
        IThemeManager themeManager,
        ParametersFileHandler fileHandler,
        IUserConfirmationService confirmation,
        ILogger<PreferencesViewModel> logger,
        IMapSecretStore mapSecretStore,
        IOfflineMapPackRepository offlinePacks,
        IOfflineMapPackManager offlinePackManager,
        IOfflineMapPackValidator offlinePackValidator,
        MapHttpDiskCache mapCache)
    {
        this.settingsService = settingsService;
        this.themeManager = themeManager;
        this.fileHandler = fileHandler;
        this.confirmation = confirmation;
        this.logger = logger;
        this.mapSecretStore = mapSecretStore;
        this.offlinePacks = offlinePacks;
        this.offlinePackManager = offlinePackManager;
        this.offlinePackValidator = offlinePackValidator;
        this.mapCache = mapCache;
    }

    /// <summary>
    /// Gets available unit systems.
    /// </summary>
    public IReadOnlyList<UnitSystem> UnitSystems { get; } = Enum.GetValues<UnitSystem>();

    /// <summary>Gets selectable built-in sources grouped for the settings UI.</summary>
    public IReadOnlyList<MapSettingsSourceItem> MapSources { get; private set; } = [];

    /// <summary>Gets selectable offline pack sources.</summary>
    public IEnumerable<MapSettingsSourceItem> OfflineMapSources => MapSources.Where(value => value.Group == MapSettingsSourceGroup.OfflinePacks);

    /// <summary>Gets selectable self-hosted and custom sources.</summary>
    public IEnumerable<MapSettingsSourceItem> CustomMapSources => MapSources.Where(value => value.Group == MapSettingsSourceGroup.SelfHostedOrCustom);

    /// <summary>Gets selectable online-provider sources.</summary>
    public IEnumerable<MapSettingsSourceItem> OnlineMapSources => MapSources.Where(value => value.Group == MapSettingsSourceGroup.OnlineProviders);

    /// <summary>Gets selectable blank-map sources.</summary>
    public IEnumerable<MapSettingsSourceItem> BlankMapSources => MapSources.Where(value => value.Group == MapSettingsSourceGroup.BlankMap);

    /// <summary>Gets installed offline packs shown by the pack manager.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<InstalledOfflineMapPack> InstalledMapPacks { get; private set; } = [];

    /// <summary>Gets or sets the pack selected in the pack manager.</summary>
    [ObservableProperty]
    public partial InstalledOfflineMapPack? SelectedMapPack { get; set; }

    /// <summary>Gets the current HTTP-cache size in megabytes.</summary>
    [ObservableProperty]
    public partial double MapHttpCacheSizeMiB { get; private set; }

    /// <summary>Gets available application themes.</summary>
    public IReadOnlyList<ThemeOption> Themes => themeManager.AvailableThemes;

    /// <summary>Gets available logging levels.</summary>
    public IReadOnlyList<PlannerLogLevel> LoggingLevels { get; } = Enum.GetValues<PlannerLogLevel>();

    /// <summary>Gets available connection channels.</summary>
    public IReadOnlyList<string> ConnectionChannels { get; } = ["AUTO", "TCP", "UDP", "UDPCI", "WS"];

    /// <summary>Gets available parameter-cache policies.</summary>
    public IReadOnlyList<ParameterCachePolicy> ParameterCachePolicies { get; } = Enum.GetValues<ParameterCachePolicy>();

    /// <summary>Gets available update channels.</summary>
    public IReadOnlyList<string> UpdateChannels { get; } = ["Stable", "Beta", "Development"];

    /// <summary>
    /// 
    /// </summary>
    public IReadOnlyList<string> DistanceUnits { get; } = ["Meters", "Feet"];

    /// <summary>
    /// 
    /// </summary>
    public IReadOnlyList<string> AltitudeUnits { get; } = ["Meters", "Feet"];

    /// <summary>
    /// 
    /// </summary>
    public IReadOnlyList<string> SpeedUnits { get; } = ["MetersPerSecond", "KilometersPerHour", "MilesPerHour", "Knots"];

    /// <summary>
    /// 
    /// </summary>
    public IReadOnlyList<string> SpeechSeverities { get; } = ["Emergency", "Alert", "Critical", "Error", "Warning", "Notice", "Info", "Debug"];

    /// <summary>
    /// 
    /// </summary>
    public IReadOnlyList<string> MapAccessModes { get; } = ["ServerOnly", "ServerAndCache", "CacheOnly"];

    /// <summary>
    /// 
    /// </summary>
    public IReadOnlyList<string> LayoutModes { get; } = ["Basic", "Advanced", "Custom"];

    /// <summary>
    /// Gets or sets a value indicating whether the flyout menu is visible at startup.
    /// </summary>
    [ObservableProperty]
    public partial bool IsFlyoutVisibleAtStartup { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the flyout menu is visible at startup.
    /// </summary>
    [ObservableProperty]
    public partial bool IsTutorialVisibleAtStartup { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the flyout menu is locked in the UI.
    /// </summary>
    [ObservableProperty]
    public partial bool IsFlyoutLocked { get; set; }


    /// <summary>Gets the selected unit system.</summary>
    [ObservableProperty]
    public partial UnitSystem SelectedUnitSystem { get; set; }

    /// <summary>Gets the default map zoom level.</summary>
    [ObservableProperty]
    public partial double DefaultMapZoom { get; set; }

    /// <summary>Gets or sets the selected stable map source.</summary>
    [ObservableProperty]
    public partial MapSettingsSourceItem? SelectedMapSource { get; set; }

    /// <summary>Gets or sets whether the bounded HTTP cache is enabled.</summary>
    [ObservableProperty]
    public partial bool MapHttpCacheEnabled { get; set; }

    /// <summary>Gets or sets the HTTP cache disk limit in mebibytes.</summary>
    [ObservableProperty]
    public partial int MapHttpCacheLimitMiB { get; set; }

    /// <summary>Gets or sets a transient credential entry; it is cleared immediately after use.</summary>
    [ObservableProperty]
    public partial string MapCredentialInput { get; set; } = string.Empty;

    /// <summary>Gets the telemetry display rate in hertz.</summary>
    [ObservableProperty]
    public partial int TelemetryDisplayRateHz { get; set; }

    /// <summary>Gets the telemetry chart history in seconds.</summary>
    [ObservableProperty]
    public partial int ChartHistorySeconds { get; set; }

    /// <summary>Gets the selected application theme.</summary>
    [ObservableProperty]
    public partial ThemeOption? SelectedTheme { get; set; }

    /// <summary>Gets the selected logging level.</summary>
    [ObservableProperty]
    public partial PlannerLogLevel SelectedLoggingLevel { get; set; }

    /// <summary>Gets the log retention period in days.</summary>
    [ObservableProperty]
    public partial int LogRetentionDays { get; set; }

    [ObservableProperty] public partial string LogDirectory { get; set; } = string.Empty;

    /// <summary>Gets the default connection channel.</summary>
    [ObservableProperty]
    public partial string ConnectionChannel { get; set; } = "AUTO";

    /// <summary>Gets the default connection host.</summary>
    [ObservableProperty]
    public partial string ConnectionHost { get; set; } = string.Empty;

    /// <summary>Gets the default connection port.</summary>
    [ObservableProperty]
    public partial int ConnectionPort { get; set; }

    /// <summary>Gets the default serial baud rate.</summary>
    [ObservableProperty]
    public partial int ConnectionBaudRate { get; set; }

    /// <summary>Gets the selected parameter-cache policy.</summary>
    [ObservableProperty]
    public partial ParameterCachePolicy SelectedParameterCachePolicy { get; set; }

    /// <summary>Gets the maximum accepted parameter-cache age in minutes.</summary>
    [ObservableProperty]
    public partial int ParameterCacheMaximumAgeMinutes { get; set; }

    /// <summary>Gets whether vehicle parameter writes require confirmation.</summary>
    [ObservableProperty]
    public partial bool ConfirmParameterWrites { get; set; }

    /// <summary>Gets whether arm and disarm operations require confirmation.</summary>
    [ObservableProperty]
    public partial bool ConfirmArmDisarm { get; set; }

    /// <summary>Gets whether firmware changes require confirmation.</summary>
    [ObservableProperty]
    public partial bool ConfirmFirmwareChanges { get; set; }

    /// <summary>Gets whether update checks run automatically.</summary>
    [ObservableProperty]
    public partial bool CheckUpdatesAutomatically { get; set; }

    /// <summary>Gets the update-check interval in days.</summary>
    [ObservableProperty]
    public partial int UpdateCheckIntervalDays { get; set; }

    /// <summary>Gets the selected update channel.</summary>
    [ObservableProperty]
    public partial string UpdateChannel { get; set; } = "Stable";

    /// <summary>Gets whether high-contrast telemetry presentation is requested.</summary>
    [ObservableProperty]
    public partial bool HighContrastTelemetry { get; set; }

    /// <summary>Gets whether nonessential telemetry animation is reduced.</summary>
    [ObservableProperty]
    public partial bool ReduceMotion { get; set; }

    /// <summary>Gets the UI text scale multiplier.</summary>
    [ObservableProperty]
    public partial double TextScale { get; set; }

    /// <summary>Gets whether important telemetry warnings should be announced.</summary>
    [ObservableProperty]
    public partial bool AnnounceTelemetryWarnings { get; set; }

    [ObservableProperty] public partial string DistanceUnit { get; set; } = "Meters";
    [ObservableProperty] public partial string LayoutMode { get; set; } = "Advanced";
    [ObservableProperty] public partial string AltitudeUnit { get; set; } = "Meters";
    [ObservableProperty] public partial string SpeedUnit { get; set; } = "MetersPerSecond";
    [ObservableProperty] public partial bool SpeechEnabled { get; set; }
    [ObservableProperty] public partial string SpeechSeverity { get; set; } = "Warning";
    [ObservableProperty] public partial int AttitudeRateHz { get; set; }
    [ObservableProperty] public partial int PositionRateHz { get; set; }
    [ObservableProperty] public partial int StatusRateHz { get; set; }
    [ObservableProperty] public partial int RcRateHz { get; set; }
    [ObservableProperty] public partial int SensorRateHz { get; set; }
    [ObservableProperty] public partial bool ResetOnUsbConnect { get; set; }
    [ObservableProperty] public partial bool DisableEsp32RtsReset { get; set; }
    [ObservableProperty] public partial int TrackLength { get; set; }
    [ObservableProperty] public partial bool ShowDistanceToHome { get; set; }
    [ObservableProperty] public partial bool LoadWaypointsOnConnect { get; set; }
    [ObservableProperty] public partial bool RotateMapToHeading { get; set; }
    [ObservableProperty] public partial int GcsSystemId { get; set; }
    [ObservableProperty] public partial bool DisplayCourseOverGround { get; set; }
    [ObservableProperty] public partial bool DisplayHeading { get; set; }
    [ObservableProperty] public partial bool DisplayNavigationBearing { get; set; }
    [ObservableProperty] public partial bool DisplayTurnRadius { get; set; }
    [ObservableProperty] public partial bool DisplayTarget { get; set; }
    [ObservableProperty] public partial bool DisplayAircraftToolTip { get; set; }
    [ObservableProperty] public partial int AircraftLineLength { get; set; }
    [ObservableProperty] public partial bool ShowAirports { get; set; }
    [ObservableProperty] public partial bool ShowAdsb { get; set; }
    [ObservableProperty] public partial bool ShowNoFlyZones { get; set; }
    [ObservableProperty] public partial bool ShowTemporaryFlightRestrictions { get; set; }
    [ObservableProperty] public partial bool DownloadParametersInBackground { get; set; }
    [ObservableProperty] public partial bool NoRcReceiver { get; set; }
    [ObservableProperty] public partial bool SlowComputerMode { get; set; }
    [ObservableProperty] public partial string MapAccessMode { get; set; } = "ServerAndCache";

    /// <summary>Gets whether an operation is running.</summary>
    [ObservableProperty]
    public partial bool IsBusy { get; private set; }

    /// <summary>Gets the latest operation or validation status.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasStatusMessage))]
    public partial string? StatusMessage { get; private set; }

    /// <summary>Gets whether a status message is available.</summary>
    public bool HasStatusMessage => !string.IsNullOrWhiteSpace(StatusMessage);

    /// <summary>Gets settings that require an application restart.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RestartRequired))]
    public partial string? RestartRequiredMessage { get; private set; }

    /// <summary>Gets whether a restart is required for one or more saved settings.</summary>
    public bool RestartRequired => !string.IsNullOrWhiteSpace(RestartRequiredMessage);

    /// <summary>Loads persisted settings and performs safe recovery when necessary.</summary>
    public Task ActivateAsync()
    {
        return RunAsync(async cancellationToken =>
        {
            var result = await settingsService.InitializeAsync(cancellationToken);
            await LoadMapSourcesAsync(result.Settings.Map.SelectedSourceId, cancellationToken);
            await RefreshMapPacksAsync(cancellationToken);
            RestoreOfflinePackSelection(result.Settings.Map.SelectedSourceId);
            RefreshMapCacheSize();
            Load(result.Settings);
            StatusMessage = result.Message ?? "Planner preferences loaded. These settings are local and do not change the flight controller.";
        });
    }

    [RelayCommand]
    private void SelectMapSource(MapSettingsSourceItem source)
    {
        selectedOfflineSourceId = null;
        SelectedMapSource = source;
    }

    [RelayCommand]
    private Task SaveMapCredentialAsync()
    {
        return RunAsync(async cancellationToken =>
        {
            if (SelectedMapSource is null || SelectedMapSource.Source.CredentialRequirement == MapCredentialRequirement.None)
            {
                StatusMessage = "The selected source does not require a credential.";
                return;
            }

            if (string.IsNullOrWhiteSpace(MapCredentialInput))
            {
                StatusMessage = "Enter a credential before saving.";
                return;
            }

            await mapSecretStore.SetAsync($"maps.credentials.{SelectedMapSource.Id}", MapCredentialInput, cancellationToken);
            MapCredentialInput = string.Empty;
            await LoadMapSourcesAsync(SelectedMapSource.Id, cancellationToken);
            StatusMessage = "Map credential saved securely. The stored value cannot be displayed.";
        });
    }

    [RelayCommand]
    private Task RemoveMapCredentialAsync()
    {
        return RunAsync(async cancellationToken =>
        {
            if (SelectedMapSource is null)
            {
                return;
            }

            await mapSecretStore.RemoveAsync($"maps.credentials.{SelectedMapSource.Id}", cancellationToken);
            MapCredentialInput = string.Empty;
            await LoadMapSourcesAsync(SelectedMapSource.Id, cancellationToken);
            StatusMessage = "Map credential removed.";
        });
    }

    [RelayCommand]
    private Task TestMapCredentialAsync()
    {
        return RunAsync(async cancellationToken =>
        {
            if (SelectedMapSource is null)
            {
                return;
            }

            var configured = !string.IsNullOrEmpty(await mapSecretStore.GetAsync($"maps.credentials.{SelectedMapSource.Id}", cancellationToken));
            StatusMessage = configured
                ? "A credential is configured. Network validation occurs when the provider is first requested."
                : "No credential is configured for this source.";
        });
    }

    [RelayCommand]
    private async Task ImportMapPackAsync()
    {
        await RunAsync(async cancellationToken =>
        {
            var manifestFile = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Select offline map pack manifest" });
            if (manifestFile is null)
            {
                return;
            }

            var archiveFile = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Select the manifest's MBTiles archive" });
            if (archiveFile is null)
            {
                return;
            }

            await using var manifestStream = await manifestFile.OpenReadAsync();
            using var reader = new StreamReader(manifestStream);
            var manifest = OfflineMapPackJson.Deserialize(await reader.ReadToEndAsync(cancellationToken));
            await using var archive = await archiveFile.OpenReadAsync();
            await offlinePackManager.InstallAsync(manifest, archive, cancellationToken);
            await RefreshMapPacksAsync(cancellationToken);
            StatusMessage = $"Offline pack '{manifest.DisplayName}' installed and verified.";
        });
    }

    [RelayCommand]
    private Task VerifyMapPackAsync()
    {
        return RunAsync(async cancellationToken =>
        {
            if (SelectedMapPack is null)
            {
                return;
            }

            await offlinePackValidator.ValidateAsync(SelectedMapPack.Manifest, SelectedMapPack.ArchivePath, cancellationToken);
            StatusMessage = $"Offline pack '{SelectedMapPack.Manifest.DisplayName}' verified.";
        });
    }

    [RelayCommand]
    private Task RemoveMapPackAsync()
    {
        return RunAsync(async cancellationToken =>
        {
            if (SelectedMapPack is null)
            {
                return;
            }

            await offlinePackManager.RemoveAsync(SelectedMapPack.Manifest.Id, SelectedMapPack.Manifest.Version, cancellationToken);
            await RefreshMapPacksAsync(cancellationToken);
            StatusMessage = "Offline pack removed.";
        });
    }

    [RelayCommand]
    private void SelectMapPack()
    {
        if (SelectedMapPack is null)
        {
            return;
        }

        selectedOfflineSourceId = $"pack:{SelectedMapPack.Manifest.Id}:{SelectedMapPack.Manifest.Version}";
        StatusMessage = $"Offline pack '{SelectedMapPack.Manifest.DisplayName}' selected. Save preferences to make it the active source.";
    }

    [RelayCommand]
    private void ClearSelectedMapCache()
    {
        mapCache.ClearSource(SelectedMapSource?.Id ?? "osm-standard");
        RefreshMapCacheSize();
        StatusMessage = "Selected source HTTP cache cleared. Offline packs were not changed.";
    }

    [RelayCommand]
    private void ClearAllMapCache()
    {
        mapCache.ClearAll();
        RefreshMapCacheSize();
        StatusMessage = "All HTTP cache entries cleared. Offline packs were not changed.";
    }

    private async Task RefreshMapPacksAsync(CancellationToken cancellationToken)
    {
        InstalledMapPacks = await offlinePacks.ListAsync(cancellationToken);
        SelectedMapPack = InstalledMapPacks.FirstOrDefault();
    }

    private void RefreshMapCacheSize()
    {
        MapHttpCacheSizeMiB = mapCache.SizeBytes / 1_048_576d;
    }

    private void RestoreOfflinePackSelection(string sourceId)
    {
        var parts = sourceId.Split(':');
        if (parts.Length != 3 || parts[0] != "pack")
        {
            return;
        }

        SelectedMapPack = InstalledMapPacks.FirstOrDefault(value => value.Manifest.Id == parts[1] && value.Manifest.Version == parts[2]);
        selectedOfflineSourceId = SelectedMapPack is null ? null : sourceId;
    }

    private async Task LoadMapSourcesAsync(string selectedSourceId, CancellationToken cancellationToken)
    {
        var catalog = BuiltInMapCatalog.Load();
        var configured = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in catalog.Sources.Where(value => value.CredentialRequirement != MapCredentialRequirement.None))
        {
            if (!string.IsNullOrEmpty(await mapSecretStore.GetAsync($"maps.credentials.{source.Id}", cancellationToken)))
            {
                configured.Add(source.Id);
            }
        }

        MapSources = MapSettingsSourceCatalog.Create(catalog, configured);
        OnPropertyChanged(nameof(MapSources));
        OnPropertyChanged(nameof(OfflineMapSources));
        OnPropertyChanged(nameof(CustomMapSources));
        OnPropertyChanged(nameof(OnlineMapSources));
        OnPropertyChanged(nameof(BlankMapSources));
        SelectedMapSource = MapSettingsSourceCatalog.Resolve(MapSources, selectedSourceId, true);
    }

    partial void OnSelectedThemeChanged(ThemeOption? value)
    {
        if (!loading && value is not null)
        {
            _ = themeManager.PreviewAsync(value.Id);
        }
    }

    [RelayCommand]
    private Task SaveAsync()
    {
        return RunAsync(async cancellationToken =>
        {
            var result = await settingsService.SaveAsync(CreateSettings(), cancellationToken);
            ShowSaveResult(result, "Planner preferences saved.");
        });
    }

    [RelayCommand]
    private Task ResetApplicationAsync(string sectionName)
    {
        return RunAsync(async cancellationToken =>
        {
            settingsService.Current.Appearance = new PlannerAppearanceSettings();
            var result = await settingsService.SaveAsync(settingsService.Current, cancellationToken);
            Load(settingsService.Current);
            ShowSaveResult(result, $"Appearance settings reset to defaults.");
        });
    }

    [RelayCommand]
    private Task ResetSectionAsync(string sectionName)
    {
        return RunAsync(async cancellationToken =>
        {
            if (!Enum.TryParse<PlannerSettingsSection>(sectionName, true, out var section))
            {
                StatusMessage = $"Unknown settings section: {sectionName}.";
                return;
            }

            var result = await settingsService.ResetSectionAsync(section, cancellationToken);
            Load(settingsService.Current);
            ShowSaveResult(result, $"{section} settings reset to defaults.");
        });
    }

    [RelayCommand]
    private Task ResetAllAsync()
    {
        return RunAsync(async cancellationToken =>
        {
            if (!await confirmation.ConfirmAsync("Reset Planner preferences?", "All local application preferences will return to safe defaults. Vehicle parameters are not affected.",
                    "Reset all", cancellationToken))
            {
                return;
            }

            var result = await settingsService.ResetAllAsync(cancellationToken);
            Load(settingsService.Current);
            ShowSaveResult(result, "All Planner preferences reset to defaults.");
        });
    }

    [RelayCommand]
    private Task ExportAsync()
    {
        return RunAsync(async cancellationToken =>
        {
            var path = await fileHandler.SaveTextFileAsync("missionplanner-settings.json", settingsService.Export(), cancellationToken);
            StatusMessage = path is null ? "Settings export cancelled." : $"Settings exported to {path}. Secrets are never included.";
        });
    }

    [RelayCommand]
    private Task ImportAsync()
    {
        return RunAsync(async cancellationToken =>
        {
            var document = await fileHandler.LoadTextFileAsync("Select MissionPlanner settings", cancellationToken);
            if (document is null)
            {
                StatusMessage = "Settings import cancelled.";
                return;
            }

            var result = await settingsService.ImportAsync(document, cancellationToken);
            if (!result.Success)
            {
                StatusMessage = string.Join(" ", result.Errors.Select(error => error.Message));
                return;
            }

            Load(settingsService.Current);
            RestartRequiredMessage = FormatRestart(result.RestartRequiredSections);
            StatusMessage = result.WasMigrated
                ? $"Settings imported and migrated to schema {PlannerSettings.CurrentSchemaVersion}."
                : "Settings imported. Secrets were ignored and remain in secure storage.";
        });
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
            StatusMessage = "Planner settings operation cancelled.";
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Planner settings operation failed.");
            StatusMessage = $"Planner settings operation failed: {exception.Message}";
        }
        finally
        {
            IsBusy = false;
            operationGate.Release();
        }
    }

    private void Load(PlannerSettings settings)
    {
        loading = true;
        try
        {
            IsFlyoutLocked = settings.Appearance.IsFlyoutLocked;
            IsFlyoutVisibleAtStartup = settings.Appearance.IsFlyoutVisibleAtStartup;
            IsTutorialVisibleAtStartup = settings.Appearance.IsTutorialVisibleAtStartup;

            SelectedUnitSystem = settings.Units.System;
            DefaultMapZoom = settings.Map.DefaultZoom;
            SelectedMapSource ??= MapSettingsSourceCatalog.Resolve(MapSources, settings.Map.SelectedSourceId, true);
            MapHttpCacheEnabled = settings.Map.HttpCacheEnabled;
            MapHttpCacheLimitMiB = checked((int)(settings.Map.HttpCacheLimitBytes / 1_048_576));
            TelemetryDisplayRateHz = settings.Telemetry.DisplayRateHz;
            ChartHistorySeconds = settings.Telemetry.ChartHistorySeconds;
            SelectedTheme = Themes.FirstOrDefault(theme =>
                string.Equals(theme.Id, settings.Appearance.ThemeId, StringComparison.Ordinal))
                ?? Themes.First(theme => theme.Id == ThemeIds.System);
            SelectedLoggingLevel = settings.Logging.Level;
            LogRetentionDays = settings.Logging.RetentionDays;
            LogDirectory = settings.Logging.LogDirectory;
            ConnectionChannel = settings.Connection.Channel;
            ConnectionHost = settings.Connection.Host;
            ConnectionPort = settings.Connection.Port;
            ConnectionBaudRate = settings.Connection.BaudRate;
            SelectedParameterCachePolicy = settings.ParameterCache.Policy;
            ParameterCacheMaximumAgeMinutes = settings.ParameterCache.MaximumAgeMinutes;
            ConfirmParameterWrites = settings.Confirmations.ConfirmParameterWrites;
            ConfirmArmDisarm = settings.Confirmations.ConfirmArmDisarm;
            ConfirmFirmwareChanges = settings.Confirmations.ConfirmFirmwareChanges;
            CheckUpdatesAutomatically = settings.Updates.CheckAutomatically;
            UpdateCheckIntervalDays = settings.Updates.CheckIntervalDays;
            UpdateChannel = settings.Updates.Channel;
            HighContrastTelemetry = settings.Accessibility.HighContrastTelemetry;
            ReduceMotion = settings.Accessibility.ReduceMotion;
            TextScale = settings.Accessibility.TextScale;
            AnnounceTelemetryWarnings = settings.Accessibility.AnnounceTelemetryWarnings;
            DistanceUnit = settings.Legacy.DistanceUnit;
            LayoutMode = settings.Legacy.LayoutMode;
            AltitudeUnit = settings.Legacy.AltitudeUnit;
            SpeedUnit = settings.Legacy.SpeedUnit;
            SpeechEnabled = settings.Legacy.SpeechEnabled;
            SpeechSeverity = settings.Legacy.SpeechSeverity;
            AttitudeRateHz = settings.Legacy.AttitudeRateHz;
            PositionRateHz = settings.Legacy.PositionRateHz;
            StatusRateHz = settings.Legacy.StatusRateHz;
            RcRateHz = settings.Legacy.RcRateHz;
            SensorRateHz = settings.Legacy.SensorRateHz;
            ResetOnUsbConnect = settings.Legacy.ResetOnUsbConnect;
            DisableEsp32RtsReset = settings.Legacy.DisableEsp32RtsReset;
            TrackLength = settings.Legacy.TrackLength;
            ShowDistanceToHome = settings.Legacy.ShowDistanceToHome;
            LoadWaypointsOnConnect = settings.Legacy.LoadWaypointsOnConnect;
            RotateMapToHeading = settings.Legacy.RotateMapToHeading;
            GcsSystemId = settings.Legacy.GcsSystemId;
            DisplayCourseOverGround = settings.Legacy.DisplayCourseOverGround;
            DisplayHeading = settings.Legacy.DisplayHeading;
            DisplayNavigationBearing = settings.Legacy.DisplayNavigationBearing;
            DisplayTurnRadius = settings.Legacy.DisplayTurnRadius;
            DisplayTarget = settings.Legacy.DisplayTarget;
            DisplayAircraftToolTip = settings.Legacy.DisplayAircraftToolTip;
            AircraftLineLength = settings.Legacy.AircraftLineLength;
            ShowAirports = settings.Legacy.ShowAirports;
            ShowAdsb = settings.Legacy.ShowAdsb;
            ShowNoFlyZones = settings.Legacy.ShowNoFlyZones;
            ShowTemporaryFlightRestrictions = settings.Legacy.ShowTemporaryFlightRestrictions;
            DownloadParametersInBackground = settings.Legacy.DownloadParametersInBackground;
            NoRcReceiver = settings.Legacy.NoRcReceiver;
            SlowComputerMode = settings.Legacy.SlowComputerMode;
            MapAccessMode = settings.Legacy.MapAccessMode;
        }
        finally
        {
            loading = false;
        }

        _ = themeManager.PreviewAsync(settings.Appearance.ThemeId);
    }

    private PlannerSettings CreateSettings()
    {
        return new PlannerSettings
        {
            Units = new PlannerUnitSettings { System = SelectedUnitSystem },
            Map = new PlannerMapSettings { DefaultZoom = DefaultMapZoom, SelectedSourceId = selectedOfflineSourceId ?? SelectedMapSource?.Id ?? "osm-standard", HttpCacheEnabled = MapHttpCacheEnabled, HttpCacheLimitBytes = Math.Max(16, MapHttpCacheLimitMiB) * 1_048_576L },
            Telemetry = new PlannerTelemetrySettings { DisplayRateHz = TelemetryDisplayRateHz, ChartHistorySeconds = ChartHistorySeconds },
            Appearance = new PlannerAppearanceSettings
            {
                ThemeId = SelectedTheme?.Id ?? ThemeIds.System,
                IsFlyoutVisibleAtStartup = IsFlyoutVisibleAtStartup,
                IsTutorialVisibleAtStartup = IsTutorialVisibleAtStartup,
                IsFlyoutLocked = IsFlyoutLocked
            },
            Logging = new PlannerLoggingSettings { Level = SelectedLoggingLevel, RetentionDays = LogRetentionDays, LogDirectory = LogDirectory },
            Connection = new PlannerConnectionSettings { Channel = ConnectionChannel, Host = ConnectionHost, Port = ConnectionPort, BaudRate = ConnectionBaudRate },
            ParameterCache = new PlannerParameterCacheSettings { Policy = SelectedParameterCachePolicy, MaximumAgeMinutes = ParameterCacheMaximumAgeMinutes },
            Confirmations = new PlannerConfirmationSettings { ConfirmParameterWrites = ConfirmParameterWrites, ConfirmArmDisarm = ConfirmArmDisarm, ConfirmFirmwareChanges = ConfirmFirmwareChanges },
            Updates = new PlannerUpdateSettings { CheckAutomatically = CheckUpdatesAutomatically, CheckIntervalDays = UpdateCheckIntervalDays, Channel = UpdateChannel },
            Accessibility = new PlannerAccessibilitySettings { HighContrastTelemetry = HighContrastTelemetry, ReduceMotion = ReduceMotion, TextScale = TextScale, AnnounceTelemetryWarnings = AnnounceTelemetryWarnings },
            Legacy = new PlannerLegacySettings
            {
                LayoutMode = LayoutMode,
                DistanceUnit = DistanceUnit,
                AltitudeUnit = AltitudeUnit,
                SpeedUnit = SpeedUnit,
                SpeechEnabled = SpeechEnabled,
                SpeechSeverity = SpeechSeverity,
                AttitudeRateHz = AttitudeRateHz,
                PositionRateHz = PositionRateHz,
                StatusRateHz = StatusRateHz,
                RcRateHz = RcRateHz,
                SensorRateHz = SensorRateHz,
                ResetOnUsbConnect = ResetOnUsbConnect,
                DisableEsp32RtsReset = DisableEsp32RtsReset,
                TrackLength = TrackLength,
                ShowDistanceToHome = ShowDistanceToHome,
                LoadWaypointsOnConnect = LoadWaypointsOnConnect,
                RotateMapToHeading = RotateMapToHeading,
                GcsSystemId = (byte)Math.Clamp(GcsSystemId, byte.MinValue, byte.MaxValue),
                DisplayCourseOverGround = DisplayCourseOverGround,
                DisplayHeading = DisplayHeading,
                DisplayNavigationBearing = DisplayNavigationBearing,
                DisplayTurnRadius = DisplayTurnRadius,
                DisplayTarget = DisplayTarget,
                DisplayAircraftToolTip = DisplayAircraftToolTip,
                AircraftLineLength = AircraftLineLength,
                ShowAirports = ShowAirports,
                ShowAdsb = ShowAdsb,
                ShowNoFlyZones = ShowNoFlyZones,
                ShowTemporaryFlightRestrictions = ShowTemporaryFlightRestrictions,
                DownloadParametersInBackground = DownloadParametersInBackground,
                NoRcReceiver = NoRcReceiver,
                SlowComputerMode = SlowComputerMode,
                MapAccessMode = MapAccessMode
            }
        };
    }

    private void ShowSaveResult(PlannerSettingsSaveResult result, string successMessage)
    {
        RestartRequiredMessage = FormatRestart(result.RestartRequiredSections);
        StatusMessage = result.Success ? successMessage : string.Join(" ", result.Errors.Select(error => error.Message));
    }

    private static string? FormatRestart(IReadOnlyList<PlannerSettingsSection> sections)
    {
        return sections.Count == 0 ? null : $"Restart required for: {string.Join(", ", sections)}.";
    }

    /// <inheritdoc />
    public void Dispose()
    {
        //settingsService.Dispose()
    }
}
