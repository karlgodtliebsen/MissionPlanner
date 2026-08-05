namespace UraniumUI.Material.Extensions.Samples.ControlsSamples;

public partial class ExtendedTabViewPage : Pages.UraniumContentPage
{
    public ExtendedTabViewPage()
    {
        InitializeComponent();
        BindingContext = new TabViewHeaderViewModel();
    }
}
