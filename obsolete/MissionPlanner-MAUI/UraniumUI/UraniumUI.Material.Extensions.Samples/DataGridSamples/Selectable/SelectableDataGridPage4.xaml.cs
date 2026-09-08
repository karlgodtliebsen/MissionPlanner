using UraniumUI.Pages;

namespace UraniumUI.Material.Extensions.Samples.DataGridSamples.Selectable;

public partial class SelectableDataGridPage4 : UraniumContentPage
{
    public SelectableDataGridPage4()
    {
        InitializeComponent();
        BindingContext = ServiceProviderHelper.GetRequiredService<SelectableDataGridPageViewModel4>();
    }
}
