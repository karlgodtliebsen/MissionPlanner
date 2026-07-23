using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Configuration;
using MissionPlanner.App.Presentation;
using MissionPlanner.Core.ConfigTuning.Planner;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>Edits versioned local MissionPlanner preferences without changing vehicle parameters.</summary>
public sealed partial class PlannerTabViewModel : ObservableObject
{
    private readonly IPlannerSettingsService settingsService;
    private readonly PlannerSettingsRuntime runtime;
    private readonly ParametersFileHandler fileHandler;
    private readonly IUserConfirmationService confirmation;
    private readonly ILogger<PlannerTabViewModel> logger;
    private readonly SemaphoreSlim operationGate = new(1, 1);
    private bool loading;

    /// <summary>Initializes the Planner preferences page.</summary>
    /// <param name="settingsService">The versioned settings service.</param>
    /// <param name="runtime">The live settings bridge.</param>
    /// <param name="fileHandler">The platform file helper.</param>
    /// <param name="confirmation">The confirmation service.</param>
    /// <param name="logger">The logger.</param>
    public PlannerTabViewModel(
        IPlannerSettingsService settingsService,
        PlannerSettingsRuntime runtime,
        ParametersFileHandler fileHandler,
        IUserConfirmationService confirmation,
        ILogger<PlannerTabViewModel> logger)
    {
        this.settingsService = settingsService;
        this.runtime = runtime;
        this.fileHandler = fileHandler;
        this.confirmation = confirmation;
        this.logger = logger;
    }

    /// <summary>Gets available unit systems.</summary>
    public IReadOnlyList<UnitSystem> UnitSystems { get; } = Enum.GetValues<UnitSystem>();

    /// <summary>Gets available map providers.</summary>
    public IReadOnlyList<PlannerMapProvider> MapProviders { get; } = Enum.GetValues<PlannerMapProvider>();

    /// <summary>Gets available map styles.</summary>
    public IReadOnlyList<PlannerMapStyle> MapStyles { get; } = Enum.GetValues<PlannerMapStyle>();

    /// <summary>Gets available application themes.</summary>
    public IReadOnlyList<PlannerTheme> Themes { get; } = Enum.GetValues<PlannerTheme>();

    /// <summary>Gets available logging levels.</summary>
    public IReadOnlyList<PlannerLogLevel> LoggingLevels { get; } = Enum.GetValues<PlannerLogLevel>();

    /// <summary>Gets available connection channels.</summary>
    public IReadOnlyList<string> ConnectionChannels { get; } = ["AUTO", "TCP", "UDP", "UDPCI", "WS"];

    /// <summary>Gets available parameter-cache policies.</summary>
    public IReadOnlyList<ParameterCachePolicy> ParameterCachePolicies { get; } = Enum.GetValues<ParameterCachePolicy>();

    /// <summary>Gets available update channels.</summary>
    public IReadOnlyList<string> UpdateChannels { get; } = ["Stable", "Beta", "Development"];

    /// <summary>Gets the selected unit system.</summary>
    [ObservableProperty]
    public partial UnitSystem SelectedUnitSystem { get; set; }

    /// <summary>Gets the selected map provider.</summary>
    [ObservableProperty]
    public partial PlannerMapProvider SelectedMapProvider { get; set; }

    /// <summary>Gets the selected map style.</summary>
    [ObservableProperty]
    public partial PlannerMapStyle SelectedMapStyle { get; set; }

    /// <summary>Gets the default map zoom level.</summary>
    [ObservableProperty]
    public partial double DefaultMapZoom { get; set; }

    /// <summary>Gets the telemetry display rate in hertz.</summary>
    [ObservableProperty]
    public partial int TelemetryDisplayRateHz { get; set; }

    /// <summary>Gets the telemetry chart history in seconds.</summary>
    [ObservableProperty]
    public partial int ChartHistorySeconds { get; set; }

    /// <summary>Gets the selected application theme.</summary>
    [ObservableProperty]
    public partial PlannerTheme SelectedTheme { get; set; }

    /// <summary>Gets the selected logging level.</summary>
    [ObservableProperty]
    public partial PlannerLogLevel SelectedLoggingLevel { get; set; }

    /// <summary>Gets the log retention period in days.</summary>
    [ObservableProperty]
    public partial int LogRetentionDays { get; set; }

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
    public Task ActivateAsync() => RunAsync(async cancellationToken =>
    {
        var result = await settingsService.InitializeAsync(cancellationToken);
        Load(result.Settings);
        StatusMessage = result.Message ?? "Planner preferences loaded. These settings are local and do not change the flight controller.";
    });

