using UraniumUI.Pages;

namespace UraniumUI.Material.Extensions.Samples.DataGridSamples.VirtualizedDataGridSample;

public partial class VirtualizedDataGridSampleView4 : UraniumContentPage
{
    public VirtualizedDataGridSampleView4()
    {
        InitializeComponent();
        BindingContext = ServiceHelper.GetRequiredService<VirtualizedDataGridSampleViewModel>();
    }
}
