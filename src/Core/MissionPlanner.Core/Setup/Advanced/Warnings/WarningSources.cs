using System.Globalization;
using MissionPlanner.Core.FlightData.Telemetry;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.Core.Setup.Advanced.Warnings;

/// <summary>Explicit numeric source metadata backed by an authoritative telemetry projection.</summary>
public sealed record WarningSourceDescriptor(TelemetryFieldDescriptor Field)
{
    /// <summary>Gets the stable telemetry key.</summary>
    public string Key => Field.Key;
    /// <summary>Gets the display name.</summary>
    public string Label => Field.Label;
    /// <summary>Gets the evaluated value type.</summary>
    public Type ValueType => typeof(double);
    /// <summary>Gets whether this source may be absent.</summary>
    public bool CanBeUnavailable => true;
    /// <summary>Gets the maximum accepted observation age.</summary>
    public TimeSpan MaximumAge => TimeSpan.FromSeconds(3);
    /// <summary>Gets native units; application display-unit preferences do not change stored thresholds.</summary>
    public string UnitKind => Field.UnitKind switch
    {
        "angle" => "degrees",
        "speed" => "m/s",
        "distance" => "m",
        "voltage" => "V",
        "current" => "A",
        "percent" => "%",
        _ => Field.UnitKind
    };
}

/// <summary>Projects numeric sources from the existing promoted-state catalogue.</summary>
public sealed class WarningSources(ITelemetryFieldCatalog catalogue)
{
    /// <summary>Gets supported numeric fields. Text, Boolean and enum sources are deliberately excluded.</summary>
    public IReadOnlyList<WarningSourceDescriptor> All { get; } = catalogue.Fields
        .Where(field => field.UnitKind != "text").Select(field => new WarningSourceDescriptor(field)).ToArray();

    /// <summary>Reads a numeric value without reflecting over presentation objects.</summary>
    public WarningSample Read(string key, VehicleState? state)
    {
        var source = All.FirstOrDefault(item => item.Key == key);
        if (state is null || source is null)
        {
            return new(null, null);
        }
        var value = source.Field.Value(state);
        return value is byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal
            ? new(Convert.ToDouble(value, CultureInfo.InvariantCulture), source.Field.ObservedAt(state))
            : new(null, source.Field.ObservedAt(state));
    }

    /// <summary>Validates every persisted/evaluated rule and returns field-specific explanations.</summary>
    public IReadOnlyDictionary<string, string> Validate(WarningRule rule)
    {
        var errors = new Dictionary<string, string>();
        if (rule.Id == Guid.Empty)
        {
            errors[nameof(rule.Id)] = "A stable rule identity is required.";
        }
        if (string.IsNullOrWhiteSpace(rule.Name) || rule.Name.Length > 100)
        {
            errors[nameof(rule.Name)] = "Use a name of 1 to 100 characters.";
        }
        if (!All.Any(item => item.Key == rule.Source))
        {
            errors[nameof(rule.Source)] = "Select a supported numeric telemetry source.";
        }
        if (!Enum.IsDefined(rule.Comparison) || !Enum.IsDefined(rule.Severity))
        {
            errors[nameof(rule.Comparison)] = "Select a supported comparison and severity.";
        }
        if (!double.IsFinite(rule.Threshold) || !double.IsFinite(rule.UpperThreshold))
        {
            errors[nameof(rule.Threshold)] = "Thresholds must be finite numbers.";
        }
        if (rule.Comparison is WarningComparison.InsideRange or WarningComparison.OutsideRange
            && rule.UpperThreshold <= rule.Threshold)
        {
            errors[nameof(rule.UpperThreshold)] = "The upper threshold must exceed the lower threshold.";
        }
        if (!double.IsFinite(rule.DelaySeconds) || rule.DelaySeconds < 0 || rule.DelaySeconds > 86400)
        {
            errors[nameof(rule.DelaySeconds)] = "Use an activation delay between 0 and 86400 seconds.";
        }
        if (!double.IsFinite(rule.CooldownSeconds) || rule.CooldownSeconds < 1 || rule.CooldownSeconds > 86400)
        {
            errors[nameof(rule.CooldownSeconds)] = "Use a repeat interval between 1 and 86400 seconds.";
        }
        if (!double.IsFinite(rule.Hysteresis) || rule.Hysteresis < 0
            || rule.Comparison == WarningComparison.OutsideRange && rule.Hysteresis * 2 >= rule.UpperThreshold - rule.Threshold)
        {
            errors[nameof(rule.Hysteresis)] = "Use non-negative hysteresis smaller than half an outside range's width.";
        }
        if (string.IsNullOrWhiteSpace(rule.Message) || rule.Message.Length > 500)
        {
            errors[nameof(rule.Message)] = "Use a message of 1 to 500 characters; {name} and {value} are supported.";
        }
        return errors;
    }
}
