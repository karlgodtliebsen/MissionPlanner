using UraniumUI.Pages;

namespace UraniumUI.Material.Extensions.Samples.DataGridSamples.VirtualizedDataGridSample;

public partial class VirtualizedDataGridSampleView1 : UraniumContentPage
{
    public VirtualizedDataGridSampleView1()
    {
        InitializeComponent();
        BindingContext = ServiceHelper.GetRequiredService<VirtualizedDataGridSampleViewModel>();
    }
}
