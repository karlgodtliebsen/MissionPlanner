using UraniumUI.Pages;

namespace UraniumUI.Material.Extensions.Samples.DataGridSamples.VirtualizedDataGridSample;

public partial class VirtualizedDataGridSampleView2 : UraniumContentPage
{
    public VirtualizedDataGridSampleView2()
    {
        InitializeComponent();
        BindingContext = ServiceHelper.GetRequiredService<VirtualizedDataGridSampleViewModel>();
    }
}
