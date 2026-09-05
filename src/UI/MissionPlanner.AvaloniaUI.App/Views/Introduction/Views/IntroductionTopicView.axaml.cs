using Avalonia.Controls;
using Avalonia.Interactivity;
using MissionPlanner.AvaloniaUI.App.Views.Introduction.Models;

namespace MissionPlanner.AvaloniaUI.App.Views.Introduction.Views;

/// <summary>Displays one introduction topic and raises its requested actions.</summary>
public partial class IntroductionTopicView : UserControl
{
    /// <summary>Occurs when an introduction action is requested.</summary>
    public event EventHandler<IntroductionActionRequestedEventArgs>? ActionRequested;

    /// <summary>Initializes the topic view.</summary>
    public IntroductionTopicView() => InitializeComponent();

    private void OnActionClicked(object? sender, RoutedEventArgs args)
    {
        if (sender is Button { DataContext: IntroductionAction action })
        {
            ActionRequested?.Invoke(this, new IntroductionActionRequestedEventArgs(action));
        }
    }
}
