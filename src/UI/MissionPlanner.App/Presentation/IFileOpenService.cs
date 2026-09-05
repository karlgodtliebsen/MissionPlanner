namespace MissionPlanner.App.Presentation;

/// <summary>Opens user-selected files through the native platform boundary.</summary>
public interface IFileOpenService
{
    /// <summary>Opens one file, or returns <see langword="null"/> when cancelled.</summary>
    Task<OpenedPlanningFile?> OpenAsync(string title, IReadOnlyList<string>? patterns = null, CancellationToken cancellationToken = default);
}
