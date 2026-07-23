namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>
/// Interaction logic for FullParametersListTabView.xaml
/// </summary>
public partial class FullParametersListTabView : ContentPageView<FullParametersListTabViewModel>

{
    /// <summary>
    /// Initializes a new instance of the <see cref="FullParametersListTabView"/> class.
    /// </summary>
    public FullParametersListTabView() : base("//ConfigFullParameters")
    {
        InitializeComponent();
    }
}
