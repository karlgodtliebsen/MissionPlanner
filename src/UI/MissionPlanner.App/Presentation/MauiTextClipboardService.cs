namespace MissionPlanner.App.Presentation;

/// <summary>
/// Copies text through the MAUI platform clipboard.
/// </summary>
public sealed class MauiTextClipboardService : ITextClipboardService
{
    /// <inheritdoc />
    public Task SetTextAsync(string text) => Clipboard.Default.SetTextAsync(text);
}
