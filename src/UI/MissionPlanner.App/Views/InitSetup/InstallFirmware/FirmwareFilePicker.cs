namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Represents a user-selected firmware file whose stream is opened only by its consumer.</summary>
public sealed record FirmwareFileSelection(
    string FileName,
    Func<CancellationToken, Task<Stream>> OpenReadAsync);

/// <summary>Abstracts the native file picker for testable firmware presentation logic.</summary>
public interface IFirmwareFilePicker
{
    /// <summary>Selects one local firmware package, or returns null when cancelled.</summary>
    Task<FirmwareFileSelection?> PickAsync(CancellationToken cancellationToken = default);
}

/// <summary>Adapts the MAUI native picker to the firmware view model.</summary>
public sealed class MauiFirmwareFilePicker : IFirmwareFilePicker
{
    /// <inheritdoc />
    public async Task<FirmwareFileSelection?> PickAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var file = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Select ArduPilot firmware (.apj or .px4)"
        });
        cancellationToken.ThrowIfCancellationRequested();
        return file is null
            ? null
            : new FirmwareFileSelection(file.FileName, _ => file.OpenReadAsync());
    }
}
