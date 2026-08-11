using CommunityToolkit.Maui.Storage;

namespace MissionPlanner.App.Presentation;

/// <summary>MAUI implementation of mission-planning prompts and choices.</summary>
public sealed class MauiMissionPlanningDialogService(IDispatcher dispatcher) : IUserPromptService, IUserChoiceService
{
    /// <inheritdoc />
    public async Task<string?> PromptAsync(string title, string message, string? initialValue = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string? result = null;
        await dispatcher.DispatchAsync(async () =>
        {
            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page is not null)
                result = await page.DisplayPromptAsync(title, message, initialValue: initialValue);
        });
        cancellationToken.ThrowIfCancellationRequested();
        return result;
    }

    /// <inheritdoc />
    public async Task<string?> ChooseAsync(string title, IReadOnlyList<string> choices, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string? result = null;
        await dispatcher.DispatchAsync(async () =>
        {
            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page is not null)
                result = await page.DisplayActionSheetAsync(title, "Cancel", null, choices.ToArray());
        });
        cancellationToken.ThrowIfCancellationRequested();
        return result == "Cancel" ? null : result;
    }
}

/// <summary>MAUI implementation of planning file open and save boundaries.</summary>
public sealed class MauiMissionPlanningFileService(IFileSaver fileSaver) : IFileOpenService, IFileSaveService
{
    /// <inheritdoc />
    public async Task<OpenedPlanningFile?> OpenAsync(string title, IReadOnlyDictionary<DevicePlatform, IEnumerable<string>>? fileTypes = null, CancellationToken cancellationToken = default)
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = title,
            FileTypes = fileTypes is null ? null : new FilePickerFileType(fileTypes.ToDictionary(pair => pair.Key, pair => pair.Value))
        });
        if (file is null)
            return null;
        cancellationToken.ThrowIfCancellationRequested();
        return new OpenedPlanningFile(Path.GetFileName(file.FileName), await file.OpenReadAsync());
    }

    /// <inheritdoc />
    public async Task<string?> SaveAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        var result = await fileSaver.SaveAsync(fileName, content, cancellationToken);
        return result.IsSuccessful ? result.FilePath : null;
    }
}
