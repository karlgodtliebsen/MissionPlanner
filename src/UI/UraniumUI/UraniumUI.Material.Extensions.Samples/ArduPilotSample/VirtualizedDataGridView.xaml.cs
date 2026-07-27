using UraniumUI.Pages;

namespace UraniumUI.Material.Extensions.Samples.ArduPilotSample;

public partial class VirtualizedDataGridView : UraniumContentPage
{
    public VirtualizedDataGridView()
    {
        InitializeComponent();
        BindingContext = ServiceHelper.GetRequiredService<VirtualizedDataGridViewModel>();
    }
}
