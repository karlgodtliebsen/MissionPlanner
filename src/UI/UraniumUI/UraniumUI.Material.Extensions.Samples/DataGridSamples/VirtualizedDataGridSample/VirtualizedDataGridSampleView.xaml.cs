using UraniumUI.Pages;

namespace UraniumUI.Material.Extensions.Samples.DataGrids.VirtualizedDataGridSample;

public partial class VirtualizedDataGridSampleView : UraniumContentPage
{
    public VirtualizedDataGridSampleView()
    {
        InitializeComponent();
        BindingContext = ServiceHelper.GetRequiredService<VirtualizedDataGridSampleViewModel>();
    }
}
