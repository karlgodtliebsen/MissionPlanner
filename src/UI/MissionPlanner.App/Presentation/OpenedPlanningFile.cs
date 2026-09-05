namespace MissionPlanner.App.Presentation;

/// <summary>An opened user-selected file and its readable content.</summary>
/// <param name="FileName">Safe display file name.</param>
/// <param name="Content">Readable file content owned by the caller.</param>
/// <param name="FullPath">Native path when the platform exposes one; otherwise <see langword="null"/>.</param>
public sealed record OpenedPlanningFile(string FileName, Stream Content, string? FullPath = null) : IDisposable
{
    /// <inheritdoc />
    public void Dispose()
    {
        Content.Dispose();
    }
}
