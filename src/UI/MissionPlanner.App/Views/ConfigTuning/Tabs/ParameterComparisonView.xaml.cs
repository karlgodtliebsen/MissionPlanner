namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>Displays and stages parameter comparison results.</summary>
public partial class ParameterComparisonView : ContentView
{
    /// <summary>Initializes a new instance of the <see cref="ParameterComparisonView"/> class.</summary>
    public ParameterComparisonView(ParameterComparisonViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
