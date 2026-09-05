using Avalonia.Platform.Storage;
using Microsoft.Extensions.Logging;
using MissionPlanner.AvaloniaUI.App.Utilities.Dialogs;
using MissionPlanner.AvaloniaUI.App.Utilities.Dispatching;

namespace MissionPlanner.AvaloniaUI.App.Presentation;

/// <summary>Avalonia implementation of planning file open and save boundaries.</summary>
public sealed class AvaloniaMissionPlanningFileService(
    IUiDispatcher dispatcher,
    IWindowProvider windowProvider,
    IFilePickerPathStore pathStore,
    ILogger<AvaloniaMissionPlanningFileService> logger)
    : IFileOpenService, IFileSaveService
{
    /// <inheritdoc />
    public Task<OpenedPlanningFile?> OpenAsync(string title, IReadOnlyList<string>? patterns = null,
        CancellationToken cancellationToken = default) => dispatcher.DispatchAsync(async () =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        var owner = windowProvider.ActiveWindow ?? throw new InvalidOperationException("No active window is available.");
        var startLocation = await GetStartLocationAsync(owner.StorageProvider, cancellationToken);
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            SuggestedStartLocation = startLocation,
            FileTypeFilter = patterns is { Count: > 0 }
                ? [new FilePickerFileType("Supported files") { Patterns = patterns }]
                : null
        });
        var file = files.FirstOrDefault();
        if (file is null)
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();
        var localPath = file.TryGetLocalPath();
        await RememberParentDirectoryAsync(localPath, cancellationToken);
        return new OpenedPlanningFile(file.Name, await file.OpenReadAsync(), localPath);
    });

    /// <inheritdoc />
    public Task<string?> SaveAsync(string fileName, Stream content, CancellationToken cancellationToken = default) =>
        dispatcher.DispatchAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var owner = windowProvider.ActiveWindow ?? throw new InvalidOperationException("No active window is available.");
            var startLocation = await GetStartLocationAsync(owner.StorageProvider, cancellationToken);
            var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save file",
                SuggestedFileName = fileName,
                DefaultExtension = Path.GetExtension(fileName).TrimStart('.'),
                SuggestedStartLocation = startLocation
            });
            if (file is null)
            {
                return null;
            }

            await using var destination = await file.OpenWriteAsync();
            if (content.CanSeek)
            {
                content.Position = 0;
            }
            await content.CopyToAsync(destination, cancellationToken);
            await destination.FlushAsync(cancellationToken);
            var localPath = file.TryGetLocalPath();
            await RememberParentDirectoryAsync(localPath, cancellationToken);
            return localPath;
        });

    private async Task<IStorageFolder?> GetStartLocationAsync(
        IStorageProvider storageProvider,
        CancellationToken cancellationToken)
    {
        try
        {
            var directoryPath = await pathStore.GetAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            {
                return null;
            }

            return await storageProvider.TryGetFolderFromPathAsync(new Uri(directoryPath));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or UriFormatException)
        {
            logger.LogWarning(exception, "Could not restore the last file-picker directory");
            return null;
        }
    }

    private async Task RememberParentDirectoryAsync(string? localPath, CancellationToken cancellationToken)
    {
        var directoryPath = localPath is null ? null : Path.GetDirectoryName(localPath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return;
        }

        try
        {
            await pathStore.SetAsync(directoryPath, cancellationToken);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "Could not persist file-picker directory {DirectoryPath}", directoryPath);
        }
    }
}
