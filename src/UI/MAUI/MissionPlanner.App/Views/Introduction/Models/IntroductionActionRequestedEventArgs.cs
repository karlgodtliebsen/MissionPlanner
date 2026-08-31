using MissionPlanner.App.Views.Introduction.Views;

namespace MissionPlanner.App.Views.Introduction.Models;

/// <summary>
/// Provides data for the <see cref="IntroductionTopicView.ActionRequested"/> event.    
/// </summary>
/// <param name="action"></param>
public sealed class IntroductionActionRequestedEventArgs(IntroductionAction action) : EventArgs
{
    /// <summary>
    /// Gets the <see cref="IntroductionAction"/> associated with the event.
    /// </summary>
    public IntroductionAction Action { get; } = action;
}
