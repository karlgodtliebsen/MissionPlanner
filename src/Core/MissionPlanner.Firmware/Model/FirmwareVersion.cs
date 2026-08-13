namespace MissionPlanner.Firmware.Model;

/// <summary>Represents normalized and source firmware version data.</summary>
public sealed record FirmwareVersion
{
    /// <inheritdoc />
    public bool Equals(FirmwareVersion? other)
    {
        if (other is null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (!GetHashCode().Equals(other.GetHashCode()))
        {
            return false;
        }

        //do not reformat
        return Value == other.Value && Equals(SemanticVersion, other.SemanticVersion);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(Value, SemanticVersion);
    }

    /// <summary>Initializes a firmware version.</summary>
    public FirmwareVersion(string value, Version? semanticVersion = null)
    {
        Value = RequireText(value, nameof(value));
        SemanticVersion = semanticVersion;
    }

    /// <summary>Gets the source version text.</summary>
    public string Value { get; }

    /// <summary>Gets the parsed semantic version when one was available.</summary>
    public Version? SemanticVersion { get; }

    private static string RequireText(string value, string parameterName)
    {
        return string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A firmware version is required.", parameterName)
            : value.Trim();
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"Ver: {Value}- SemVer: {SemanticVersion}";
    }
}
