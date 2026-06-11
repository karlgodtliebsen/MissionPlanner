namespace MissionPlanner.Views.Connect;

/// <summary>
/// Represents the connect popup view.
/// </summary>
public partial class ConnectPopup : ContentView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectPopup"/> class with the specified view model.
    /// </summary>
    /// <param name="model">The view model for the connect popup.</param>
    public ConnectPopup(ConnectPopupViewModel model)
    {
        InitializeComponent();
        BindingContext = model;

        //TapGestureRecognizer tap = new();
        //tap.Tapped += (_, _) => IsVisible = false;
        //GestureRecognizers.Add(tap);
    }
}