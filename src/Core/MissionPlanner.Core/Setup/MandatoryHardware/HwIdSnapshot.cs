using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Setup.MandatoryHardware;

/// <summary>Describes one reported hardware identifier.</summary>
/// <param name="Name">The source parameter name.</param>
/// <param name="RawValue">The reported numeric value.</param>
/// <param name="Description">A stable diagnostic representation.</param>
public sealed record HwIdItem(string Name, double RawValue, string Description)
{
    /// <summary>Gets the decoded bus type, or an unavailable marker.</summary>
    public string BusType { get; init; } = "—";
    /// <summary>Gets the bus instance.</summary>
    public string Bus { get; init; } = "—";
    /// <summary>Gets the device address on the bus.</summary>
    public string Address { get; init; } = "—";
    /// <summary>Gets the sensor type or its unknown numeric code.</summary>
    public string DeviceType { get; init; } = "—";
}

/// <summary>Contains hardware identity information available for one vehicle.</summary>
/// <param name="VehicleId">The vehicle identifier.</param>
/// <param name="Board">The reported autopilot board summary.</param>
/// <param name="Firmware">The reported firmware summary.</param>
/// <param name="Items">Reported peripheral identifiers.</param>
public sealed record HwIdSnapshot(VehicleId VehicleId, string Board, string Firmware, IReadOnlyList<HwIdItem> Items);
