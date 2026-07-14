using MissionPlanner.App.Configuration;
using UraniumUI.Pages;

namespace MissionPlanner.App.Views.Dashboard;

/// <summary>
/// Represents the dashboard page of the application.
/// </summary>
public partial class DashboardPage : UraniumContentPage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardPage"/> class with the specified view model.
    /// </summary>
    public DashboardPage()
    {
        InitializeComponent();

        var viewModel = ServiceHelper.GetRequiredService<DashboardPageViewModel>();
        BindingContext = viewModel;

        //var view = FindByName("VehiclesView") as StackLayout;
        //view!.Children.Add(vehiclesView);
        // PickerField.SelectedValueChangedCommand = viewModel.ExecuteCommand;
    }
}
