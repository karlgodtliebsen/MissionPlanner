using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Models;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.Definitions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.App.Views.Navigation;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Presents metadata-backed frame choices and confirmed, recoverable writes.</summary>
public sealed partial class FrameSetupViewModel : ViewModelBase
{
    private readonly INavigationService navigation;

    private readonly IActiveVehicleContext activeVehicle;
    private readonly IFrameConfigurationService frameService;
    private readonly IVehicleParameterRegistry parameterRegistry;
    private readonly ISetupCompletionStore completionStore;
    private readonly ISetupWorkflowCatalog workflowCatalog;
    private readonly IUserConfirmationService confirmation;
    private readonly IDateTimeProvider clock;
    private readonly IDomainEventHub domainEventHub;
    private readonly IVehicleParameterLoadStatusContext parameterLoadStatus;
    private readonly IDialogService dialogService;
    private IDisposable? progressDialog;
    private bool showingProgressDialog;
    private int progressDialogGeneration;
    private string progressMessage = string.Empty;
    private IDisposable? parameterLoadSubscription;
    private CancellationTokenSource? operationCancellation;

    /// <summary>Initializes the frame setup workflow.</summary>
    /// <param name="activeVehicle">The active vehicle boundary.</param>
    /// <param name="frameService">The guarded frame-configuration service.</param>
    /// <param name="parameterRegistry">The live parameter registry.</param>
    /// <param name="completionStore">The setup evidence store.</param>
    /// <param name="workflowCatalog">The setup workflow catalog.</param>
    /// <param name="confirmation">The shared confirmation service.</param>
    /// <param name="clock">The application clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="dispatcher">The UI dispatcher.</param>
    /// <param name="domainEventHub">The parameter load event source.</param>
    /// <param name="parameterLoadStatus">The latest parameter download status.</param>
    /// <param name="dialogService">The shared progress dialog service.</param>
    /// <param name="navigation">The application navigation service.</param>
    public FrameSetupViewModel(
        IActiveVehicleContext activeVehicle,
        IFrameConfigurationService frameService,
        IVehicleParameterRegistry parameterRegistry,
        ISetupCompletionStore completionStore,
        ISetupWorkflowCatalog workflowCatalog,
        IUserConfirmationService confirmation,
        IDateTimeProvider clock, ILogger<FrameSetupViewModel> logger,
        IUiDispatcher dispatcher, IDomainEventHub domainEventHub,
        IVehicleParameterLoadStatusContext parameterLoadStatus, IDialogService dialogService, INavigationService navigation)
        : base(logger, dispatcher, domainEventHub)
    {
        this.navigation = navigation;
        this.activeVehicle = activeVehicle;
        this.frameService = frameService;
        this.parameterRegistry = parameterRegistry;
        this.completionStore = completionStore;
        this.workflowCatalog = workflowCatalog;
        this.confirmation = confirmation;
        this.clock = clock;
        this.domainEventHub = domainEventHub;
        this.parameterLoadStatus = parameterLoadStatus;
        this.dialogService = dialogService;
    }

    /// <summary>Gets frame parameters supported by both live values and firmware metadata.</summary>
    public ObservableCollection<FrameParameterSettingViewModel> Settings
    {
        get;
    } = [];

    /// <summary>Gets optional initial-setup recommendations that require explicit selection.</summary>
    public ObservableCollection<FrameRecommendationViewModel> Recommendations
    {
        get;
    } = [];

    /// <summary>Gets the reported firmware family.</summary>
    [ObservableProperty]
    public partial string FirmwareFamily
    {
        get;
        private set;
    } = "Unknown";


    /// <summary>Gets whether a confirmed change requires a vehicle reboot.</summary>
    [ObservableProperty]
    public partial bool RebootRequired
    {
        get;
        private set;
    }

    /// <summary>Gets whether at least one frame setting is supported.</summary>
    public bool HasSettings => Settings.Count > 0;

    /// <summary>Gets whether any optional recommendations are available for review.</summary>
    public bool HasRecommendations => Recommendations.Count > 0;

