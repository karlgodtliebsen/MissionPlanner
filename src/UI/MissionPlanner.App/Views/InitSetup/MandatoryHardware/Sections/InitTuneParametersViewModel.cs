using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using MissionPlanner.Firmware;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.Definitions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.App.Views.Navigation;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Presents supported initial-tune parameters for explicit review and application.</summary>
public sealed partial class InitTuneParametersViewModel : MandatoryParameterViewModel
{
    private readonly IInitTuneParametersService service;
    private readonly IActiveVehicleContext vehicle;
    private VehicleId? proposedVehicle;
    private CancellationToken proposedConnection;

    /// <summary>Initializes the Initial Tune Parameters workflow ViewModel.</summary>
    public InitTuneParametersViewModel(IActiveVehicleContext activeVehicle,
        IInitTuneParametersService service, ILogger<InitTuneParametersViewModel> logger, INavigationService navigation)
        : base(activeVehicle, logger, navigation)
    {
        this.service = service;
        vehicle = activeVehicle;
    }

    /// <summary>Gets or sets propeller diameter in inches.</summary>
    [ObservableProperty]
    public partial double PropellerInches { get; set; } = 10;

    /// <summary>Gets or sets the series cell count.</summary>
    [ObservableProperty]
    public partial int BatteryCells { get; set; } = 4;

    /// <summary>Gets or sets the fully charged cell voltage.</summary>
    [ObservableProperty]
    public partial double CellMaximumVoltage { get; set; } = 4.2;

    /// <summary>Gets or sets the minimum cell voltage used by the historical calculator.</summary>
    [ObservableProperty]
    public partial double CellMinimumVoltage { get; set; } = 3.3;

    /// <summary>Gets or sets the explicit low-expo ESC option.</summary>
    [ObservableProperty]
    public partial bool TMotorEsc { get; set; }

    /// <summary>Gets recommendations for individual review; nothing is selected automatically.</summary>
    public ObservableCollection<InitialTuneProposalViewModel> Proposals { get; } = [];

    [RelayCommand]
    private void Calculate()
    {
        Proposals.Clear();
        proposedVehicle = null;
        if (!vehicle.IsOnline || vehicle.State?.Identity.Firmware.Family is not (FirmwareFamily.ArduCopter or FirmwareFamily.ArduPlane))
        {
            SetMessages("Connect a Copter or QuadPlane and load its settings first.");
            return;
        }
        try
        {
            if (vehicle.State.Identity.Firmware.Family == FirmwareFamily.ArduPlane &&
                !Settings.Any(row => row.Name.StartsWith("Q_A_", StringComparison.Ordinal)))
            {
                SetMessages("Load a QuadPlane parameter set before calculating multicopter recommendations.");
                return;
            }
            var calculated = InitTuneParametersCalculator.Calculate(PropellerInches, BatteryCells, CellMaximumVoltage, CellMinimumVoltage, TMotorEsc);
            var mapped = InitTuneParametersCalculator.Resolve(calculated, Settings.Select(row => row.Name), vehicle.State.Identity.Firmware.Family == FirmwareFamily.ArduPlane);
            foreach (var (name, value) in mapped)
            {
                var row = Settings.First(item => item.Name == name);
                Proposals.Add(new InitialTuneProposalViewModel(name, row.Value, value));
            }
            proposedVehicle = vehicle.VehicleId;
            proposedConnection = vehicle.ConnectionCancellationToken;
            SetMessages("Historical starting estimates only. Review each value against current firmware guidance, select the desired rows, then stage and apply. No vehicle writes have occurred.");
        }
        catch (ArgumentException exception)
        {
            SetMessages(exception.Message);
        }
    }

    [RelayCommand]
    private void StageSelected()
    {
        if (proposedVehicle is null || proposedVehicle != vehicle.VehicleId || !vehicle.IsOnline ||
            proposedConnection.IsCancellationRequested || proposedConnection != vehicle.ConnectionCancellationToken)
        {
            Proposals.Clear();
            SetMessages("Vehicle changed. Calculate a fresh proposal before staging.");
            return;
        }
        var selected = Proposals.Where(row => row.IsSelected).ToArray();
        if (selected.Any(proposal => Settings.FirstOrDefault(row => row.Name == proposal.Name)?.Value != proposal.Before))
        {
            SetMessages("Settings changed since calculation. Recalculate before staging these values.");
            return;
        }
        foreach (var proposal in selected)
        {
            Settings.First(row => row.Name == proposal.Name).NumericValue = proposal.After;
        }
        SetMessages($"Staged {selected.Length} reviewed recommendations. Use Apply modified to write and verify the selected values.");
        Proposals.Clear();
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        Proposals.Clear();
        proposedVehicle = null;
        return base.DeactivateAsync();
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


/// <summary>A selectable before/after estimate; selection is explicit and does not write the vehicle.</summary>
public sealed partial class InitialTuneProposalViewModel(string name, double before, double after) : ObservableObject
{
    /// <summary>Gets the actual reported parameter name.</summary>
    public string Name { get; } = name;
    /// <summary>Gets the value at calculation time.</summary>
    public double Before { get; } = before;
    /// <summary>Gets the recommendation in the reported parameter's native units.</summary>
    public double After { get; } = after;
    /// <summary>Gets or sets whether to stage this recommendation.</summary>
    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
