namespace MissionPlanner.App.Views.Connect;

/// <summary>
/// Represents the connect popup view.
/// </summary>
public partial class ConnectPopupView : ContentView
{
    /// <summary>
    /// Raised when the user requests the popup be closed.
    /// </summary>
    public event EventHandler? CloseRequested;


    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectPopupView"/> class with the specified view model.
    /// </summary>
    /// <param name="model">The view model for the connect popup.</param>
    public ConnectPopupView(ConnectPopupViewModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }

    private void CloseButton_Clicked(object? sender, EventArgs e)
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
}
