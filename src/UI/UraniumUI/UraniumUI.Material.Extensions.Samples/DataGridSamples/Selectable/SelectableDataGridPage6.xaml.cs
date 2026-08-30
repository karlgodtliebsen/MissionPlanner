using UraniumUI.Pages;

namespace UraniumUI.Material.Extensions.Samples.DataGridSamples.Selectable;

public partial class SelectableDataGridPage6 : UraniumContentPage
{
    public SelectableDataGridPage6()
    {
        InitializeComponent();
        BindingContext = ServiceProviderHelper.GetRequiredService<SelectableDataGridPageViewModel6>();
    }
}
