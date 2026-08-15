namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Abstracts the native file picker for testable firmware presentation logic.</summary>
public interface IFirmwareFilePicker
{
    /// <summary>Selects one local firmware package, or returns null when cancelled.</summary>
    Task<FirmwareFileSelection?> PickAsync(CancellationToken cancellationToken = default);
}
