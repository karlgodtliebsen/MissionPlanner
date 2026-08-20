using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.Definitions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Presents supported ADS-B and avoidance parameters.</summary>
public sealed class AdsbViewModel : MandatoryParameterViewModel
{
    private readonly IAdsbService service;

    /// <summary>Initializes the ADS-B workflow ViewModel.</summary>
    public AdsbViewModel(ISetupWorkflowCatalog catalog, IActiveVehicleContext activeVehicle, IAdsbService service, IDispatcher dispatcher)
        : base(catalog.Workflows.First(workflow => workflow.Key == SetupWorkflowKey.Adsb), activeVehicle, dispatcher)
    {
        this.service = service;
        Initialize();
    }

    /// <inheritdoc />
    protected override Task<MandatoryParameterConfiguration> LoadConfigurationAsync(VehicleId vehicleId, CancellationToken cancellationToken)
    {
        return service.GetConfigurationAsync(vehicleId, cancellationToken);
    }

    /// <inheritdoc />
    protected override Task<MandatoryParameterApplyResult> ApplySettingAsync(VehicleId vehicleId, string name, double value, CancellationToken cancellationToken)
    {
        return service.ApplyAsync(vehicleId, name, value, cancellationToken);
    }
}
