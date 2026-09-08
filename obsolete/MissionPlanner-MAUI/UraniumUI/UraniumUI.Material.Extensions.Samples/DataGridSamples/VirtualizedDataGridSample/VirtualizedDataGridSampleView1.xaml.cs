using System.Diagnostics;

namespace UraniumUI.Material.Extensions.Samples.DataGridSamples.VirtualizedDataGridSample;

public partial class VirtualizedDataGridSampleView1 : ContentPageView<VirtualizedDataGridSampleViewModel>
{
    public VirtualizedDataGridSampleView1()
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
        Debug.Print("End Report 1");
        Debug.Print("####################################################");
    }


    /// <inheritdoc />
    protected override void OnNavigatedTo(NavigatedToEventArgs args)
    {
        Debug.Print("");
        Debug.Print("####################################################");
        Debug.Print("Begin Report 1");
        Debug.Print("####################################################");
        base.OnNavigatedTo(args);
    }
}
