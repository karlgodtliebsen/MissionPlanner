using MissionPlanner.App.Configuration;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>
/// Provides the public API for GeoFenceTabView.
/// </summary>
public partial class GeoFenceTabView : ContentPage
{
	private readonly GeoFenceTabViewModel viewModel;

	/// <summary>
	/// Provides the public API for GeoFenceTabView.
	/// </summary>
	public GeoFenceTabView()
	{
		InitializeComponent();
		viewModel = ServiceHelper.GetRequiredService<GeoFenceTabViewModel>();
		BindingContext = viewModel;
	}

	/// <inheritdoc />
	protected override void OnAppearing()
	{
		base.OnAppearing();
		viewModel.Activate();
	}

	/// <inheritdoc />
	protected override void OnDisappearing()
	{
		viewModel.Deactivate();
		base.OnDisappearing();
	}
}
