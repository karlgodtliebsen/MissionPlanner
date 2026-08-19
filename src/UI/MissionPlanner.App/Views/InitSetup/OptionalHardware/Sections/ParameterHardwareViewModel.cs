using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    private CancellationTokenSource? cancellation = null;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParameterHardwareViewModel"/> class.
    /// </summary>
    /// <param name="moduleKey">The key of the module.</param>
    /// <param name="activeVehicle">The active vehicle context.</param>
    /// <param name="service">The optional hardware service.</param>
    protected ParameterHardwareViewModel(string moduleKey, IActiveVehicleContext activeVehicle, IOptionalHardwareService service)
    {
        this.moduleKey = moduleKey;
        this.activeVehicle = activeVehicle;
        this.service = service;
        activeVehicle.Changed += Changed;
        _ = Load();
    }

    /// <summary>Gets editable settings.</summary>
    public ObservableCollection<ParameterSettingViewModel> Settings { get; } = [];

    /// <summary>Gets status.</summary>
    [ObservableProperty]
    public partial string Status { get; private set; } = "Loading supported settings…";

    /// <summary>Gets reboot state.</summary>
    [ObservableProperty]
    public partial bool RebootRequired { get; private set; }


    [RelayCommand]
    private async Task LoadAsync()
    {
        await Dispatcher.DispatchAsync(Load);
    }

    private async Task Load()
    {
        if (cancellation != null)
        {
            await cancellation.CancelAsync();
            cancellation.Dispose();
        }

        cancellation = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken);
        if (activeVehicle.VehicleId is not { } id || !activeVehicle.IsOnline)
        {
            Status = "Connect a vehicle to load this hardware.";
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
                Status = "This hardware is not reported by the active vehicle.";
                return;
            }

            foreach (var setting in module.Settings)
            {
                Settings.Add(new ParameterSettingViewModel(setting, ApplyAsync));
            }

            Status = module.Description;
        }
        catch (OperationCanceledException)
        {
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
        Status = result.Message;
        RebootRequired |= result.RequiresReboot;
        if (result.Success)
        {
            await Load();
        }
    }

    private void Changed(object? s, ActiveVehicleChangedEventArgs e)
    {
        _ = Load();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        activeVehicle.Changed -= Changed;
        cancellation?.Cancel();
        cancellation?.Dispose();
    }
}
