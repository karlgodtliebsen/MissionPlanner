using UraniumUI.Pages;

namespace UraniumUI.Material.Extensions.Samples.DataGrids;

public partial class SimpleDataGridPage : UraniumContentPage
{
    public SimpleDataGridPage()
    {
        InitializeComponent();
        BindingContext = ServiceProviderHelper.GetRequiredService<SimpleDataGridPageViewModel>();
    }
}
