using MissionPlanner.App.Presentation;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>Selects firmware through the shared persistent-path file picker.</summary>
public sealed class AvaloniaFirmwareFilePicker(IFileOpenService fileOpenService) : IFirmwareFilePicker
{
    private static readonly string[] FirmwarePatterns = ["*.apj", "*.px4", "*_with_bl.hex"];

    /// <inheritdoc />
    public async Task<FirmwareFileSelection?> PickAsync(CancellationToken cancellationToken = default)
    {
        using var selectedFile = await fileOpenService.OpenAsync(
            "Select ArduPilot firmware (.apj, .px4, or *_with_bl.hex)",
            FirmwarePatterns,
            cancellationToken);
        if (selectedFile is null)
        {
            return null;
        }

        if (selectedFile.FullPath is { } localPath)
        {
            return new FirmwareFileSelection(
                selectedFile.FileName,
                _ => Task.FromResult<Stream>(File.OpenRead(localPath)),
                localPath);
        }

        await using var buffer = new MemoryStream();
        await selectedFile.Content.CopyToAsync(buffer, cancellationToken);
        var content = buffer.ToArray();
        return new FirmwareFileSelection(
            selectedFile.FileName,
            _ => Task.FromResult<Stream>(new MemoryStream(content, writable: false)));
    }
}