    partial void OnSelectedThemeChanged(PlannerTheme value)
    {
        if (!loading)
        {
            runtime.PreviewTheme(value);
        }
    }

    [RelayCommand]
    private Task SaveAsync() => RunAsync(async cancellationToken =>
    {
        var result = await settingsService.SaveAsync(CreateSettings(), cancellationToken);
        ShowSaveResult(result, "Planner preferences saved.");
    });

    [RelayCommand]
    private Task ResetSectionAsync(string sectionName) => RunAsync(async cancellationToken =>
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

    [RelayCommand]
    private Task ResetAllAsync() => RunAsync(async cancellationToken =>
    {
        if (!await confirmation.ConfirmAsync(
                "Reset Planner preferences?",
                "All local application preferences will return to safe defaults. Vehicle parameters are not affected.",
                "Reset all",
                cancellationToken))
        {
            return;
        }

        var result = await settingsService.ResetAllAsync(cancellationToken);
        Load(settingsService.Current);
        ShowSaveResult(result, "All Planner preferences reset to defaults.");
    });

    [RelayCommand]
    private Task ExportAsync() => RunAsync(async cancellationToken =>
    {
        var path = await fileHandler.SaveTextFileAsync(
            "missionplanner-settings.json",
            settingsService.Export(),
            cancellationToken);
        StatusMessage = path is null ? "Settings export cancelled." : $"Settings exported to {path}. Secrets are never included.";
    });

    [RelayCommand]
    private Task ImportAsync() => RunAsync(async cancellationToken =>
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
            SelectedUnitSystem = settings.Units.System;
            SelectedMapProvider = settings.Map.Provider;
            SelectedMapStyle = settings.Map.Style;
            DefaultMapZoom = settings.Map.DefaultZoom;
            TelemetryDisplayRateHz = settings.Telemetry.DisplayRateHz;
            ChartHistorySeconds = settings.Telemetry.ChartHistorySeconds;
            SelectedTheme = settings.Appearance.Theme;
            SelectedLoggingLevel = settings.Logging.Level;
            LogRetentionDays = settings.Logging.RetentionDays;
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
        }
        finally
        {
            loading = false;
        }

        runtime.PreviewTheme(settings.Appearance.Theme);
    }

    private PlannerSettings CreateSettings() => new()
    {
        Units = new PlannerUnitSettings { System = SelectedUnitSystem },
        Map = new PlannerMapSettings
        {
            Provider = SelectedMapProvider,
            Style = SelectedMapStyle,
            DefaultZoom = DefaultMapZoom
        },
        Telemetry = new PlannerTelemetrySettings
        {
            DisplayRateHz = TelemetryDisplayRateHz,
            ChartHistorySeconds = ChartHistorySeconds
        },
        Appearance = new PlannerAppearanceSettings { Theme = SelectedTheme },
        Logging = new PlannerLoggingSettings { Level = SelectedLoggingLevel, RetentionDays = LogRetentionDays },
        Connection = new PlannerConnectionSettings
        {
            Channel = ConnectionChannel,
            Host = ConnectionHost,
            Port = ConnectionPort,
            BaudRate = ConnectionBaudRate
        },
        ParameterCache = new PlannerParameterCacheSettings
        {
            Policy = SelectedParameterCachePolicy,
            MaximumAgeMinutes = ParameterCacheMaximumAgeMinutes
        },
        Confirmations = new PlannerConfirmationSettings
        {
            ConfirmParameterWrites = ConfirmParameterWrites,
            ConfirmArmDisarm = ConfirmArmDisarm,
            ConfirmFirmwareChanges = ConfirmFirmwareChanges
        },
        Updates = new PlannerUpdateSettings
        {
            CheckAutomatically = CheckUpdatesAutomatically,
            CheckIntervalDays = UpdateCheckIntervalDays,
            Channel = UpdateChannel
        },
        Accessibility = new PlannerAccessibilitySettings
        {
            HighContrastTelemetry = HighContrastTelemetry,
            ReduceMotion = ReduceMotion,
            TextScale = TextScale,
            AnnounceTelemetryWarnings = AnnounceTelemetryWarnings
        }
    };

    private void ShowSaveResult(PlannerSettingsSaveResult result, string successMessage)
    {
        RestartRequiredMessage = FormatRestart(result.RestartRequiredSections);
        StatusMessage = result.Success
            ? successMessage
            : string.Join(" ", result.Errors.Select(error => error.Message));
    }

    private static string? FormatRestart(IReadOnlyList<PlannerSettingsSection> sections) => sections.Count == 0
        ? null
        : $"Restart required for: {string.Join(", ", sections)}.";
}
