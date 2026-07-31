using UraniumUI.Pages;

namespace UraniumUI.Material.Extensions.Samples.DataGrids;

public partial class SelectableDataGridPage : UraniumContentPage
{
    public SelectableDataGridPage()
    {
        InitializeComponent();
        BindingContext = ServiceHelper.GetRequiredService<SelectableDataGridPageViewModel>();
    }
}
