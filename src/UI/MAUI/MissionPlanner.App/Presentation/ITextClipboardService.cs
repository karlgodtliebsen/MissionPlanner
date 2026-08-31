namespace MissionPlanner.App.Presentation;

/// <summary>
/// Copies text through the platform clipboard without exposing platform APIs to view models.
/// </summary>
public interface ITextClipboardService
{
    /// <summary>Copies text to the platform clipboard.</summary>
    /// <param name="text">The text to copy.</param>
    /// <returns>A task that completes when the copy operation finishes.</returns>
    Task SetTextAsync(string text);
}
