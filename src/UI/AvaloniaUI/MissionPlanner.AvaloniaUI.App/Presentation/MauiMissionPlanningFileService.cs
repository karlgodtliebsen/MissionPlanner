namespace MissionPlanner.AvaloniaUI.App.Presentation;

/// <summary>MAUI implementation of planning file open and save boundaries.</summary>
//public sealed class MauiMissionPlanningFileService(IFileSaver fileSaver) : IFileOpenService, IFileSaveService
//{
//    /// <inheritdoc />
//    public async Task<OpenedPlanningFile?> OpenAsync(string title, IReadOnlyDictionary<DevicePlatform, IEnumerable<string>>? fileTypes = null, CancellationToken cancellationToken = default)
//    {
//        var file = await FilePicker.Default.PickAsync(new PickOptions
//        {
//            PickerTitle = title,
//            FileTypes = fileTypes is null ? null : new FilePickerFileType(fileTypes.ToDictionary(pair => pair.Key, pair => pair.Value))
//        });
//        if (file is null)
//        {
//            return null;
//        }

//        cancellationToken.ThrowIfCancellationRequested();
//        return new OpenedPlanningFile(Path.GetFileName(file.FileName), await file.OpenReadAsync(), file.FullPath);
//    }

//    /// <inheritdoc />
//    public async Task<string?> SaveAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
//    {
//        var result = await fileSaver.SaveAsync(fileName, content, cancellationToken);
//        return result.IsSuccessful ? result.FilePath : null;
//    }
//}
