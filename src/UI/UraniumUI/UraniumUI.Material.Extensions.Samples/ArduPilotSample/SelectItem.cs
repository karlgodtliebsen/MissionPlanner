namespace UraniumUI.Material.Extensions.Samples.ArduPilotSample;

/// <summary>
/// Provides the public API for SelectItem.
/// </summary>
public sealed record SelectItem(string Name, double Value)
{
    /// <inheritdoc />
    public override string ToString()
    {
        return Name;
    }
}
