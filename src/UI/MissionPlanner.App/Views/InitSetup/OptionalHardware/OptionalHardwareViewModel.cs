using CommunityToolkit.Mvvm.ComponentModel;
using Mapsui.Utilities;
using MissionPlanner.App.Views.Common;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using TabItemViewModel = MissionPlanner.App.Views.Common.TabItemViewModel;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware;

/// <summary>Owns Optional Hardware availability and selected-tab state.</summary>
public sealed partial class OptionalHardwareViewModel : ObservableObject, IDisposable
{
    private readonly OptionalHardwareTabCatalog catalog;
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IVehicleParameterRegistry parameters;
    private readonly IDispatcher dispatcher;
    private CancellationTokenSource? refreshCancellation;

    /// <summary>Initializes the workspace.</summary>
    public OptionalHardwareViewModel(OptionalHardwareTabCatalog catalog,
        IActiveVehicleContext activeVehicle,
        IVehicleParameterRegistry parameters, IDispatcher dispatcher)
    {
        this.catalog = catalog;
        this.activeVehicle = activeVehicle;
        this.parameters = parameters;
        this.dispatcher = dispatcher;

        Tabs = new ObservableRangeCollection<TabItemViewModel>(
            catalog.Tabs.Select(item =>
                new TabItemViewModel(new TabDescriptor(item.Key.ToString(), item.Title, item.Description))));

        //, item.Order, item.RequiresVehicle, item.RequiresParameters, item.SupportsOffline, item.ParameterPrefixes, item.FirmwareFamilies))));

        activeVehicle.Changed += OnVehicleChanged;
        parameters.Changed += OnParameterChanged;
        Refresh();
    }

    /// <summary>
    /// Gets fixed index-aligned headers.
    /// </summary>
    public ObservableRangeCollection<TabItemViewModel> Tabs
    {
        get;
    }

    /// <summary>Gets or sets the selected header.</summary>
    [ObservableProperty]
    public partial TabItemViewModel? SelectedTab
    {
        get;
        set;
    }

    /// <summary>Gets the vehicle heading.</summary>
    [ObservableProperty]
    public partial string VehicleHeading
    {
        get;
        private set;
    } = "No vehicle connected";

    /// <summary>Gets the availability summary.</summary>
    [ObservableProperty]
    public partial string AvailabilitySummary
    {
        get;
        private set;
    } = string.Empty;

    private void Refresh()
    {
        var snapshot = activeVehicle.Current;
        var values =
            snapshot.VehicleId is
            {
            }
                id
                ? parameters.GetAllParameters(id)
                : new Dictionary<string, MavLink.Parameters.VehicleParameter>();

        var states = catalog.Evaluate(snapshot.IsOnline, snapshot.State?.Identity.Firmware.Family, values);
        for (var index = 0; index < Tabs.Count; index++)
        {
            Tabs[index].Update(states[index]);
        }

        SelectedTab = SelectedTab?.IsAvailable == true ? SelectedTab : Tabs.FirstOrDefault(item => item.IsAvailable);
        VehicleHeading = snapshot.IsOnline ? $"{snapshot.DisplayName} · {snapshot.State!.Identity.Firmware.Family}" : "No vehicle connected";
        var count = Tabs.Count(item => item.IsAvailable);
        AvailabilitySummary = snapshot.IsOnline ? $"{count} optional hardware tools available." : $"{count} standalone tools available; connect a vehicle to show vehicle-specific hardware.";
    }

    private void OnVehicleChanged(object? sender, ActiveVehicleChangedEventArgs args)
    {
        dispatcher.Dispatch(Refresh);
    }

    private void OnParameterChanged(object? sender, VehicleParameterChangedEventArgs args)
    {
        if (args.VehicleId != activeVehicle.VehicleId)
        {
            return;
        }

        refreshCancellation?.Cancel();
        refreshCancellation?.Dispose();
        refreshCancellation = new CancellationTokenSource();
        _ = RefreshLaterAsync(refreshCancellation.Token);
    }

    private async Task RefreshLaterAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(150, token);
            dispatcher.Dispatch(Refresh);
        }
        catch (OperationCanceledException)
        {
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        activeVehicle.Changed -= OnVehicleChanged;
        parameters.Changed -= OnParameterChanged;
        refreshCancellation?.Cancel();
        refreshCancellation?.Dispose();
    }
}
