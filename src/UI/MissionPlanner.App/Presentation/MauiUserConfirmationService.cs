namespace MissionPlanner.App.Presentation;

/// <summary>
/// Presents safety confirmations on the current MAUI page.
/// </summary>
public sealed class MauiUserConfirmationService(IDispatcher dispatcher) : IUserConfirmationService
{
    /// <inheritdoc />
    public async Task<bool> ConfirmAsync(string title, string message, string acceptText, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var accepted = false;
        await dispatcher.DispatchAsync(async () =>
        {
            var page = Application.Current?.Windows.FirstOrDefault()?.Page;
            if (page is not null)
            {
                accepted = await page.DisplayAlertAsync(title, message, acceptText, "Cancel");
            }
        });
        cancellationToken.ThrowIfCancellationRequested();
        return accepted;
    }
}
