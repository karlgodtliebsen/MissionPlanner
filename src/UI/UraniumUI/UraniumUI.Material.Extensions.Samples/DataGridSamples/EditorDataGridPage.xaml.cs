using UraniumUI.Pages;

namespace UraniumUI.Material.Extensions.Samples.DataGrids;

public partial class EditorDataGridPage : UraniumContentPage
{
    public EditorDataGridPage()
    {
        InitializeComponent();
        BindingContext = ServiceHelper.GetRequiredService<EditorDataGridPageViewModel>();
    }
}
