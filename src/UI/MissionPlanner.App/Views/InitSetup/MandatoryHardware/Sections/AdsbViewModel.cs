using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities;
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
    public AdsbViewModel(IActiveVehicleContext activeVehicle, IAdsbService service, ILogger<AdsbViewModel> logger)
        : base(activeVehicle, logger)
    {
        this.service = service;
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

