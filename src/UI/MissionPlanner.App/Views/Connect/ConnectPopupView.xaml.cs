namespace MissionPlanner.App.Views.Connect;

/// <summary>
/// Represents the connect popup view.
/// </summary>
public partial class ConnectPopupView : ContentView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectPopupView"/> class with the specified view model.
    /// </summary>
    /// <param name="model">The view model for the connect popup.</param>
    public ConnectPopupView(ConnectPopupViewModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
