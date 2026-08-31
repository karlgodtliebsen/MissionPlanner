using Avalonia.Platform.Storage;
using MissionPlanner.AvaloniaUI.App.Utilities.Dialogs;
using MissionPlanner.AvaloniaUI.App.Utilities.Dispatching;

namespace MissionPlanner.AvaloniaUI.App.Presentation;

/// <summary>Avalonia implementation of planning file open and save boundaries.</summary>
public sealed class AvaloniaMissionPlanningFileService(IUiDispatcher dispatcher, IWindowProvider windowProvider)
    : IFileOpenService, IFileSaveService
{
    /// <inheritdoc />
    public Task<OpenedPlanningFile?> OpenAsync(string title, IReadOnlyList<string>? patterns = null,
        CancellationToken cancellationToken = default) => dispatcher.DispatchAsync(async () =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        var owner = windowProvider.ActiveWindow ?? throw new InvalidOperationException("No active window is available.");
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
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
        return new OpenedPlanningFile(file.Name, await file.OpenReadAsync(), file.TryGetLocalPath());
    });

    /// <inheritdoc />
    public Task<string?> SaveAsync(string fileName, Stream content, CancellationToken cancellationToken = default) =>
        dispatcher.DispatchAsync(async () =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            var owner = windowProvider.ActiveWindow ?? throw new InvalidOperationException("No active window is available.");
            var file = await owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save file",
                SuggestedFileName = fileName,
                DefaultExtension = Path.GetExtension(fileName).TrimStart('.')
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
            return file.TryGetLocalPath();
        });
}
