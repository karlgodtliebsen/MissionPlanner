using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.Definitions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.AvaloniaUI.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Presents supported initial-tune parameters for explicit review and application.</summary>
public sealed class InitTuneParametersViewModel : MandatoryParameterViewModel
{
    private readonly IInitTuneParametersService service;

    /// <summary>Initializes the Initial Tune Parameters workflow ViewModel.</summary>
    public InitTuneParametersViewModel(ISetupWorkflowCatalog catalog, IActiveVehicleContext activeVehicle,
        IInitTuneParametersService service, ILogger<InitTuneParametersViewModel> logger)
        : base(catalog.Workflows.First(workflow => workflow.Key == SetupWorkflowKey.InitTuneParameters), activeVehicle, logger)
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

