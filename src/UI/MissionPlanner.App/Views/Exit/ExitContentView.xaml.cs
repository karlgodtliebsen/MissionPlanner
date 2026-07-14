using MissionPlanner.App.Configuration;

namespace MissionPlanner.App.Views.Exit;

/// <inheritdoc />
public partial class ExitContentView : ContentView
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExitView"/> class.
    /// </summary>
    public ExitContentView()
    {
        InitializeComponent();
        var viewModel = ServiceHelper.GetRequiredService<ExitViewModel>();
        BindingContext = viewModel;
    }
}
