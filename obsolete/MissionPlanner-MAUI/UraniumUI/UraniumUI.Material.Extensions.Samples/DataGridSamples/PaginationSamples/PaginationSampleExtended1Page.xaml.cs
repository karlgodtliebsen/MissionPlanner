using UraniumUI.Pages;

namespace UraniumUI.Material.Extensions.Samples.DataGridSamples.PaginationSamples;

public partial class PaginationSampleExtended1Page : UraniumContentPage
{
    public PaginationSampleExtended1Page()
    {
        InitializeComponent();
        BindingContext = ServiceProviderHelper.GetRequiredService<PaginationSampleExtendedViewModel>();
    }
}
