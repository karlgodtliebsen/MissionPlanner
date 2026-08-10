using UraniumUI.Pages;

namespace UraniumUI.Material.Extensions.Samples.DataGridSamples.Selectable;

public partial class SelectableDataGridPage4 : UraniumContentPage
{
    public SelectableDataGridPage4()
    {
        InitializeComponent();
        BindingContext = ServiceHelper.GetRequiredService<SelectableDataGridPageViewModel4>();
    }
}
