using UraniumUI.Pages;

namespace UraniumUI.Material.Extensions.Samples.DataGridSamples.VirtualizedDataGridSample;

public partial class VirtualizedDataGridSampleView4 : UraniumContentPage
{
    public VirtualizedDataGridSampleView4()
    {
        InitializeComponent();
    }


    ///// <inheritdoc />
    //protected override void OnAppearing()
    //{
    //    base.OnAppearing();

    //    var viewModel = ServiceHelper.GetRequiredService<VirtualizedDataGridSampleViewModel>();
    //    BindingContext = viewModel;
    //}

    ///// <inheritdoc />
    //protected override void OnDisappearing()
    //{
    //    base.OnDisappearing();
    //    BindingContext = null;
    //}

    /// <inheritdoc />
    protected override void OnNavigatingFrom(NavigatingFromEventArgs args)
    {
        base.OnNavigatingFrom(args);
        BindingContext = null;
    }

    /// <inheritdoc />
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        BindingContext = null;
    }

    /// <inheritdoc />
    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        base.OnNavigatedTo(args);

        var viewModel = ServiceHelper.GetRequiredService<VirtualizedDataGridSampleViewModel>();
        BindingContext = viewModel;
    }
}
