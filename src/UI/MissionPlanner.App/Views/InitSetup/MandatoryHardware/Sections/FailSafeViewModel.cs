using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.Definitions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.App.Views.Navigation;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Presents supported vehicle failsafe settings and safety guidance.</summary>
public sealed partial class FailSafeViewModel : MandatoryParameterViewModel
{
    private readonly IFailSafeService service;
    private readonly IActiveVehicleContext vehicle;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(500) };

    /// <summary>Gets passive live RC, flight, battery and estimator context.</summary>
    [ObservableProperty]
    public partial string LiveContext { get; private set; } = "No connected vehicle.";

    private void UpdateContext(object? sender, EventArgs args)
    {
        LiveContext = SetupTelemetrySummary.Failsafe(vehicle.State, DateTimeOffset.UtcNow);
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        timer.Stop();
        timer.Tick -= UpdateContext;
        base.Dispose();
    }


    /// <summary>Gets the purpose of the failsafe settings page.</summary>
    public string Description => "Configure supported vehicle failsafe actions and thresholds.";

    /// <summary>Initializes the Failsafe workflow ViewModel.</summary>
    public FailSafeViewModel(
        IActiveVehicleContext activeVehicle,
        IFailSafeService service, ILogger<FailSafeViewModel> logger, INavigationService navigation)
        : base(activeVehicle, logger, navigation)
    {
        this.service = service;
        vehicle = activeVehicle;
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
        timer.Tick -= UpdateContext;
        timer.Tick += UpdateContext;
        UpdateContext(null, EventArgs.Empty);
        timer.Start();
        return base.ActivateAsync();
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        timer.Stop();
        timer.Tick -= UpdateContext;
        return base.DeactivateAsync();
    }
}

