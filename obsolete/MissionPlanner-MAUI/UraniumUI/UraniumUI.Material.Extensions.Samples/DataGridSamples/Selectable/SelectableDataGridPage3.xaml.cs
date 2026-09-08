using UraniumUI.Pages;

namespace UraniumUI.Material.Extensions.Samples.DataGridSamples.Selectable;

public partial class SelectableDataGridPage3 : UraniumContentPage
{
    public SelectableDataGridPage3()
    {
        InitializeComponent();
        BindingContext = ServiceProviderHelper.GetRequiredService<SelectableDataGridPageViewModel3>();
    }
}
