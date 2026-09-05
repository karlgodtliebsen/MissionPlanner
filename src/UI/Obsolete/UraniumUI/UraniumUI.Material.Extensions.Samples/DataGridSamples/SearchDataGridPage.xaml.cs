using UraniumUI.Pages;

namespace UraniumUI.Material.Extensions.Samples.DataGrids;

public partial class SearchDataGridPage : UraniumContentPage
{
    public SearchDataGridPage()
    {
        InitializeComponent();
        BindingContext = ServiceProviderHelper.GetRequiredService<SimpleDataGridPageViewModel>();
    }
}
