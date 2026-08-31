namespace MissionPlanner.AvaloniaUI.App.Presentation;

/// <summary>
/// Copies text through the platform clipboard.
/// </summary>
public sealed class TextClipboardService : ITextClipboardService
{
    /// <inheritdoc />
    public Task SetTextAsync(string text)
    {
        throw new NotImplementedException();

        // return Clipboard.Default.SetTextAsync(text);
    }
}
