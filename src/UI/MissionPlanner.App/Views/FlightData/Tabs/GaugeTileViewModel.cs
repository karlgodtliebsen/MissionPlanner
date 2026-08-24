using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.Core.FlightData.Telemetry;

namespace MissionPlanner.App.Views.FlightData.Tabs;

/// <summary>Provides one stable bindable gauge tile.</summary>
public partial class GaugeTileViewModel(TelemetryFieldDescriptor descriptor) : ObservableObject
{
    /// <summary>Gets the field descriptor.</summary>
    public TelemetryFieldDescriptor Descriptor { get; } = descriptor;
    /// <summary>Gets the label.</summary>
    public string Label => Descriptor.Label;
    /// <summary>Gets the formatted reading.</summary>
    [ObservableProperty] public partial string Value { get; private set; } = "Unavailable";
    /// <summary>Gets the formatted unit.</summary>
    [ObservableProperty] public partial string Unit { get; private set; } = string.Empty;
    /// <summary>Gets the explicit freshness label.</summary>
    [ObservableProperty] public partial string Freshness { get; private set; } = "Unavailable";
    /// <summary>Gets the numeric reading used to position an analog needle.</summary>
    [ObservableProperty]
    public partial double? NumericValue
    {
        get; private set;
    }
    /// <summary>Updates this object without replacing it.</summary>
    public void Update(TelemetryValueSnapshot? snapshot)
    {
        Value = snapshot?.DisplayValue ?? "Unavailable";
        Unit = snapshot?.Unit ?? string.Empty;
        Freshness = snapshot?.Freshness.ToString() ?? "Unavailable";
        NumericValue = snapshot is not null && double.TryParse(
            snapshot.DisplayValue,
            NumberStyles.Float,
            CultureInfo.CurrentCulture,
            out var numericValue)
            ? numericValue
            : null;
    }
}
