using MissionPlanner.App.Configuration;

namespace MissionPlanner.App.Views.Connect;

/// <summary>
/// Represents the connect popup view.
/// </summary>
public partial class ConnectPopupView : ContentView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectPopupView"/> class with the specified view model.
    /// </summary>
    public ConnectPopupView()
    {
        InitializeComponent();
        var viewModel = ServiceHelper.GetRequiredService<ConnectPopupViewModel>();
        BindingContext = viewModel;
    }
}
