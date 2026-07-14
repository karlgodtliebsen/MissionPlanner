using MissionPlanner.App.Configuration;

namespace MissionPlanner.App.Views.Common;

/// <inheritdoc />
public partial class TopBarView : ContentView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TopBarView"/> class with the specified view model.
    /// </summary>
    public TopBarView()
    {
        InitializeComponent();
        var viewModel = ServiceHelper.GetRequiredService<TopBarViewModel>();
        BindingContext = viewModel;
    }
}
