using MissionPlanner.App.Views.Common;

namespace MissionPlanner.App;

/// <summary>
/// The main shell of the application.
/// </summary>
public partial class AppShell : Shell
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AppShell"/> class.
    /// </summary>
    /// <param name="topbarView">The top bar view to be added to the shell.</param>
    public AppShell(TopBarView topbarView)
    {
        InitializeComponent();
        var view = FindByName("TopbarView") as StackLayout;
        view!.Children.Add(topbarView);
    }
}