    /// <summary>Loads choices for the current active vehicle.</summary>
    /// <returns>A task that completes when current values and metadata are merged.</returns>
    public async Task LoadAsync()
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            CloseProgressDialog();
            SetMessages("Connect a vehicle before loading frame configuration.");
            return;
        }

        if (parameterLoadStatus.Get(vehicleId) is { IsInProgress: true } status)
        {
            await ApplyParameterLoadStatusAsync(status);
            return;
        }

        var token = StartOperation();
        try
        {
            var configuration = await frameService.GetConfigurationAsync(vehicleId, token);
            Dispatcher.Dispatch(() =>
            {
                if (!token.IsCancellationRequested && activeVehicle.IsOnline && activeVehicle.VehicleId == vehicleId)
                {
                    ShowConfiguration(configuration);
                }
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Loading frame configuration failed for {VehicleId}.", vehicleId);
            SetMessages(exception);
        }
    }

    /// <inheritdoc />
    public void Cancel()
    {
        operationCancellation?.Cancel();
        operationCancellation?.Dispose();
        operationCancellation = null;
    }

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        if (parameterLoadSubscription is not null)
        {
            return;
        }

        SetMessages("Load the connected vehicle's supported frame choices.");
        parameterLoadSubscription = domainEventHub.SubscribeDomainEventAsync<VehicleParameterLoadStatusChanged>(OnParameterLoadStatusChanged);
        activeVehicle.Changed += OnActiveVehicleChanged;
        await base.ActivateAsync();
        await LoadAsync();
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        parameterLoadSubscription?.Dispose();
        parameterLoadSubscription = null;
        CloseProgressDialog();
        Cancel();
        activeVehicle.Changed -= OnActiveVehicleChanged;
        return base.DeactivateAsync();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        DeactivateAsync().GetAwaiter().GetResult();
        base.Dispose();
    }


    /// <summary>Opens the shared Full Parameters workspace.</summary>
    [RelayCommand]
    private Task OpenFullParametersAsync()
    {
        return navigation.NavigateAsync(MissionPlannerRoutes.ConfigFullParameters);
    }

    [RelayCommand]
    private Task LoadCommandAsync()
    {
        return LoadAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            SetMessages("Reconnect the vehicle before refreshing frame values.");
            return;
        }

        var token = StartOperation();
        try
        {
            await frameService.RefreshAsync(vehicleId, token);
            var configuration = await frameService.GetConfigurationAsync(vehicleId, token);
            Dispatcher.Dispatch(() => ShowConfiguration(configuration));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Refreshing frame configuration failed for {VehicleId}.", vehicleId);
            SetMessages(exception);
        }
    }

    [RelayCommand]
    private async Task ApplyAsync()
    {
        if (activeVehicle.VehicleId is not { } vehicleId || activeVehicle.State is not { } state || !activeVehicle.IsOnline)
        {
            SetMessages("Connect a vehicle before applying frame configuration.");
            return;
        }

        var changes = Settings.Where(item => item.HasChange).Select(item => item.ToChange())
            .Concat(Recommendations.Where(item => item.IsSelected).Select(item => item.ToChange()))
            .ToArray();
        if (changes.Length == 0)
        {
            SetMessages("No reviewed changes are pending.");
            return;
        }

        var summary = string.Join(Environment.NewLine, changes.Select(change =>
            $"{change.Name}: {change.OriginalValue} → {change.PendingValue}"));
        var accepted = await confirmation.ConfirmAsync(
            "Apply frame configuration",
            $"Review these vehicle changes before writing:{Environment.NewLine}{summary}",
            "Write and verify");
        if (!accepted)
        {
            return;
        }

        var token = StartOperation();
        Progress = 0.1;
        try
        {
            var result = await frameService.ApplyAsync(vehicleId, changes, token);
            RebootRequired = result.Succeeded && result.RequiresReboot;
            Progress = 1;
            if (result.Succeeded && result.Status == FrameConfigurationApplyStatus.Succeeded &&
                activeVehicle.IsOnline && activeVehicle.State is { } currentState && currentState.VehicleId == state.VehicleId)
            {
                completionStore.Save(workflowCatalog.CreateEvidence(
                    SetupWorkflowKey.Frame,
                    currentState,
                    parameterRegistry.GetAllParameters(vehicleId),
                    clock.UtcNow));
                Logger.LogInformation("Recorded confirmed frame setup evidence for {VehicleId}.", vehicleId);
            }

            if (activeVehicle.IsOnline && activeVehicle.VehicleId == vehicleId)
            {
                var configuration = await frameService.GetConfigurationAsync(vehicleId, token);
                Dispatcher.Dispatch(() => ShowConfiguration(configuration, true));
            }
        }
        catch (OperationCanceledException)
        {
            SetMessages("Frame configuration was cancelled. Refresh vehicle values before continuing.");
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Applying frame configuration failed for {VehicleId}.", vehicleId);
            SetMessages(exception);
        }
    }

    private CancellationToken StartOperation()
    {
        Cancel();
        operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken);
        Progress = 0;
        return operationCancellation.Token;
    }

    private void OnActiveVehicleChanged(ActiveVehicleChangedEventArgs args)
    {
        if (SetupVehicleChange.IsConnectionOrIdentityBoundary(args))
        {
            Dispatcher.DispatchAsync(async () =>
            {
                if (parameterLoadSubscription is null)
                {
                    return;
                }
                Cancel();
                CloseProgressDialog();
                await LoadAsync();
            });
        }
    }

    private Task OnParameterLoadStatusChanged(VehicleParameterLoadStatusChanged evt, CancellationToken cancellationToken)
    {
        return Dispatcher.DispatchAsync(async () =>
        {
            if (parameterLoadSubscription is not null && !cancellationToken.IsCancellationRequested &&
                activeVehicle.IsOnline && activeVehicle.VehicleId == evt.Status.VehicleId &&
                parameterLoadStatus.Get(evt.Status.VehicleId) == evt.Status)
            {
                await ApplyParameterLoadStatusAsync(evt.Status);
            }
        });
    }

    private async Task ApplyParameterLoadStatusAsync(ParameterLoadStatus status)
    {
        progressMessage = status.Message;
        SetMessages(status.Message);
        if (status.IsInProgress)
        {
            Cancel();
            await ShowProgressDialogAsync();
            return;
        }

        var generation = progressDialogGeneration;
        try
        {
            if (status.State == ParameterLoadState.Completed)
            {
                progressMessage = "Loading frame configuration...";
                await LoadAsync();
            }
            else
            {
                SetMessages(errorMessage: status.Message);
            }
        }
        finally
        {
            if (generation == progressDialogGeneration)
            {
                CloseProgressDialog();
            }
        }
    }

    private async Task ShowProgressDialogAsync()
    {
        var token = activeVehicle.ConnectionCancellationToken;
        if (showingProgressDialog || parameterLoadSubscription is null || token.IsCancellationRequested)
        {
            return;
        }

        var generation = ++progressDialogGeneration;
        showingProgressDialog = true;
        try
        {
            var dialog = await dialogService.DisplayProgressCancellableAsync(
                () => progressMessage, new DialogOptions { Title = "Loading parameters" }, token);
            if (generation != progressDialogGeneration || parameterLoadSubscription is null || token.IsCancellationRequested)
            {
                dialog.Dispose();
                if (generation == progressDialogGeneration)
                {
                    CloseProgressDialog();
                }
                return;
            }
            progressDialog = dialog;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (generation == progressDialogGeneration)
            {
                CloseProgressDialog();
            }
        }
        catch (Exception exception)
        {
            if (generation == progressDialogGeneration)
            {
                CloseProgressDialog();
                SetMessages(exception);
            }
            Logger.LogError(exception, "Showing frame parameter loading progress failed.");
        }
    }

    private void CloseProgressDialog()
    {
        ++progressDialogGeneration;
        showingProgressDialog = false;
        progressDialog?.Dispose();
        progressDialog = null;
    }

    private void ShowConfiguration(FrameConfigurationSnapshot configuration, bool preserveStatus = false)
    {
        Settings.Clear();
        foreach (var setting in configuration.Settings)
        {
            Settings.Add(new FrameParameterSettingViewModel(setting));
        }

        Recommendations.Clear();
        foreach (var recommendation in configuration.Recommendations)
        {
            Recommendations.Add(new FrameRecommendationViewModel(recommendation));
        }

        FirmwareFamily = configuration.Family.ToString();
        if (!preserveStatus)
        {
            SetMessages(Settings.Count == 0
                ? "This firmware exposes no metadata-backed frame choices for its live parameters."
                : "Select desired values, review pending changes, then write and verify.");
        }

        OnPropertyChanged(nameof(HasSettings));
        OnPropertyChanged(nameof(HasRecommendations));
    }
}

