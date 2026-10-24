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

    /// <summary>Gets the navigation service shared by optional parameter workflows.</summary>
    protected INavigationService Navigation { get; }

    private readonly string moduleKey;
    /// <summary>Gets the active vehicle context shared by parameter and passive telemetry views.</summary>
    protected IActiveVehicleContext ActiveVehicle => activeVehicle;

    private readonly IActiveVehicleContext activeVehicle;
    private readonly IOptionalHardwareService service;
    private CancellationTokenSource? cancellation = null;
    private CancellationTokenSource? pageLifetime;
    private MissionPlanner.Shared.Models.Vehicles.Models.VehicleId? loadedVehicle;
    private CancellationToken loadedConnection;

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
        using var request = CancellationTokenSource.CreateLinkedTokenSource(activeVehicle.ConnectionCancellationToken, pageLifetime?.Token ?? CancellationToken.None);
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
            var pending = loadedVehicle == id && loadedConnection == activeVehicle.ConnectionCancellationToken
                ? Settings.Where(row => row.Editor.IsDirty).ToDictionary(row => row.Name, row => (row.CurrentValue, row.Editor.Value))
                : new Dictionary<string, (double CurrentValue, double Value)>();
            Settings.Clear();
            loadedVehicle = id;
            loadedConnection = activeVehicle.ConnectionCancellationToken;
            if (module is null)
            {
                SetMessages("This hardware is not reported by the active vehicle.");
                return;
            }

            var connection = activeVehicle.ConnectionCancellationToken;
            Settings.ReplaceRange(module.Settings.Select(setting => new ParameterSettingViewModel(setting, (selected, value, token) =>
                activeVehicle.IsOnline && activeVehicle.VehicleId == id && activeVehicle.ConnectionCancellationToken == connection ? ApplyAsync(selected, value, token) : Task.CompletedTask)));
            var conflicted = false;
            foreach (var row in Settings)
            {
                if (!pending.TryGetValue(row.Name, out var edit))
                {
                    continue;
                }
                if (row.CurrentValue != edit.CurrentValue)
                {
                    conflicted = true;
                    continue;
                }
                if (row.Editor.HasOptions)
                {
                    row.Editor.SelectedOption = row.Editor.Options.FirstOrDefault(option => option.Value == edit.Value) ?? row.Editor.SelectedOption;
                }
                else
                {
                    row.Editor.NumericValue = edit.Value;
                }
            }
            SetMessages(module.Description + (conflicted ? " Some pending values conflicted with updated vehicle values and were reloaded; review them again." : ""));
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

    /// <summary>Allows actuator-sensitive workflows to confirm a proposed value before transmission.</summary>
    protected virtual Task<bool> ConfirmApplyAsync(PeripheralSetting setting, double value, CancellationToken token) => Task.FromResult(true);

    private async Task ApplyAsync(PeripheralSetting setting, double value, CancellationToken cancellationToken)
    {
        if (activeVehicle.VehicleId is not { } id)
        {
            return;
        }

        if (pageLifetime is null || IsBusy)
        {
            return;
        }
        using var write = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, activeVehicle.ConnectionCancellationToken, pageLifetime.Token);
        SetBusy();
        try
        {
            if (!await ConfirmApplyAsync(setting, value, write.Token))
            {
                return;
            }
            write.Token.ThrowIfCancellationRequested();
            if (activeVehicle.VehicleId != id || !activeVehicle.IsOnline)
            {
                return;
            }
            var result = await service.SetValueAsync(id, setting.Name, value, write.Token);
            write.Token.ThrowIfCancellationRequested();
            SetMessages(result.Message);
            RebootRequired |= result.RequiresReboot;
            if (result.Success)
            {
                Settings.FirstOrDefault(row => row.Name == setting.Name)?.Editor.AcceptValue(value);
                await Dispatcher.DispatchAsync(Load);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            SetMessages(exception);
        }
        finally
        {
            ResetBusy();
        }
    }

    private void Changed(ActiveVehicleChangedEventArgs e)
    {
        Dispatcher.DispatchAsync(Load).SafeFireAndForget(exception => Logger.LogError(exception, "Could not reload hardware settings for the new active vehicle."));
    }

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        pageLifetime?.Cancel();
        pageLifetime?.Dispose();
        pageLifetime = new CancellationTokenSource();
        activeVehicle.Changed -= Changed;
        activeVehicle.Changed += Changed;
        await Dispatcher.DispatchAsync(Load);
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        activeVehicle.Changed -= Changed;
        pageLifetime?.Cancel();
        pageLifetime?.Dispose();
        pageLifetime = null;
        cancellation?.Cancel();
        cancellation = null;
        ResetBusy();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        activeVehicle.Changed -= Changed;
        pageLifetime?.Cancel();
        pageLifetime?.Dispose();
        pageLifetime = null;
        cancellation?.Cancel();
        cancellation = null;
        base.Dispose();
    }

}

