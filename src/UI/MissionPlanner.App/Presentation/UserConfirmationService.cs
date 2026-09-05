using MissionPlanner.App.Utilities.Dialogs;

namespace MissionPlanner.App.Presentation;

/// <summary>
/// Presents safety confirmations on the current page.
/// </summary>
public sealed class UserConfirmationService(IDialogService dialogService) : IUserConfirmationService
{
    /// <inheritdoc />
    public async Task<bool> ConfirmAsync(string title, string message, string acceptText, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = dialogService.CreateOptions(title, acceptText, null);
        return await dialogService.ConfirmAsync(options, message, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ConfirmPhraseAsync(string title, string message, string requiredPhrase, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = dialogService.CreateOptions(title);
        var entered = await dialogService.PromptAsync(options, $"{message}\n\nType exactly: {requiredPhrase}", string.Empty, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return string.Equals(entered?.Trim(), requiredPhrase, StringComparison.Ordinal);
    }
}
