using MissionPlanner.App.Configuration;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>
/// Provides the public API for ExtendedTuningTabView.
/// </summary>
public partial class ExtendedTuningTabView : ContentPage
{
	private readonly ExtendedTuningTabViewModel viewModel;

	/// <summary>
	/// Provides the public API for ExtendedTuningTabView.
	/// </summary>
	public ExtendedTuningTabView()
	{
		InitializeComponent();
		viewModel = ServiceHelper.GetRequiredService<ExtendedTuningTabViewModel>();
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
