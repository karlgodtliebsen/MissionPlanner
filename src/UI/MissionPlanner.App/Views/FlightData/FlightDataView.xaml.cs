using MissionPlanner.App.Helpers;
using UraniumUI.Extensions;
using UraniumUI.Pages;

namespace MissionPlanner.App.Views.FlightData;

/// <summary>
/// Represents the view for displaying flight data.
/// </summary>
public partial class FlightDataView : UraniumContentPage
{
    private readonly FlightDataViewModel viewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="FlightDataView"/> class.
    /// </summary>
    public FlightDataView()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetRequiredService<FlightDataViewModel>();
        BindingContext = viewModel;
        TabView.SelectedTabChanged += async (_, _) => await viewModel.SelectTabAsync(GetSelectedTabIndex());
    }

    /// <inheritdoc />
    protected override void OnAppearing()
    {
        base.OnAppearing();
        viewModel.ActivateAsync(GetSelectedTabIndex()).FireAndForget();
    }

    /// <inheritdoc />
    protected override void OnDisappearing()
    {
        viewModel.DeactivateAsync().FireAndForget();
        base.OnDisappearing();
    }

    private int GetSelectedTabIndex()
    {
        var index = TabView.Tabs.IndexOf(TabView.SelectedTab);
        return index < 0 ? 0 : index;
    }
}
