using UraniumUI.Pages;

namespace UraniumUI.Material.Extensions.Samples.DataGridSamples.Selectable;

public partial class SelectableDataGridPage2 : UraniumContentPage
{
    public SelectableDataGridPage2()
    {
        InitializeComponent();
        BindingContext = ServiceHelper.GetRequiredService<SelectableDataGridPageViewModel2>();
    }
}
