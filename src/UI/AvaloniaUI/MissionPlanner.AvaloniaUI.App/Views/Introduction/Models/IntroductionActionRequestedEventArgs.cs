namespace MissionPlanner.AvaloniaUI.App.Views.Introduction.Models;

/// <summary>
/// Provides data when an introduction action is requested.
/// </summary>
/// <param name="action"></param>
public sealed class IntroductionActionRequestedEventArgs(IntroductionAction action) : EventArgs
{
    /// <summary>
    /// Gets the <see cref="IntroductionAction"/> associated with the event.
    /// </summary>
    public IntroductionAction Action { get; } = action;
}
