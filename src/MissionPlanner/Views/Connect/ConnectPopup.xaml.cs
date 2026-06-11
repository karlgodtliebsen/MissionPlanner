namespace MissionPlanner.Views.Connect;

public partial class ConnectPopup : ContentView
{
    public ConnectPopup()
    {
        InitializeComponent();
        BindingContext = new ConnectPopupViewModel();

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => IsVisible = false;
        GestureRecognizers.Add(tap);
    }
}