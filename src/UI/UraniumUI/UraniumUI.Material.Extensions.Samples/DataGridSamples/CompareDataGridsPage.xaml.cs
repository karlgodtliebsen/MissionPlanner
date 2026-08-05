using InputKit.Shared.Controls;
using UraniumUI.Pages;

namespace UraniumUI.Material.Extensions.Samples.DataGridSamples;

public partial class CompareDataGridsPage : UraniumContentPage
{
    public CompareDataGridsPage()
    {
        SelectionView.GlobalSetting.CornerRadius = 0;
        InitializeComponent();
    }

    private void ShowBottomSheet(object sender, EventArgs e)
    {
        bottomSheet.IsPresented = true;
    }
}
