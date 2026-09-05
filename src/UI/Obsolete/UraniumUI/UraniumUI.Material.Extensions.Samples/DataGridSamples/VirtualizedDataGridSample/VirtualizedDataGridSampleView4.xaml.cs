using System.Diagnostics;

namespace UraniumUI.Material.Extensions.Samples.DataGridSamples.VirtualizedDataGridSample;

public partial class VirtualizedDataGridSampleView4 : ContentPageView<VirtualizedDataGridSampleViewModel>
{
    public VirtualizedDataGridSampleView4()
    {
        InitializeComponent();
    }

    protected override void OnNavigatingFrom(NavigatingFromEventArgs args)
    {
        base.OnNavigatingFrom(args);
    }

    /// <inheritdoc />
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        var report = TheDataGrid.Diagnostics.CreateReport();
        Debug.Print(report);
        Debug.Print("");
        Debug.Print("####################################################");
        Debug.Print("End Report 4");
        Debug.Print("####################################################");
    }


    /// <inheritdoc />
    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        Debug.Print("");
        Debug.Print("####################################################");
        Debug.Print("Begin Report 4");
        Debug.Print("####################################################");
        base.OnNavigatedTo(args);
    }
}
