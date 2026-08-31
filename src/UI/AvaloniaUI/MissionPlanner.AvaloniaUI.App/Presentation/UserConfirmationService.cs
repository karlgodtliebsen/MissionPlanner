using MissionPlanner.AvaloniaUI.App.Utilities.Dialogs;
using MissionPlanner.AvaloniaUI.App.Utilities.Dispatching;

namespace MissionPlanner.AvaloniaUI.App.Presentation;

/// <summary>
/// Presents safety confirmations on the current page.
/// </summary>
public sealed class UserConfirmationService(AvaloniaMissionPlanningDialogService dialogService) : IUserConfirmationService
{
    /// <inheritdoc />
    public async Task<bool> ConfirmAsync(string title, string message, string acceptText, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return await dialogService.ConfirmAsync(title, message, acceptText, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ConfirmPhraseAsync(string title, string message, string requiredPhrase, CancellationToken cancellationToken = default)
    {

        cancellationToken.ThrowIfCancellationRequested();
        var entered = await dialogService.PromptAsync(title, $"{message}\n\nType exactly: {requiredPhrase}", string.Empty, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return string.Equals(entered?.Trim(), requiredPhrase, StringComparison.Ordinal);
    }
}
