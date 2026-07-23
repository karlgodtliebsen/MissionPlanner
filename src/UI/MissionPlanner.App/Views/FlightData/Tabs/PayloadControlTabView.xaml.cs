using MissionPlanner.App.Helpers;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <inheritdoc />
public partial class PayloadControlTabView : ContentView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PayloadControlTabView"/> class.
    /// </summary>
    public PayloadControlTabView()
    {
        InitializeComponent();
        var viewModel = ServiceHelper.GetRequiredService<PayloadControlTabViewModel>();
        BindingContext = viewModel;
    }
}
