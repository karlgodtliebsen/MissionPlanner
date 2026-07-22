namespace MissionPlanner.MavLink.Parameters;

/// <summary>
/// Represents metadata for a vehicle parameter.
/// Provides additional information beyond the raw parameter value.
/// </summary>
/// <param name="Name">The parameter name.</param>
/// <param name="DisplayName">Human-readable display name.</param>
/// <param name="Description">Detailed description of what the parameter does.</param>
/// <param name="Units">Measurement units (e.g., "m/s", "deg", "Hz").</param>
/// <param name="UnitText"></param>
/// <param name="Range">Valid range as "min max" string (e.g., "0 100").</param>
/// <param name="Values">Enumerated values as comma-separated list (e.g., "0:Disabled,1:Enabled").</param>
/// <param name="Bitmask">Bit flags for boolean parameters (e.g., "0:Air-mode,1:Rate Loop Only").</param>
/// <param name="Increment">Suggested increment value for numeric parameters.</param>
/// <param name="UserLevel">User level: "Standard" or "Advanced".</param>
/// <param name="RebootRequired">Whether changing this parameter requires a reboot.</param>
/// <param name="ReadOnly">Whether this parameter is read-only.</param>
public sealed record ParameterMetadata(
    string Name,
    string? DisplayName,
    string? Description,
    string? Units,
    string? UnitText,
    string? Range,
    string? Values,
    string? Bitmask,
    string? Increment,
    string? UserLevel,
    bool RebootRequired,
    bool ReadOnly)
{
    private float? max;
    private float? min;

    /// <summary>
    /// Gets the minimum value from the range, if available.
    /// </summary>
    public float? MinValue
    {
        get
        {
            if (min.HasValue)
            {
                return min.Value;
            }

            if (string.IsNullOrWhiteSpace(Range))
            {
                return null;
            }

            var parts = Range.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            min = parts.Length >= 1 && TryParseMetadataNumber(parts[0], out var m) ? m : null;
            return min;
        }
    }

    /// <summary>
    /// Gets the maximum value from the range, if available.
    /// </summary>
    public float? MaxValue
    {
        get
        {
            if (max.HasValue)
            {
                return max.Value;
            }

            if (string.IsNullOrWhiteSpace(Range))
            {
                return null;
            }

            var parts = Range.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            max = parts.Length >= 2 && TryParseMetadataNumber(parts[1], out var m) ? m : null;
            return max;
        }
    }

    /// <summary>
    /// Gets the increment value as a float, if available.
    /// </summary>
    public float? IncrementValue => string.IsNullOrWhiteSpace(Increment) ? null : TryParseMetadataNumber(Increment, out var increment) ? increment : null;

    /// <summary>
    /// Parses the Values string into a dictionary of value->label mappings.
    /// </summary>
    /// <returns>Dictionary mapping numeric values to their labels.</returns>
    public Dictionary<float, string> GetValueOptions()
    {
        var options = new Dictionary<float, string>();

        if (string.IsNullOrWhiteSpace(Values))
        {
            return options;
        }

        // Format: "0:Disabled,1:Enabled,2:Auto"
        var pairs = Values.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in pairs)
        {
            var parts = pair.Split(':', 2);
            if (parts.Length == 2 && TryParseMetadataNumber(parts[0], out var value))
            {
                options[value] = parts[1].Trim();
            }
        }

        return options;
    }

    /// <summary>
    /// Parses the Bitmask string into a dictionary of bit->label mappings.
    /// </summary>
    /// <returns>Dictionary mapping bit positions to their labels.</returns>
    public Dictionary<int, string> GetBitmaskOptions()
    {
        var options = new Dictionary<int, string>();

        if (string.IsNullOrWhiteSpace(Bitmask))
        {
            return options;
        }

        // Format: "0:Air-mode,1:Rate Loop Only,2:Something Else"
        var pairs = Bitmask.Split(',', StringSplitOptions.RemoveEmptyEntries);
        foreach (var pair in pairs)
        {
            var parts = pair.Split(':', 2);
            if (parts.Length == 2 && int.TryParse(parts[0], out var bit))
            {
                options[bit] = parts[1].Trim();
            }
        }

        return options;
    }

    /// <summary>
    /// Validates a parameter value against the metadata constraints.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public bool IsValueValid(float value)
    {
        if (ReadOnly)
        {
            return false;
        }

        // Check range
        return (!min.HasValue || !(value < min.Value)) && (!max.HasValue || value <= max.Value);
    }

    /// <summary>
    /// Gets a validation error message for an invalid value.
    /// </summary>
    /// <param name="value">The value to validate.</param>
    /// <returns>Error message, or null if valid.</returns>
    public string? GetValidationError(float value)
    {
        return ReadOnly
            ? "This parameter is read-only and cannot be modified."
            : min.HasValue && value < min.Value
                ? $"Value must be at least {min.Value}."
                : max.HasValue && value > max.Value
                    ? $"Value must be at most {max.Value}."
                    : null;
    }

    private static bool TryParseMetadataNumber(string value, out float result) =>
        float.TryParse(
            value,
            System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture,
            out result);
}
