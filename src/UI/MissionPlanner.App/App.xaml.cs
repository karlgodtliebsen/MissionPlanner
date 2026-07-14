using MissionPlanner.App.Views.Exit;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Library;
using MissionPlanner.Library.EventHub.Abstractions;
using Serilog;

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

    private Window? window;

    /// <inheritdoc />
    protected override Window CreateWindow(IActivationState? activationState)
    {
        if (activationState is not null)
        {
            // Store connection service reference for cleanup
            serviceProvider = activationState.Context.Services;
            var domainEventHub = activationState.Context.Services.GetRequiredService<IDomainEventHub>();
            domainEventHub.SubscribeDomainEventAsync<ExitApplicationRequested>(Func);
            window = new Window(new AppShell());
            // Handle window destruction to ensure connection cleanup
            window.Destroying += OnWindowDestroying;
            return window;
        }
        else
        {
            throw new DomainException("Error Initializing Application");
        }
    }

    private async Task Func(ExitApplicationRequested _, CancellationToken ct)
    {
        await Dispatcher.DispatchAsync(() => CloseWindow(window!));
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
            Log.Logger.Error(ex, "Error disposing connection service");
            System.Diagnostics.Debug.WriteLine($"Error disposing connection service: {ex.Message}");
        }

        await Log.CloseAndFlushAsync().ConfigureAwait(false);
    }
}
