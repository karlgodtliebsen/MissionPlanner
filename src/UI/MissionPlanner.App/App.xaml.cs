using MissionPlanner.App.Views.Common;

namespace MissionPlanner.App;

/// <inheritdoc />
public partial class App : Application
{
    /// <inheritdoc />
    public App()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override Window CreateWindow(IActivationState? activationState)
    {
        var topbarView = activationState?.Context.Services.GetRequiredService<TopBarView>()!;
        return new Window(new AppShell(topbarView));
    }
}
