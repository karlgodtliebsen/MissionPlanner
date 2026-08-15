namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Represents a user-selected firmware file whose stream is opened only by its consumer.</summary>
public sealed record FirmwareFileSelection(
    string FileName,
    Func<CancellationToken, Task<Stream>> OpenReadAsync);
