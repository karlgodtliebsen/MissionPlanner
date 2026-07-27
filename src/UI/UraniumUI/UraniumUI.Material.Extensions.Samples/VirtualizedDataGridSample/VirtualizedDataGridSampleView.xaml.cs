using UraniumUI.Pages;

namespace UraniumUI.Material.Extensions.Samples.VirtualizedDataGridSample;

public partial class VirtualizedDataGridSampleView : UraniumContentPage
{
    public VirtualizedDataGridSampleView()
    {
        InitializeComponent();
        BindingContext = ServiceHelper.GetRequiredService<VirtualizedDataGridSampleViewModel>();
    }
}
