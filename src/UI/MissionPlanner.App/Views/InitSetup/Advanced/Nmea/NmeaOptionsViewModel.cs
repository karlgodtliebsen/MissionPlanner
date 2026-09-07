using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Core.Setup.Advanced.Nmea;
using MissionPlanner.Library.EventHub.Abstractions;

namespace MissionPlanner.App.Views.InitSetup.Advanced.Nmea;

/// <summary>Owns NMEA sentence selection and validated output frequency.</summary>
public sealed partial class NmeaOptionsViewModel(ILogger<NmeaOptionsViewModel> logger,
    IUiDispatcher dispatcher, IDomainEventHub events) : ViewModelBase(logger, dispatcher, events)
{
    /// <summary>Signals changed immutable options to the active parent.</summary>
    public event Action<NmeaOptions>? OptionsChanged;
    /// <summary>Gets or sets GGA output.</summary>
    [ObservableProperty] public partial bool Gga { get; set; } = true;
    /// <summary>Gets or sets RMC output.</summary>
    [ObservableProperty] public partial bool Rmc { get; set; } = true;
    /// <summary>Gets or sets the scheduled batch rate.</summary>
    [ObservableProperty] public partial int RateHz { get; set; } = 1;
    /// <summary>Gets or sets whether an active session owns the options.</summary>
    [ObservableProperty] public partial bool Locked { get; set; }
    /// <summary>Creates immutable options.</summary>
    public NmeaOptions Options() => new(Gga, Rmc, RateHz);
    partial void OnGgaChanged(bool value) => OptionsChanged?.Invoke(Options());
    partial void OnRmcChanged(bool value) => OptionsChanged?.Invoke(Options());
    partial void OnRateHzChanged(int value) => OptionsChanged?.Invoke(Options());
}
