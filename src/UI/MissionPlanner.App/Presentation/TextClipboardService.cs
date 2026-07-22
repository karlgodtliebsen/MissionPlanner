namespace MissionPlanner.App.Presentation;

/// <summary>
/// Copies text through the platform clipboard.
/// </summary>
public sealed class TextClipboardService : ITextClipboardService
{
    /// <inheritdoc />
    public Task SetTextAsync(string text)
    {
        return Clipboard.Default.SetTextAsync(text);
    }
}
