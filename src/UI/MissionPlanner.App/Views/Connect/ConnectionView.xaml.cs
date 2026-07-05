using UraniumUI.Pages;

namespace MissionPlanner.App.Views.Connect;

/// <inheritdoc />
public partial class ConnectionView : UraniumContentPage
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="popupView"></param>
    /// <param name="viewModel"></param>
    public ConnectionView(ConnectPopupView popupView, ConnectionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
        var view = FindByName("PopupView") as StackLayout;
        view!.Children.Add(popupView);
    }
}
