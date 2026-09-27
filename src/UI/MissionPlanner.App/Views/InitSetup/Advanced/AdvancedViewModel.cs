using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using Mapsui.Utilities;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.Common;
using MissionPlanner.Core.Setup.Advanced;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.Advanced;

/// <summary>
/// Interaction logic for AdvancedViewModel 
/// </summary>
public sealed partial class AdvancedViewModel : ViewModelBase
{
    private readonly IAdvancedPlatformCapabilities platform;
    private readonly IActiveVehicleContext activeVehicle;
    private readonly IVehicleConnectionService connection;
    //private readonly AdvancedAvailabilityService availability;
    //private readonly AdvancedToolRegistry registry;
    //private readonly INavigationService navigation;
    private readonly IVehicleParameterRegistry parameters;
    private readonly Diagnostics.LiveTelemetryInspectorViewModel? inspector;

    private bool active;
    private int generation;

    /// <summary>Initializes the hub from existing application and platform services.</summary>
    public AdvancedViewModel(
        IAdvancedPlatformCapabilities platform,
        IActiveVehicleContext activeVehicle,
        IVehicleConnectionService connection,
        //AdvancedAvailabilityService availability,
        //AdvancedToolRegistry registry, 
        //INavigationService navigation, 
        IVehicleParameterRegistry parameters,
        IUiDispatcher dispatcher, IDomainEventHub eventHub,
        ILogger<AdvancedViewModel> logger,
        Diagnostics.LiveTelemetryInspectorViewModel? inspector = null)
        : base(logger, dispatcher, eventHub)
    {
        this.platform = platform;
        this.connection = connection;
        //this.availability = availability;
        //this.registry = registry;
        //this.navigation = navigation;
        this.parameters = parameters;
        this.inspector = inspector;
        this.activeVehicle = activeVehicle;
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

    /// <summary>
    /// Gets whether the selected workflow links to a Config page.
    /// </summary>
    partial void OnSelectedTabChanged(TabItemViewModel? value)
    {
        inspector?.SuggestContext(value?.Descriptor.Key);
    }
    /// <summary>Gets the active vehicle heading.</summary>
    [ObservableProperty]
    public partial string VehicleHeading { get; private set; } = "No vehicle connected";


    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        if (!active)
        {
            Debug.Print("AdvancedViewModel ActivateAsync Enter");

            active = true;
            generation++;
            platform.Changed += PlatformChanged;
            activeVehicle.Changed += VehicleChanged;
            parameters.Changed += ParametersChanged;

            Refresh();
        }
        Debug.Print("AdvancedViewModel ActivateAsync Exit");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        Debug.Print("AdvancedViewModel DeactivateAsync Enter");
        Deactivate();
        Debug.Print("AdvancedViewModel DeactivateAsync Exit");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        Deactivate();
        base.Dispose();
    }

    private void Deactivate()
    {
        active = false;
        generation++;
        platform.Changed -= PlatformChanged;
        activeVehicle.Changed -= VehicleChanged;
        parameters.Changed -= ParametersChanged;
    }

    private void PlatformChanged(AdvancedPlatformCapabilities _)
    {
        Refresh();
    }

    private void VehicleChanged(ActiveVehicleChangedEventArgs _)
    {
        Refresh();
    }

    private void ParametersChanged(VehicleParameterChangedEventArgs _)
    {
        Refresh();
    }

    private AdvancedSessionState GetSessionState()
    {
        var id = activeVehicle.VehicleId;
        var expected = id.HasValue ? parameters.GetParameterCount(id.Value) : null;
        var complete = id.HasValue && expected is > 0 && parameters.GetAllParameters(id.Value).Count >= expected;
        return new(connection.IsConnected, activeVehicle.IsOnline && id.HasValue, complete);
    }

    private void Refresh()
    {
        var version = generation;
        Dispatcher.Dispatch(() =>
        {
            if (!active || version != generation)
            {
                return;
            }
            var state = GetSessionState();
            //foreach (var tool in Tools)
            //{
            //    var result = availability.Evaluate(tool.Feature, platform.Current, state);
            //    tool.Availability = result.CanLaunch && !registry.Contains(tool.Feature.Id)
            //        ? new(AdvancedAvailabilityState.TemporarilyUnavailable, "This tool's implementation is pending in the Advanced Setup task bundle.")
            //        : result;
            //}
        });
    }

}

