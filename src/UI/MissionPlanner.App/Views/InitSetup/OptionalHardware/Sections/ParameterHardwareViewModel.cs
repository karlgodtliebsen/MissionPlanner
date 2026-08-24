using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>Shared lifecycle model for one metadata-backed Optional Hardware module.</summary>
public abstract partial class ParameterHardwareViewModel : OptionalHardwareBaseViewModel
{
    private readonly string moduleKey;
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IOptionalHardwareService service;
    private readonly ILogger<ParameterHardwareViewModel> logger;
    private CancellationTokenSource? cancellation = null;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParameterHardwareViewModel"/> class.
    /// </summary>
    /// <param name="moduleKey">The key of the module.</param>
    /// <param name="activeVehicle">The active vehicle context.</param>
    /// <param name="service">The optional hardware service.</param>
    /// <param name="logger"></param>
    protected ParameterHardwareViewModel(string moduleKey, IActiveVehicleContext activeVehicle,
        IOptionalHardwareService service, ILogger<ParameterHardwareViewModel> logger) : base(logger)
    {
        this.moduleKey = moduleKey;
        this.activeVehicle = activeVehicle;
        this.service = service;
        this.logger = logger;
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

    private async Task Load()
    {
        StatusMessage = "Loading supported settings…";

        if (cancellation != null)
        {
            await cancellation.CancelAsync();
            cancellation.Dispose();
        }

        cancellation = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken);
        if (activeVehicle.VehicleId is not { } id || !activeVehicle.IsOnline)
        {
            StatusMessage = "Connect a vehicle to load this hardware.";
            Settings.Clear();
            return;
        }

        try
        {
            IsBusy = true;
            var module = (await service.GetModulesAsync(id, cancellation.Token)).FirstOrDefault(x => x.Key == moduleKey);
            Settings.Clear();
            if (module is null)
            {
                StatusMessage = "This hardware is not reported by the active vehicle.";
                return;
            }

            Settings.ReplaceRange(module.Settings.Select(setting => new ParameterSettingViewModel(setting, ApplyAsync)));
            StatusMessage = module.Description;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Debug.Print("Error Loading ");
            logger.LogError(ex, ex.Message);
        }
        finally
        {
            IsBusy = false;
            if (cancellation != null)
            {
                await cancellation.CancelAsync();
                cancellation.Dispose();
                cancellation = null;
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
        StatusMessage = result.Message;
        RebootRequired |= result.RequiresReboot;
        if (result.Success)
        {
            await Dispatcher.DispatchAsync(Load);
        }
    }

    private async void Changed(object? s, ActiveVehicleChangedEventArgs e)
    {
        await Dispatcher.DispatchAsync(Load);
    }

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        cancellation = new CancellationTokenSource();
        activeVehicle.Changed += Changed;
        await Dispatcher.DispatchAsync(Load);
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        activeVehicle.Changed -= Changed;
        cancellation?.Cancel();
        cancellation?.Dispose();
        return Task.CompletedTask;
    }

}
