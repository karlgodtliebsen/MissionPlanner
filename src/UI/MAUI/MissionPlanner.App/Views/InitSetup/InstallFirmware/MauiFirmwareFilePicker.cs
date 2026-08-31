namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Adapts the MAUI native picker to the firmware view model.</summary>
public sealed class MauiFirmwareFilePicker : IFirmwareFilePicker
{
    /// <inheritdoc />
    public async Task<FirmwareFileSelection?> PickAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Select ArduPilot firmware (.apj, .px4, or *_with_bl.hex)" });
        cancellationToken.ThrowIfCancellationRequested();
        return file is null
            ? null
            : new FirmwareFileSelection(file.FileName, _ => file.OpenReadAsync(), file.FullPath);
    }
}
