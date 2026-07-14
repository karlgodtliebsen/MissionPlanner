using MissionPlanner.App.Configuration;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <inheritdoc />
public partial class TransponderTabView : ContentView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransponderTabView"/> class.
    /// </summary>
    public TransponderTabView()
    {
        InitializeComponent();
        var viewModel = ServiceHelper.GetRequiredService<TransponderTabViewModel>();
        BindingContext = viewModel;
    }
}
