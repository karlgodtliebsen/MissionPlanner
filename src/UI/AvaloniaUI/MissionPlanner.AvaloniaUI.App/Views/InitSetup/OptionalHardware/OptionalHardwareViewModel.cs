using CommunityToolkit.Mvvm.ComponentModel;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.AvaloniaUI.App.Utilities;
using MissionPlanner.AvaloniaUI.App.Views.Common;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using TabItemViewModel = MissionPlanner.AvaloniaUI.App.Views.Common.TabItemViewModel;

namespace MissionPlanner.AvaloniaUI.App.Views.InitSetup.OptionalHardware;

/// <summary>Owns Optional Hardware availability and selected-tab state.</summary>
public sealed partial class OptionalHardwareViewModel : ViewModelBase
{
    private readonly OptionalHardwareTabCatalog catalog;
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IVehicleParameterRegistry parameters;
    private CancellationTokenSource? refreshCancellation;

    /// <summary>Initializes the workspace.</summary>
    public OptionalHardwareViewModel(OptionalHardwareTabCatalog catalog,
        IActiveVehicleContext activeVehicle,
        IVehicleParameterRegistry parameters, ILogger<OptionalHardwareViewModel> logger
        ) : base(logger)
    {
        this.catalog = catalog;
        this.activeVehicle = activeVehicle;
        this.parameters = parameters;
    }

    /// <summary>
    /// Gets fixed index-aligned headers.
    /// </summary>
    public ObservableRangeCollection<TabItemViewModel> Tabs { get; } = [];

    /// <summary>Gets or sets the selected header.</summary>
    [ObservableProperty]
    public partial TabItemViewModel? SelectedTab
    {
        get; set;
    }

    /// <summary>Gets the vehicle heading.</summary>
    [ObservableProperty]
    public partial string VehicleHeading { get; private set; } = "No vehicle connected";

    /// <summary>Gets the availability summary.</summary>
    [ObservableProperty]
    public partial string AvailabilitySummary { get; private set; } = string.Empty;

    private void Refresh()
    {
        var snapshot = activeVehicle.Current;
        var values =
            snapshot.VehicleId is { } id ? parameters.GetAllParameters(id) : new Dictionary<string, MavLink.Parameters.VehicleParameter>();

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

    private void OnVehicleChanged(ActiveVehicleChangedEventArgs args)
    {
        Dispatcher.Dispatch(Refresh);
    }

    private void OnParameterChanged(VehicleParameterChangedEventArgs args)
    {
        if (args.VehicleId != activeVehicle.VehicleId)
        {
            return;
        }

        refreshCancellation?.Cancel();
        refreshCancellation?.Dispose();
        refreshCancellation = new CancellationTokenSource();
        Dispatcher.Dispatch(Refresh);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        Deactivate();
    }

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        Tabs.ReplaceRange(
            catalog.Tabs.Select(item =>
                new TabItemViewModel(new TabDescriptor(item.Key.ToString(), item.Title, item.Description))));
        activeVehicle.Changed += OnVehicleChanged;
        parameters.Changed += OnParameterChanged;
        Refresh();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        Deactivate();
        return Task.CompletedTask;
    }

    private void Deactivate()
    {
        activeVehicle.Changed -= OnVehicleChanged;
        parameters.Changed -= OnParameterChanged;
        refreshCancellation?.Cancel();
        refreshCancellation?.Dispose();
    }
}

