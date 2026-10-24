using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;
using MissionPlanner.App.Views.Navigation;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.MandatoryHardware.Sections;

/// <summary>Groups reported helicopter configuration while keeping each actuator-affecting write explicit.</summary>
public sealed class TraditionalHelicopterViewModel : ParameterHardwareViewModel
{
    private readonly IUserConfirmationService confirmation;

    /// <summary>Creates the helicopter workflow using the shared readback-confirmed parameter service.</summary>
    public TraditionalHelicopterViewModel(IActiveVehicleContext active, IOptionalHardwareService service,
        ILogger<TraditionalHelicopterViewModel> logger, INavigationService navigation, IUserConfirmationService confirmation)
        : base("traditional-helicopter", active, service, logger, navigation)
    {
        this.confirmation = confirmation;
        Settings.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(Swash));
            OnPropertyChanged(nameof(Rotor));
            OnPropertyChanged(nameof(Governor));
            OnPropertyChanged(nameof(Other));
        };
    }

    /// <summary>Gets swashplate, collective and manual-servo controls.</summary>
    public IEnumerable<ParameterSettingViewModel> Swash => Settings.Where(row => Group(row.Name) == 0);
    /// <summary>Gets rotor speed, throttle curve and spool timing controls.</summary>
    public IEnumerable<ParameterSettingViewModel> Rotor => Settings.Where(row => Group(row.Name) == 1);
    /// <summary>Gets governor controls.</summary>
    public IEnumerable<ParameterSettingViewModel> Governor => Settings.Where(row => Group(row.Name) == 2);
    /// <summary>Gets other reported helicopter settings.</summary>
    public IEnumerable<ParameterSettingViewModel> Other => Settings.Where(row => Group(row.Name) == 3);

    /// <inheritdoc />
    protected override Task<bool> ConfirmApplyAsync(PeripheralSetting setting, double value, CancellationToken token)
    {
        return confirmation.ConfirmAsync("Review helicopter setup change",
            $"{setting.Name}: {setting.CurrentValue} → {value}. Motor power must be disconnected and the rotor secured. This change may move servos. Apply this value?", "Apply", token);
    }

    private static int Group(string name) => name.Contains("GOV_", StringComparison.Ordinal) ? 2
        : name.Contains("RSC_", StringComparison.Ordinal) ? 1
        : name.Contains("SW_", StringComparison.Ordinal) || name.Contains("COL_", StringComparison.Ordinal) ||
          name.Contains("CYC_", StringComparison.Ordinal) || name.EndsWith("SV_MAN", StringComparison.Ordinal) ? 0 : 3;
}
