using UraniumUI.Pages;

namespace UraniumUI.Material.Extensions.Samples.DataGrids;

public partial class CustomDataGridPage : UraniumContentPage
{
    public CustomDataGridPage()
    {
        InitializeComponent();
        BindingContext = ServiceProviderHelper.GetRequiredService<CustomDataGridPageViewModel>();
    }
}
