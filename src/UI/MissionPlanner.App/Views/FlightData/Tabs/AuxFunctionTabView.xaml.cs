using MissionPlanner.App.Helpers;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <inheritdoc />
public partial class AuxFunctionTabView : ContentView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuxFunctionTabView"/> class.
    /// </summary>
    public AuxFunctionTabView()
    {
        InitializeComponent();
        var viewModel = ServiceHelper.GetRequiredService<AuxFunctionTabViewModel>();
        BindingContext = viewModel;
    }
}
