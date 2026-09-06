using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Firmware.Preparation;
using MissionPlanner.Library.EventHub.Abstractions;
namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Owns validated panel state and commands.</summary>
public sealed partial class ValidatedPackageViewModel : ViewModelBase
{
    /// <summary>Initializes the validated panel.</summary>
    public ValidatedPackageViewModel(
        ILogger<ValidatedPackageViewModel> logger,
        IUiDispatcher dispatcher,
        IDomainEventHub eventHub) : base(logger, dispatcher, eventHub)
    {
    }

    public void Reset()
    {
        PreparedFirmware = null;
        IsFirmwareValidated = false;
        CanInstall = false;
    }

    [ObservableProperty]
    public partial FirmwarePreparationResult? PreparedFirmware
    {
        get; set;
    }

    /// <summary>Gets whether a validated downloadable artifact is ready.</summary>
    public bool HasPreparedFirmware => PreparedFirmware is not null;

    [ObservableProperty]
    public partial bool IsFirmwareValidated
    {
        get;
        set;
    }
    /// <summary>Notifies the active parent about panel changes.</summary>
    public event Action<FirmwarePanelRequest>? OperationRequested;


    [RelayCommand(CanExecute = nameof(CanInstall))]
    private Task InstallAsync(CancellationToken cancellationToken)
    {
        return FirmwarePanelRequest.SendAsync(OperationRequested, FirmwarePanelAction.Install, cancellationToken);
    }

    /// <summary>Gets whether the parent permits installation.</summary>
    [ObservableProperty, NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    public partial bool CanInstall
    {
        get; set;
    }
    partial void OnPreparedFirmwareChanged(FirmwarePreparationResult? value) => OnPropertyChanged(nameof(HasPreparedFirmware));

}
