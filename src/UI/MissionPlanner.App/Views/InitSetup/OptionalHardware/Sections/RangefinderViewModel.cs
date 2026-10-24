using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Views.Navigation;
using MissionPlanner.Core.Setup.Abstractions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;

/// <summary>Configures reported rangefinders and displays fresh passive sensor observations.</summary>
public sealed partial class RangefinderViewModel(IActiveVehicleContext vehicle, IOptionalHardwareService service,
    ILogger<RangefinderViewModel> logger, INavigationService navigation)
    : ParameterHardwareViewModel("rangefinder", vehicle, service, logger, navigation)
{
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(500) };

    /// <summary>Gets the current read-only sensor context.</summary>
    [ObservableProperty]
    public partial string LiveReadings { get; private set; } = "No rangefinder observations.";

    /// <inheritdoc />
    public override async Task ActivateAsync()
    {
        timer.Tick -= Update;
        timer.Tick += Update;
        Update(null, EventArgs.Empty);
        timer.Start();
        await base.ActivateAsync();
    }

    /// <inheritdoc />
    public override Task DeactivateAsync()
    {
        timer.Stop();
        timer.Tick -= Update;
        return base.DeactivateAsync();
    }

    /// <inheritdoc />
    public override void Dispose()
    {
        timer.Stop();
        timer.Tick -= Update;
        base.Dispose();
    }

    private void Update(object? sender, EventArgs args)
    {
        LiveReadings = SetupTelemetrySummary.Rangefinders(ActiveVehicle.State, DateTimeOffset.UtcNow);
    }
}
