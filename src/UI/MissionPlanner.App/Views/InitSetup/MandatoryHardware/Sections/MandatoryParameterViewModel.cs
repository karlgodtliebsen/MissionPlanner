using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using UraniumUI.Extensions;
using SetupWorkflowDetailViewModel = MissionPlanner.App.Views.InitSetup.MandatoryHardware.Models.SetupWorkflowDetailViewModel;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Provides lifecycle-safe presentation for metadata-backed mandatory parameter pages.</summary>
public abstract partial class MandatoryParameterViewModel : SetupWorkflowDetailViewModel
{
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IDispatcher dispatcher;
    private CancellationTokenSource? operationCancellation;
    private readonly ILogger<MandatoryParameterViewModel> logger;

    /// <summary>Initializes a metadata-backed mandatory workflow.</summary>
    protected MandatoryParameterViewModel(SetupWorkflowDescriptor descriptor, IActiveVehicleContext activeVehicle, IDispatcher dispatcher, ILogger<MandatoryParameterViewModel> logger)
        : base(descriptor, logger)
    {
        this.activeVehicle = activeVehicle;
        this.dispatcher = dispatcher;
        this.logger = logger;
    }

    /// <summary>Gets the supported settings.</summary>
    public ObservableRangeCollection<PeripheralSettingViewModel> Settings
    {
        get;
    } = [];

    /// <summary>Gets workflow guidance.</summary>
    public ObservableRangeCollection<string> Guidance
    {
        get;
    } = [];


    /// <summary>Gets whether supported settings were found.</summary>
    public bool HasSettings => Settings.Count > 0;


    /// <summary>Loads the parameter configuration from the workflow service.</summary>
    public async Task LoadAsync()
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            ShowDisconnected();
            return;
        }

        var token = StartOperation();
        IsBusy = true;
        try
        {
            var configuration = await LoadConfigurationAsync(vehicleId, token);
            dispatcher.Dispatch(() => Show(configuration));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Loading mandatory parameter configuration failed for {VehicleId}.", vehicleId);
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <inheritdoc />
    public override void Cancel()
    {
        operationCancellation?.Cancel();
        operationCancellation?.Dispose();
        operationCancellation = null;
    }

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        StatusMessage = "Connect a vehicle to load settings.";
        activeVehicle.Changed += OnActiveVehicleChanged;
        base.ActivateAsync();
        LoadAsync().GetAwaiter().GetResult();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        activeVehicle.Changed -= OnActiveVehicleChanged;
        Cancel();
        return base.DeactivateAsync();
    }


    /// <summary>Loads configuration for the concrete workflow.</summary>
    protected abstract Task<MandatoryParameterConfiguration> LoadConfigurationAsync(MissionPlanner.Shared.Models.Vehicles.Models.VehicleId vehicleId,
        CancellationToken cancellationToken);

    /// <summary>Applies one setting for the concrete workflow.</summary>
    protected abstract Task<MandatoryParameterApplyResult> ApplySettingAsync(MissionPlanner.Shared.Models.Vehicles.Models.VehicleId vehicleId,
        string name, double value, CancellationToken cancellationToken);

    [RelayCommand]
    private Task RefreshAsync()
    {
        return LoadAsync();
    }

    private async Task Apply((string Name, double Value) change)
    {
        if (activeVehicle.VehicleId is not { } vehicleId || !activeVehicle.IsOnline)
        {
            ShowDisconnected();
            return;
        }

        var token = StartOperation();
        IsBusy = true;
        try
        {
            var result = await ApplySettingAsync(vehicleId, change.Name, change.Value, token);
            StatusMessage = result.Message;
            if (result.Success)
            {
                await LoadAsync();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            ErrorMessage = exception.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private CancellationToken StartOperation()
    {
        Cancel();
        operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken);
        ErrorMessage = null;
        StatusMessage = null;
        return operationCancellation.Token;
    }

    private void Show(MandatoryParameterConfiguration configuration)
    {
        var allSettings = configuration.Settings.Select(s => new PeripheralSettingViewModel(s, Apply));
        Settings.ReplaceRange(allSettings);
        Guidance.ReplaceRange(configuration.Guidance);

        StatusMessage = Settings.Count == 0
            ? "This firmware does not report settings for this workflow."
            : $"{Settings.Count} supported setting(s) loaded. Review changes before applying.";
        OnPropertyChanged(nameof(HasSettings));
    }

    private void ShowDisconnected()
    {
        dispatcher.Dispatch(() =>
        {
            Settings.Clear();
            Guidance.Clear();
            StatusMessage = "Connect a vehicle to load settings.";
            OnPropertyChanged(nameof(HasSettings));
        });
    }

    private void OnActiveVehicleChanged(object? sender, ActiveVehicleChangedEventArgs args)
    {
        dispatcher.Dispatch(() => LoadAsync().FireAndForget());
    }
}
