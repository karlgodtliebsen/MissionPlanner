namespace MissionPlanner.App.Views.InitSetup.Advanced;

/// <summary>Displays the advanced setup workspace.</summary>
public partial class AdvancedPage : NavigationViewBase<AdvancedViewModel>
{
    /// <summary>Initializes the advanced setup page.</summary>
    public AdvancedPage()
    {
        InitializeComponent();
        //DataContext = ServiceHelper.GetRequiredService<AdvancedViewModel>();
    }
}
