namespace MissionPlanner.App.Views.InitSetup.InstallFirmware;

/// <summary>
/// Interaction logic for InstallFirmwareView.xaml
/// </summary>
public partial class InstallFirmwareView : UraniumUI.Pages.UraniumContentPage
{
    private readonly InstallFirmwareViewModel viewModel;
    /// <summary>
    /// Initializes a new instance of the <see cref="InstallFirmwareView"/> class.
    /// </summary>
    public InstallFirmwareView()
    {
        InitializeComponent();
        viewModel = Helpers.ServiceHelper.GetRequiredService<InstallFirmwareViewModel>();
        BindingContext = viewModel;
    }

    /// <inheritdoc />
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (Shell.Current is not null) Shell.Current.Navigating += OnShellNavigating;
        await viewModel.ActivateAsync();
    }

    /// <inheritdoc />
    protected override void OnDisappearing()
    {
        if (Shell.Current is not null) Shell.Current.Navigating -= OnShellNavigating;
        viewModel.Deactivate();
        base.OnDisappearing();
    }

    private void OnShellNavigating(object? sender, ShellNavigatingEventArgs args)
    {
        if (!viewModel.CanNavigateAway && args.CanCancel) args.Cancel();
    }
}
