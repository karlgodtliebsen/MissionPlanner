using MissionPlanner.App.Views.Common;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Library;

namespace MissionPlanner.App;

/// <inheritdoc />
public partial class App : Application
{
    private IServiceProvider serviceProvider;

    /// <inheritdoc />
    public App()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    protected override Window CreateWindow(IActivationState? activationState)
    {
        if (activationState is not null)
        {
            var topbarView = activationState.Context.Services.GetRequiredService<TopBarView>()!;
            // Store connection service reference for cleanup
            serviceProvider = activationState.Context.Services;
            var window = new Window(new AppShell(topbarView));
            // Handle window destruction to ensure connection cleanup
            window.Destroying += OnWindowDestroying;
            return window;
        }
        else
        {
            throw new DomainException("Error Initializing Application");
        }
    }

    private async void OnWindowDestroying(object? sender, EventArgs e)
    {
        // Ensure the connection is properly closed when the app is closing

        try
        {
            var connectionService = serviceProvider.GetRequiredService<IVehicleConnectionService>();
            await connectionService.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error disposing connection service: {ex.Message}");
        }
    }
}
