using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
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

    /// <summary>Initializes a metadata-backed mandatory workflow.</summary>
    protected MandatoryParameterViewModel(SetupWorkflowDescriptor descriptor, IActiveVehicleContext activeVehicle, IDispatcher dispatcher)
        : base(descriptor)
    {
        this.activeVehicle = activeVehicle;
        this.dispatcher = dispatcher;
        activeVehicle.Changed += OnActiveVehicleChanged;
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

    /// <summary>Gets the current workflow status.</summary>
    [ObservableProperty]
    public partial string Status
    {
        get;
        private set;
    } = "Connect a vehicle to load settings.";

    /// <summary>Gets whether supported settings were found.</summary>
    public bool HasSettings => Settings.Count > 0;

    /// <summary>Starts the initial load after the concrete ViewModel has initialized its dependencies.</summary>
    protected void Initialize()
    {
        LoadAsync().FireAndForget();
    }

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
            Error = exception.Message;
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
    public override void Dispose()
    {
        activeVehicle.Changed -= OnActiveVehicleChanged;
        Cancel();
        base.Dispose();
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
            Status = result.Message;
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
            Error = exception.Message;
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
        Error = null;
        return operationCancellation.Token;
    }

    private void Show(MandatoryParameterConfiguration configuration)
    {
        var allSettings = configuration.Settings.Select(s => new PeripheralSettingViewModel(s, Apply));
        Settings.ReplaceRange(allSettings);
        Guidance.ReplaceRange(configuration.Guidance);

        Status = Settings.Count == 0
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
            Status = "Connect a vehicle to load settings.";
            OnPropertyChanged(nameof(HasSettings));
        });
    }

    private void OnActiveVehicleChanged(object? sender, ActiveVehicleChangedEventArgs args)
    {
        dispatcher.Dispatch(() => LoadAsync().FireAndForget());
    }
}
