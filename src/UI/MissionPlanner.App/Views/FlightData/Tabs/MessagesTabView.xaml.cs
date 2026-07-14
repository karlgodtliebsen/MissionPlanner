using MissionPlanner.App.Configuration;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <inheritdoc />
public partial class MessagesTabView : ContentView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MessagesTabView"/> class.
    /// </summary>
    public MessagesTabView()
    {
        InitializeComponent();
        var viewModel = ServiceHelper.GetRequiredService<MessagesTabViewModel>();
        BindingContext = viewModel;
    }
}