/// <summary>Presents current and pending values for one frame parameter.</summary>
public sealed partial class FrameParameterSettingViewModel : ObservableObject
{
    /// <summary>Initializes a frame parameter row.</summary>
    /// <param name="setting">The metadata-backed setting.</param>
    public FrameParameterSettingViewModel(FrameParameterSetting setting)
    {
        Setting = setting;
        SelectedOption = setting.Options.FirstOrDefault(option => Math.Abs(option.Value - setting.CurrentValue) <= 0.0001f);
    }

    /// <summary>Gets the underlying setting.</summary>
    public FrameParameterSetting Setting
    {
        get;
    }

    /// <summary>Gets the parameter name.</summary>
    public string Name => Setting.Name;

    /// <summary>Gets the user-facing name.</summary>
    public string DisplayName => Setting.DisplayName;

    /// <summary>Gets metadata-supported values.</summary>
    public IReadOnlyList<FrameParameterOption> Options => Setting.Options;

    /// <summary>Gets a label for the current vehicle value.</summary>
    public string CurrentDisplay => OptionLabel(Setting.CurrentValue);

    /// <summary>Gets whether firmware metadata requires reboot after this change.</summary>
    public bool RebootRequired => Setting.RebootRequired;

    /// <summary>Gets or sets the explicitly selected pending value.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PendingDisplay))]
    [NotifyPropertyChangedFor(nameof(HasChange))]
    public partial FrameParameterOption? SelectedOption
    {
        get;
        set;
    }

