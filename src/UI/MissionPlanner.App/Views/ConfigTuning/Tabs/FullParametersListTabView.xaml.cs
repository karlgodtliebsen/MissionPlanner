using MissionPlanner.App.Configuration;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>
/// Interaction logic for FullParametersListTabView.xaml
/// </summary>
public partial class FullParametersListTabView : ContentPage, IDisposable
{
    private readonly FullParametersListTabViewModel viewModel;

    /// <summary>
    /// Initializes a new instance of the <see cref="FullParametersListTabView"/> class.
    /// </summary>
    public FullParametersListTabView()
    {
        InitializeComponent();
        viewModel = ServiceHelper.GetRequiredService<FullParametersListTabViewModel>();
        BindingContext = viewModel;
    }

    ///// <inheritdoc />
    //protected override void OnAppearing()
    //{
    //    base.OnAppearing();
    //    viewModel.Activate();
    //}

    ///// <inheritdoc />
    //protected override void OnDisappearing()
    //{
    //    // Pushing the owned progress dialog also makes the underlying MAUI page disappear.
    //    // Keep its lifecycle active until the actual Config page is navigated away from.
    //    if (!viewModel.IsShowingProgressDialog)
    //    {
    //        viewModel.Deactivate();
    //    }

    //    base.OnDisappearing();
    //}
    /// <inheritdoc />
    public void Dispose()
    {
    }
}
