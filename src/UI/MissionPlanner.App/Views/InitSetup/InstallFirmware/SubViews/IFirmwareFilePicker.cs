namespace MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews;

/// <summary>Abstracts the native file picker for testable firmware presentation logic.</summary>
public interface IFirmwareFilePicker
{
    /// <summary>Selects one local firmware package, or returns null when cancelled.</summary>
    Task<FirmwareFileSelection?> PickAsync(CancellationToken cancellationToken = default);

    /// <summary>Selects an artifact restricted to the current installation plan.</summary>
    Task<FirmwareFileSelection?> PickAsync(MissionPlanner.Firmware.Workflow.FirmwareArtifactFormat format,
        CancellationToken cancellationToken = default);
}
