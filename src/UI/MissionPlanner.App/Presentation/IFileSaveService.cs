namespace MissionPlanner.App.Presentation;

/// <summary>Saves generated planning files through the native platform boundary.</summary>
public interface IFileSaveService
{
    /// <summary>Saves content under a suggested file name and returns the resulting path when available.</summary>
    Task<string?> SaveAsync(string fileName, Stream content, CancellationToken cancellationToken = default);
}