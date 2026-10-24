namespace MissionPlanner.App.Presentation;

/// <summary>Saves generated planning files through the native platform boundary.</summary>
public interface IFileSaveService
{
    /// <summary>Saves content under a suggested file name and returns the resulting path when available.</summary>
    Task<string?> SaveAsync(string fileName, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Saves to a new local file, refusing every existing destination; unsupported platforms must fail closed.</summary>
    Task<string?> SaveNewAsync(string fileName, Stream content, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This platform cannot guarantee creation of a separate file.");
}
