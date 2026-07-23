using MissionPlanner.App.Configuration;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>
/// Provides the public API for MAVFtpTabView.
/// </summary>
public partial class MAVFtpTabView : ContentPage
{
    private readonly MavFtpTabViewModel viewModel;

    /// <summary>
    /// Provides the public API for MAVFtpTabView.
    /// </summary>
    public MAVFtpTabView()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetRequiredService<MavFtpTabViewModel>();
        BindingContext = viewModel;
    }

    ///// <summary>
    ///// Provides the public API for OnAppearing.
    ///// </summary>
    //protected override void OnAppearing()
    //{
    //    base.OnAppearing();
    //    _ = viewModel.RefreshCommand.ExecuteAsync(null);
    //}
}
