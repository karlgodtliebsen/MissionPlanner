using MissionPlanner.App.Views.Introduction.Models;
using MissionPlanner.App.Views.Introduction.Views;

namespace MissionPlanner.App.Views.Introduction;

/// <summary>
/// Provides data for the <see cref="IntroductionTopicView.ActionRequested"/> event.    
/// </summary>
/// <param name="action"></param>
public sealed class IntroductionActionRequestedEventArgs(IntroductionAction action) : EventArgs
{
    public IntroductionAction Action { get; } = action;
}
