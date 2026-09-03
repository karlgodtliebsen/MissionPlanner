using Microsoft.Extensions.Logging;
using MissionPlanner.AvaloniaUI.App.Utilities;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.Definitions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.AvaloniaUI.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Presents supported vehicle failsafe settings and safety guidance.</summary>
public sealed class FailSafeViewModel : MandatoryParameterViewModel
{
    private readonly IFailSafeService service;

    /// <summary>Gets the purpose of the failsafe settings page.</summary>
    public string Description => "Configure supported vehicle failsafe actions and thresholds.";

    /// <summary>Initializes the Failsafe workflow ViewModel.</summary>
    public FailSafeViewModel(
        IActiveVehicleContext activeVehicle,
        IFailSafeService service, ILogger<FailSafeViewModel> logger)
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

    /// <inheritdoc />
    public override Task ActivateAsync()
    {
        return base.ActivateAsync();
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        return base.DeactivateAsync();
    }
}

