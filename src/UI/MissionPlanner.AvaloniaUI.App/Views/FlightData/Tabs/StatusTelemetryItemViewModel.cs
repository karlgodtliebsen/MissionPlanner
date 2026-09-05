using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.Core.FlightData.Telemetry;

namespace MissionPlanner.AvaloniaUI.App.Views.FlightData.Tabs;

/// <summary>One stable bindable Status telemetry row.</summary>
public partial class StatusTelemetryItemViewModel(TelemetryFieldDescriptor descriptor) : ObservableObject
{
    /// <summary>Gets its descriptor.</summary>
    public TelemetryFieldDescriptor Descriptor { get; } = descriptor;

    /// <summary>Gets its category.</summary>
    public string Category => Descriptor.Category.ToString();

    /// <summary>Gets its label.</summary>
    public string Label => Descriptor.Label;

    /// <summary>Gets display value.</summary>
    [ObservableProperty]
    public partial string Value { get; private set; } = "Unavailable";

    /// <summary>Gets raw value.</summary>
    [ObservableProperty]
    public partial object? RawValue { get; private set; }

    /// <summary>Gets unit.</summary>
    [ObservableProperty]
    public partial string Unit { get; private set; } = string.Empty;

    /// <summary>Gets freshness.</summary>
    [ObservableProperty]
    public partial string Freshness { get; private set; } = "Unavailable";

    /// <summary>Gets observation time.</summary>
    [ObservableProperty]
    public partial DateTimeOffset? ObservedAt { get; private set; }

    /// <summary>Gets filter visibility.</summary>
    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;

    /// <summary>Updates the row in place.</summary>
    public void Update(TelemetryValueSnapshot? value)
    {
        Value = value?.DisplayValue ?? "Unavailable";
        RawValue = value?.RawValue;
        Unit = value?.Unit ?? "";
        Freshness = value?.Freshness.ToString() ?? "Unavailable";
        ObservedAt = value?.ObservedAt;
    }
}

