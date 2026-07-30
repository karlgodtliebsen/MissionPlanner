using UraniumUI.Pages;

namespace UraniumUI.Material.Extensions.Samples.DialogSamples;

public partial class DialogSampleView : UraniumContentPage
{
    public DialogSampleView()
    {
        InitializeComponent();

        BindingContext = ServiceHelper.GetRequiredService<DialogSampleViewModel>();
    }
}
