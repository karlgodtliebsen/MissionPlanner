using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.AvaloniaUI.App.Views.InitSetup.MandatoryHardware.Models;
using MissionPlanner.Core.Setup;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.AvaloniaUI.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Provides lifecycle-safe presentation for metadata-backed mandatory parameter pages.</summary>
public abstract partial class MandatoryParameterViewModel : SetupWorkflowDetailViewModel
{
    private readonly IActiveVehicleContext activeVehicle;
    private CancellationTokenSource? operationCancellation;

    /// <summary>Initializes a metadata-backed mandatory workflow.</summary>
    protected MandatoryParameterViewModel(SetupWorkflowDescriptor descriptor, IActiveVehicleContext activeVehicle, ILogger<MandatoryParameterViewModel> logger)
        : base(descriptor, logger)
    {
        this.activeVehicle = activeVehicle;
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
        SetBusy();
        try
        {
            var configuration = await LoadConfigurationAsync(vehicleId, token);
            Dispatcher.Dispatch(() => Show(configuration));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Loading mandatory parameter configuration failed for {VehicleId}.", vehicleId);
            SetMessages(exception);
        }
        finally
        {
            ResetBusy();
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
    public override async Task ActivateAsync()
    {
        SetMessages("Connect a vehicle to load settings.");
        activeVehicle.Changed += OnActiveVehicleChanged;
        await base.ActivateAsync();
        await LoadAsync();
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
        SetBusy();
        try
        {
            var result = await ApplySettingAsync(vehicleId, change.Name, change.Value, token);
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
            SetMessages("Connect a vehicle to load settings.", exception.Message);
        }
        finally
        {
            ResetBusy();
        }
    }

    private CancellationToken StartOperation()
    {
        Cancel();
        operationCancellation = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken);
        SetMessages(null);
        return operationCancellation.Token;
    }

    private void Show(MandatoryParameterConfiguration configuration)
    {
        var allSettings = configuration.Settings.Select(s => new PeripheralSettingViewModel(s, Apply));
        Settings.ReplaceRange(allSettings);
        Guidance.ReplaceRange(configuration.Guidance);

        SetMessages(Settings.Count == 0
            ? "This firmware does not report settings for this workflow."
            : $"{Settings.Count} supported setting(s) loaded. Review changes before applying.");
        OnPropertyChanged(nameof(HasSettings));
    }

    private void ShowDisconnected()
    {
        Dispatcher.Dispatch(() =>
        {
            Settings.Clear();
            Guidance.Clear();
            OnPropertyChanged(nameof(HasSettings));
        });
    }

    private void OnActiveVehicleChanged(ActiveVehicleChangedEventArgs args)
    {
        Dispatcher.DispatchAsync(LoadAsync);
    }
}

