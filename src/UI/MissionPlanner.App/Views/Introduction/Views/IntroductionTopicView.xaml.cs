using MissionPlanner.App.Views.Introduction.Models;

namespace MissionPlanner.App.Views.Introduction.Views;

/// <summary>
///  
/// </summary>
public partial class IntroductionTopicView : ContentView
{
    /// <summary>
    /// Occurs when an introduction action is requested.
    /// </summary>
    public event EventHandler<IntroductionActionRequestedEventArgs>? ActionRequested;

    /// <summary>
    /// Initializes a new instance of the <see cref="IntroductionTopicView"/> class.
    /// </summary>
    public IntroductionTopicView()
    {
        InitializeComponent();
    }

    private void OnActionClicked(object? sender, EventArgs e)
    {
        if (sender is Button { BindingContext: IntroductionAction action })
        {
            ActionRequested?.Invoke(this, new IntroductionActionRequestedEventArgs(action));
        }
    }
}
