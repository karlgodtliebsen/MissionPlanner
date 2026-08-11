namespace MissionPlanner.App.Presentation;

/// <summary>Presents text and numeric prompts for mission-planning workflows.</summary>
public interface IUserPromptService
{
    /// <summary>Prompts for text and returns <see langword="null"/> when cancelled.</summary>
    Task<string?> PromptAsync(string title, string message, string? initialValue = null, CancellationToken cancellationToken = default);
}

/// <summary>Presents a bounded set of choices for mission-planning workflows.</summary>
public interface IUserChoiceService
{
    /// <summary>Returns the selected option, or <see langword="null"/> when cancelled.</summary>
    Task<string?> ChooseAsync(string title, IReadOnlyList<string> choices, CancellationToken cancellationToken = default);
}

/// <summary>An opened user-selected file and its readable content.</summary>
/// <param name="FileName">Safe display file name.</param>
/// <param name="Content">Readable file content owned by the caller.</param>
/// <param name="FullPath">Native path when the platform exposes one; otherwise <see langword="null"/>.</param>
public sealed record OpenedPlanningFile(string FileName, Stream Content, string? FullPath = null) : IDisposable
{
    /// <inheritdoc />
    public void Dispose() => Content.Dispose();
}

/// <summary>Opens user-selected files through the native platform boundary.</summary>
public interface IFileOpenService
{
    /// <summary>Opens one file, or returns <see langword="null"/> when cancelled.</summary>
    Task<OpenedPlanningFile?> OpenAsync(string title, IReadOnlyDictionary<DevicePlatform, IEnumerable<string>>? fileTypes = null, CancellationToken cancellationToken = default);
}

/// <summary>Saves generated planning files through the native platform boundary.</summary>
public interface IFileSaveService
{
    /// <summary>Saves content under a suggested file name and returns the resulting path when available.</summary>
    Task<string?> SaveAsync(string fileName, Stream content, CancellationToken cancellationToken = default);
}
