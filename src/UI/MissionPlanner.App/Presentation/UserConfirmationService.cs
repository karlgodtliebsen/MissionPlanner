using UraniumUI.Material.Dialogs;

namespace MissionPlanner.App.Presentation;

/// <summary>
/// Presents safety confirmations on the current page.
/// </summary>
public sealed class UserConfirmationService(IDispatcher dispatcher, IExtendedDialogService dialogService) : IUserConfirmationService
{
    /// <inheritdoc />
    public async Task<bool> ConfirmAsync(string title, string message, string acceptText, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var accepted = false;
        await dispatcher.DispatchAsync(async () =>
        {
            accepted = await dialogService.ConfirmAsync(title, message, acceptText, "Cancel");
        });
        cancellationToken.ThrowIfCancellationRequested();
        return accepted;
    }
}
