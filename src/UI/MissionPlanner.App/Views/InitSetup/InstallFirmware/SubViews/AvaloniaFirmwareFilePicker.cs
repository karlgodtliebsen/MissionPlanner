using MissionPlanner.App.Presentation;

namespace MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews;

/// <summary>Selects firmware through the shared persistent-path file picker.</summary>
public sealed class AvaloniaFirmwareFilePicker(IFileOpenService fileOpenService) : IFirmwareFilePicker
{
    /// <inheritdoc />
    public Task<FirmwareFileSelection?> PickAsync(CancellationToken cancellationToken = default)
    {
        return PickAsync(MissionPlanner.Firmware.Workflow.FirmwareArtifactFormat.Apj, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<FirmwareFileSelection?> PickAsync(MissionPlanner.Firmware.Workflow.FirmwareArtifactFormat format,
        CancellationToken cancellationToken = default)
    {
        string[] patterns = format switch
        {
            MissionPlanner.Firmware.Workflow.FirmwareArtifactFormat.WithBootloaderHex => ["*_with_bl.hex"],
            MissionPlanner.Firmware.Workflow.FirmwareArtifactFormat.Apj => ["*.apj"],
            _ => ["*.apj", "*_with_bl.hex"]
        };
        using var selectedFile = await fileOpenService.OpenAsync(
            "Select ArduPilot firmware (" + string.Join(", ", patterns) + ")",
            patterns,
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
