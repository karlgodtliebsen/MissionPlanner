using UraniumUI.Pages;

namespace UraniumUI.Material.Extensions.Samples.DataGridSamples.Selectable;

public partial class SelectableDataGridPage1 : UraniumContentPage
{
    public SelectableDataGridPage1()
    {
        InitializeComponent();
        BindingContext = ServiceHelper.GetRequiredService<SelectableDataGridPageViewModel1>();
    }
}
