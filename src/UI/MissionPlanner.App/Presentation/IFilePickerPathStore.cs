namespace MissionPlanner.App.Presentation;

/// <summary>Persists the most recently used local file-picker directory.</summary>
public interface IFilePickerPathStore
{
    /// <summary>Gets the most recently used directory, or <see langword="null"/> when none is stored.</summary>
    ValueTask<string?> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Persists the most recently used directory.</summary>
    ValueTask SetAsync(string directoryPath, CancellationToken cancellationToken = default);
}
