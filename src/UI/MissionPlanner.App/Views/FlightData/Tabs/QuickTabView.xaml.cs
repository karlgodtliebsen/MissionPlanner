using MissionPlanner.App.Configuration;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <inheritdoc />
public partial class QuickTabView : ContentView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QuickTabView"/> class.
    /// </summary>
    public QuickTabView()
    {
        InitializeComponent();
        var viewModel = ServiceHelper.GetRequiredService<QuickTabViewModel>();
        BindingContext = viewModel;
    }
}
