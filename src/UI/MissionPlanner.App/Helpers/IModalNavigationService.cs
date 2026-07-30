namespace MissionPlanner.App.Helpers;

/// <summary>
/// Defines methods for navigating modally within the application.
/// </summary>
public interface IModalNavigationService
{
    /// <summary>
    /// Displays a modal page of the specified type.
    /// </summary>
    /// <param name="animated">Indicates whether the display should be animated.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <typeparam name="TPage">The type of the page to display.</typeparam>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ShowAsync<TPage>(bool animated = true, CancellationToken cancellationToken = default) where TPage : Page;


    /// <summary>
    /// Displays a modal page.
    /// </summary>
    /// <param name="page">The page to display.</param>
    /// <param name="animated">Indicates whether the display should be animated.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task ShowAsync(Page page, bool animated = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Closes the currently displayed modal page.
    /// </summary>
    /// <param name="animated">Indicates whether the closing should be animated.</param>
    /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task CloseAsync(bool animated = true, CancellationToken cancellationToken = default);
}
