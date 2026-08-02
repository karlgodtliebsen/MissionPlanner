using UraniumUI.Pages;

namespace UraniumUI.Material.Extensions.Samples.DataGridSamples.PaginationSamples;

public partial class PaginationSampleExtended1Page : UraniumContentPage
{
    public PaginationSampleExtended1Page()
    {
        InitializeComponent();
        BindingContext = ServiceHelper.GetRequiredService<PaginationSampleExtendedViewModel>();
    }
}
