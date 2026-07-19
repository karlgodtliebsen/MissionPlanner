using MissionPlanner.App.Configuration;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

public partial class MAVFtpTabView : ContentPage
{
    private readonly MavFtpTabViewModel viewModel;

    public MAVFtpTabView()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetRequiredService<MavFtpTabViewModel>();
        BindingContext = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = viewModel.RefreshCommand.ExecuteAsync(null);
    }
}
