using UraniumUI.Pages;

namespace UraniumUI.Material.Extensions.Samples.DataGrids;

public partial class PaginationSamplePage : UraniumContentPage
{
    public PaginationSamplePage()
    {
        InitializeComponent();
        BindingContext = ServiceHelper.GetRequiredService<PaginationSampleViewModel>();
    }
}
