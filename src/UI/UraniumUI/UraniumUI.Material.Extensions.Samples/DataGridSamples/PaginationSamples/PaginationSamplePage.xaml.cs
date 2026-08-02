using UraniumUI.Pages;

namespace UraniumUI.Material.Extensions.Samples.DataGridSamples.PaginationSamples;

public partial class PaginationSamplePage : UraniumContentPage
{
    public PaginationSamplePage()
    {
        InitializeComponent();
        BindingContext = ServiceHelper.GetRequiredService<PaginationSampleViewModel>();
    }
}
