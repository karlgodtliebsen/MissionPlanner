using Avalonia.Input.Platform;
using MissionPlanner.AvaloniaUI.App.Utilities.Dialogs;
using MissionPlanner.AvaloniaUI.App.Utilities.Dispatching;

namespace MissionPlanner.AvaloniaUI.App.Presentation;

/// <summary>Copies text through Avalonia's platform clipboard.</summary>
public sealed class TextClipboardService(IUiDispatcher dispatcher, IWindowProvider windowProvider) : ITextClipboardService
{
    /// <inheritdoc />
    public Task SetTextAsync(string text) => dispatcher.DispatchAsync(async () =>
    {
        var owner = windowProvider.ActiveWindow ?? throw new InvalidOperationException("No active window is available.");
        var clipboard = owner.Clipboard ?? throw new InvalidOperationException("The platform clipboard is unavailable.");
        await clipboard.SetTextAsync(text);
    });
}
