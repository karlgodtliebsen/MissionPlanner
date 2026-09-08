using UraniumUI.Pages;

namespace UraniumUI.Material.Extensions.Samples.DataGridSamples.PaginationSamples;

public partial class PaginationSampleExtended2Page : UraniumContentPage
{
    public PaginationSampleExtended2Page()
    {
        InitializeComponent();
        BindingContext = ServiceProviderHelper.GetRequiredService<PaginationSampleExtendedViewModel>();
    }
}