    /// <summary>Gets a label for the pending value.</summary>
    public string PendingDisplay => SelectedOption is null ? "Select a supported value" : $"{SelectedOption.Label} ({SelectedOption.Value})";

    /// <summary>Gets whether the pending selection differs from live readback.</summary>
    public bool HasChange => SelectedOption is { } option && Math.Abs(option.Value - Setting.CurrentValue) > 0.0001f;

    /// <summary>Creates the reviewed change represented by this row.</summary>
    /// <returns>The reviewed parameter change.</returns>
    public FrameParameterChange ToChange()
    {
        return new FrameParameterChange(Name, Setting.CurrentValue, SelectedOption!.Value, Setting.ParameterType);
    }

    private string OptionLabel(float value)
    {
        return Options.FirstOrDefault(option => Math.Abs(option.Value - value) <= 0.0001f) is { } option
            ? $"{option.Label} ({value})"
            : $"Unknown firmware value ({value})";
    }
}

/// <summary>Presents an optional initial parameter recommendation.</summary>
public sealed partial class FrameRecommendationViewModel : ObservableObject
{
    /// <summary>Initializes a recommendation row.</summary>
    /// <param name="recommendation">The recommendation.</param>
    public FrameRecommendationViewModel(FrameInitialParameterRecommendation recommendation)
    {
        Recommendation = recommendation;
    }

    /// <summary>Gets the underlying recommendation.</summary>
    public FrameInitialParameterRecommendation Recommendation
    {
        get;
    }

    /// <summary>Gets the parameter name.</summary>
    public string Name => Recommendation.Name;

    /// <summary>Gets the review text.</summary>
    public string Description => $"{Recommendation.DisplayName}: {Recommendation.CurrentValue} → {Recommendation.RecommendedValue}. {Recommendation.Reason}";

    /// <summary>Gets or sets whether the user explicitly approved this recommendation.</summary>
    [ObservableProperty]
    public partial bool IsSelected
    {
        get;
        set;
    }

    /// <summary>Creates the explicitly selected change.</summary>
    /// <returns>The reviewed parameter change.</returns>
    public FrameParameterChange ToChange()
    {
        return new FrameParameterChange(
            Recommendation.Name,
            Recommendation.CurrentValue,
            Recommendation.RecommendedValue,
            Recommendation.ParameterType);
    }
}

