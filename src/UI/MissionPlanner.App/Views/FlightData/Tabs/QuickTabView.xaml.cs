using MissionPlanner.App.Helpers;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <inheritdoc />
public partial class QuickTabView : ContentView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QuickTabView"/> class.
    /// </summary>
    public QuickTabView()
    {
        InitializeComponent();
        var viewModel = ServiceHelper.GetRequiredService<QuickTabViewModel>();
        BindingContext = viewModel;
    }
    ///// <inheritdoc />
    //protected override void OnAppearing()
    //{
    //    base.OnAppearing();
    //    viewModel.InitializeView();
    //}

    ///// <inheritdoc />
    //protected override void OnDisappearing()
    //{
    //    viewModel.Deactivate();
    //    base.OnDisappearing();
    //}
}
