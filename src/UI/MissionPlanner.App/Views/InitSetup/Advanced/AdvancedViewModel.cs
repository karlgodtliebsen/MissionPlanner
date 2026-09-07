using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.App.Views.Navigation;
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
    private readonly IActiveVehicleContext vehicle;
    private readonly IVehicleConnectionService connection;
    private readonly AdvancedAvailabilityService availability;
    private readonly AdvancedToolRegistry registry;
    private readonly INavigationService navigation;
    private readonly IVehicleParameterRegistry parameters;
    private bool active;
    private int generation;

    /// <summary>Initializes the hub from existing application and platform services.</summary>
    public AdvancedViewModel(IAdvancedPlatformCapabilities platform, IActiveVehicleContext vehicle,
        IVehicleConnectionService connection, AdvancedAvailabilityService availability,
        AdvancedToolRegistry registry, INavigationService navigation, IVehicleParameterRegistry parameters,
        ILogger<AdvancedViewModel> logger, IUiDispatcher dispatcher, IDomainEventHub eventHub)
        : base(logger, dispatcher, eventHub)
    {
        this.platform = platform;
        this.vehicle = vehicle;
        this.connection = connection;
        this.availability = availability;
        this.registry = registry;
        this.navigation = navigation;
        this.parameters = parameters;
        Tools = AdvancedFeatureCatalog.All.Select(feature =>
            new AdvancedToolCardViewModel(feature, logger, dispatcher, eventHub)).ToArray();
    }

    /// <summary>Gets all thirteen cards in stable order.</summary>
    public IReadOnlyList<AdvancedToolCardViewModel> Tools { get; }

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        if (!active)
        {
            active = true;
            generation++;
            platform.Changed += PlatformChanged;
            vehicle.Changed += VehicleChanged;
            parameters.Changed += ParametersChanged;
            foreach (var tool in Tools)
            {
                tool.LaunchRequested += LaunchRequested;
            }
            Refresh();
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        Detach();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        Detach();
        foreach (var tool in Tools)
        {
            tool.Dispose();
        }
        base.Dispose();
    }

    private void Detach()
    {
        active = false;
        generation++;
        platform.Changed -= PlatformChanged;
        vehicle.Changed -= VehicleChanged;
        parameters.Changed -= ParametersChanged;
        foreach (var tool in Tools)
        {
            tool.LaunchRequested -= LaunchRequested;
        }
    }

    private void PlatformChanged(AdvancedPlatformCapabilities _) => Refresh();
    private void VehicleChanged(ActiveVehicleChangedEventArgs _) => Refresh();
    private void ParametersChanged(VehicleParameterChangedEventArgs _) => Refresh();

    private AdvancedSessionState GetSessionState()
    {
        var id = vehicle.VehicleId;
        var expected = id.HasValue ? parameters.GetParameterCount(id.Value) : null;
        var complete = id.HasValue && expected is > 0 && parameters.GetAllParameters(id.Value).Count >= expected;
        return new(connection.IsConnected, vehicle.IsOnline && id.HasValue, complete);
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
            foreach (var tool in Tools)
            {
                var result = availability.Evaluate(tool.Feature, platform.Current, state);
                tool.Availability = result.CanLaunch && !registry.Contains(tool.Feature.Id)
                    ? new(AdvancedAvailabilityState.TemporarilyUnavailable, "This tool's implementation is pending in the Advanced Setup task bundle.")
                    : result;
            }
        });
    }

    private void LaunchRequested(AdvancedFeatureId id)
    {
        if (active)
        {
            _ = NavigateAsync(id);
        }
    }

    private async Task NavigateAsync(AdvancedFeatureId id)
    {
        try
        {
            var tool = Tools.Single(item => item.Feature.Id == id);
            var state = GetSessionState();
            if (registry.Contains(id) && availability.Evaluate(tool.Feature, platform.Current, state).CanLaunch)
            {
                await navigation.NavigateAsync(tool.Feature.Route);
            }
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Advanced tool navigation failed for {Tool}", id);
            StatusMessage = "Unable to open this tool. Return to Advanced and try again.";
        }
    }
}

