using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;

using MissionPlanner.App.Views.Navigation;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>Shared lifecycle model for one metadata-backed Optional Hardware module.</summary>
public abstract partial class ParameterHardwareViewModel : OptionalHardwareBaseViewModel
{
    /// <summary>Refreshes the current page state without starting a hardware operation.</summary>
    [RelayCommand]
    private Task RefreshAsync()
    {
        return IsBusy ? Task.CompletedTask : Dispatcher.DispatchAsync(() => LoadCoreAsync(true));
    }

    /// <summary>Opens the shared Full Parameters workspace when no operation is running.</summary>
    [RelayCommand]
    private async Task OpenFullParametersAsync()
    {
        if (IsBusy)
        {
            return;
        }
        await Navigation.NavigateAsync(MissionPlannerRoutes.Configuration);
    }

    /// <summary>Gets the navigation service shared by optional parameter workflows.</summary>
    protected INavigationService Navigation { get; }

    private readonly string moduleKey;
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IOptionalHardwareService service;
    private CancellationTokenSource? cancellation = null;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParameterHardwareViewModel"/> class.
    /// </summary>
    /// <param name="moduleKey">The key of the module.</param>
    /// <param name="activeVehicle">The active vehicle context.</param>
    /// <param name="service">The optional hardware service.</param>
    /// <param name="logger"></param>
    /// <param name="navigation">The application navigation service.</param>
    protected ParameterHardwareViewModel(string moduleKey, IActiveVehicleContext activeVehicle,
        IOptionalHardwareService service, ILogger<ParameterHardwareViewModel> logger, INavigationService navigation) : base(logger)
    {
        Navigation = navigation;
        this.moduleKey = moduleKey;
        this.activeVehicle = activeVehicle;
        this.service = service;
    }

    /// <summary>Gets editable settings.</summary>
    public ObservableRangeCollection<ParameterSettingViewModel> Settings { get; } = [];


    /// <summary>Gets reboot state.</summary>
    [ObservableProperty]
    public partial bool RebootRequired
    {
        get; private set;
    }


    [RelayCommand]
    private async Task LoadAsync()
    {
        await Dispatcher.DispatchAsync(Load);
    }

    private Task Load()
    {
        return LoadCoreAsync(false);
    }

    private async Task LoadCoreAsync(bool requestValues)
    {
        cancellation?.Cancel();
        using var request = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken);
        cancellation = request;
        try
        {
            if (activeVehicle.VehicleId is not { } id || !activeVehicle.IsOnline)
            {
                Settings.Clear();
                SetMessages("Connect a vehicle to load this hardware.");
                return;
            }

            SetBusy();
            SetMessages("Loading supported settings…");
            if (requestValues)
            {
                await service.RefreshAsync(id, request.Token);
                request.Token.ThrowIfCancellationRequested();
            }
            var modules = await service.GetModulesAsync(id, request.Token);
            request.Token.ThrowIfCancellationRequested();
            if (!ReferenceEquals(cancellation, request) || activeVehicle.VehicleId != id || !activeVehicle.IsOnline)
            {
                return;
            }

            var module = modules.FirstOrDefault(x => x.Key == moduleKey);
            Settings.Clear();
            if (module is null)
            {
                SetMessages("This hardware is not reported by the active vehicle.");
                return;
            }

            Settings.ReplaceRange(module.Settings.Select(setting => new ParameterSettingViewModel(setting, ApplyAsync)));
            SetMessages(module.Description);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            if (ReferenceEquals(cancellation, request))
            {
                Logger.LogError(exception, "Loading optional hardware settings failed.");
                SetMessages(exception);
            }
        }
        finally
        {
            if (ReferenceEquals(cancellation, request))
            {
                cancellation = null;
                ResetBusy();
            }
        }
    }

    private async Task ApplyAsync(PeripheralSetting setting, double value)
    {
        if (activeVehicle.VehicleId is not { } id)
        {
            return;
        }

        var result = await service.SetValueAsync(id, setting.Name, value, cancellation?.Token ?? default);
        SetMessages(result.Message);
        RebootRequired |= result.RequiresReboot;
        if (result.Success)
        {
            await Dispatcher.DispatchAsync(Load);
        }
    }

    private async void Changed(ActiveVehicleChangedEventArgs e)
    {
        await Dispatcher.DispatchAsync(Load);
    }

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        activeVehicle.Changed += Changed;
        await Dispatcher.DispatchAsync(Load);
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        activeVehicle.Changed -= Changed;
        cancellation?.Cancel();
        cancellation = null;
        ResetBusy();
        return Task.CompletedTask;
    }

}

