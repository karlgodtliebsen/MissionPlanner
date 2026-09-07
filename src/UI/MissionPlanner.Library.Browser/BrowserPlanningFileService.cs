using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dispatching;

namespace MissionPlanner.Library.Browser;

/// <summary>Uses the browser's single-view storage provider for user-initiated imports and downloads.</summary>
public sealed class BrowserPlanningFileService(IUiDispatcher dispatcher) : IFileOpenService, IFileSaveService
{
    /// <inheritdoc />
    public Task<OpenedPlanningFile?> OpenAsync(string title, IReadOnlyList<string>? patterns = null,
        CancellationToken cancellationToken = default) => dispatcher.DispatchAsync(async () =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        var files = await GetStorage().OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter = patterns is { Count: > 0 }
                ? [new FilePickerFileType("Supported files") { Patterns = patterns }] : null
        });
        cancellationToken.ThrowIfCancellationRequested();
        var file = files.FirstOrDefault();
        return file is null ? null : new OpenedPlanningFile(file.Name, await file.OpenReadAsync());
    });

    /// <inheritdoc />
    public Task<string?> SaveAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
        => dispatcher.DispatchAsync(async () =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        var file = await GetStorage().SaveFilePickerAsync(new FilePickerSaveOptions { SuggestedFileName = fileName });
        cancellationToken.ThrowIfCancellationRequested();
        if (file is null)
        {
            return null;
        }
        await using var output = await file.OpenWriteAsync();
        if (content.CanSeek)
        {
            content.Position = 0;
        }
        await content.CopyToAsync(output, cancellationToken);
        await output.FlushAsync(cancellationToken);
        return file.Name;
    });

    private static IStorageProvider GetStorage()
    {
        var view = (Application.Current?.ApplicationLifetime as ISingleViewApplicationLifetime)?.MainView;
        return view is null ? throw new InvalidOperationException("The browser view is not ready.")
            : TopLevel.GetTopLevel(view)?.StorageProvider ?? throw new InvalidOperationException("Browser file access is unavailable.");
    }
}
