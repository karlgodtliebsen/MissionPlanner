namespace MissionPlanner.App.Presentation;

/// <summary>
/// Requests explicit confirmation for a potentially hazardous user action.
/// </summary>
public interface IUserConfirmationService
{
    /// <summary>Displays a confirmation prompt.</summary>
    /// <param name="title">The prompt title.</param>
    /// <param name="message">The safety explanation.</param>
    /// <param name="acceptText">The affirmative button text.</param>
    /// <param name="cancellationToken">A token that cancels the prompt.</param>
    /// <returns><see langword="true"/> only when the user explicitly accepts.</returns>
    Task<bool> ConfirmAsync(string title, string message, string acceptText, CancellationToken cancellationToken = default);
}
