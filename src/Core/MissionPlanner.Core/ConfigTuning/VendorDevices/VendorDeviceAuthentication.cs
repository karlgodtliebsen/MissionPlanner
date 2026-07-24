using System.Text.Json.Serialization;

namespace MissionPlanner.Core.ConfigTuning.VendorDevices;

/// <summary>Supplies optional device authentication while redacting its secret.</summary>
/// <param name="UserName">The non-secret user name.</param>
/// <param name="Secret">The secret, excluded from JSON serialization.</param>
public sealed record VendorDeviceAuthentication(
    string UserName,
    [property: JsonIgnore] string Secret)
{
    /// <inheritdoc />
    public override string ToString()
    {
        return $"UserName = {UserName}, Secret = ***";
    }
}
