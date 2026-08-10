using UraniumUI.Pages;

namespace UraniumUI.Material.Extensions.Samples.DataGridSamples.Selectable;

public partial class SelectableDataGridPage5 : UraniumContentPage
{
    public SelectableDataGridPage5()
    {
        InitializeComponent();
        BindingContext = ServiceHelper.GetRequiredService<SelectableDataGridPageViewModel5>();
    }
}
