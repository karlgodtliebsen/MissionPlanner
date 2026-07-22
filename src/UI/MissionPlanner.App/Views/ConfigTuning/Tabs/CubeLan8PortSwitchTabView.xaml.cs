using MissionPlanner.App.Configuration;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>
/// Provides the public API for CubeLan8PortSwitchTabView.
/// </summary>
public partial class CubeLan8PortSwitchTabView : ContentPage
{
	private readonly CubeLan8PortSwitchTabViewModel viewModel;

	/// <summary>
	/// Provides the public API for CubeLan8PortSwitchTabView.
	/// </summary>
	public CubeLan8PortSwitchTabView()
	{
		InitializeComponent();
		viewModel = ServiceHelper.GetRequiredService<CubeLan8PortSwitchTabViewModel>();
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
