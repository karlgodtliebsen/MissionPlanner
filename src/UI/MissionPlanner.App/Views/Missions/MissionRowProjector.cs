using System.Globalization;
using MissionPlanner.Core.Missions;
using MissionPlanner.Core.Missions.Abstractions;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.MavLink.Missions;

namespace MissionPlanner.App.Views.Missions;

/// <summary>The rows projected from a mission together with the derived route totals.</summary>
/// <param name="Rows">The display rows, one per mission item, in sequence order.</param>
/// <param name="TotalDistanceMeters">The summed ground distance of the positioned legs.</param>
public sealed record MissionRowProjection(IReadOnlyList<MissionItemRow> Rows, double TotalDistanceMeters)
{
    /// <summary>Gets the one-line summary shown above the mission list.</summary>
    public string Summary => Rows.Count == 0
        ? "0 items"
        : $"{Rows.Count} items • {TotalDistanceMeters:F0} m total";
}

/// <summary>Projects a mission into editable display rows and converts edited rows back to mission items.</summary>
public interface IMissionRowProjector
{
    /// <summary>Builds one row per mission item with derived distance, azimuth and gradient.</summary>
    /// <param name="mission">The mission to project.</param>
    /// <param name="home">The planned home position the first leg starts from, when known.</param>
    MissionRowProjection Project(Mission mission, GeoPosition? home);

    /// <summary>Converts the edited values of a row into the mission item that replaces the row's source item.</summary>
    /// <param name="row">The edited row.</param>
    /// <param name="sequence">The zero-based position of the item in its mission.</param>
    MissionItem ToMissionItem(MissionItemRow row, int sequence);
}

/// <summary>Default <see cref="IMissionRowProjector"/> backed by the MAVLink protocol mapper.</summary>
public sealed class MissionRowProjector : IMissionRowProjector
{
    private readonly IMissionProtocolMapper protocolMapper;

    /// <summary>Initializes a new instance of the <see cref="MissionRowProjector"/> class.</summary>
    public MissionRowProjector(IMissionProtocolMapper protocolMapper)
    {
        this.protocolMapper = protocolMapper;
    }

    /// <inheritdoc />
    public MissionRowProjection Project(Mission mission, GeoPosition? home)
    {
        ArgumentNullException.ThrowIfNull(mission);

        var rows = new List<MissionItemRow>(mission.Items.Count);
        var previousPosition = home;
        var previousAltitude = 0.0;
        var totalDistance = 0.0;

        foreach (var item in mission.Items)
        {
            var position = MissionItemGeometry.PositionOf(item);
            var altitude = MissionItemGeometry.AltitudeOf(item);
            var protocol = protocolMapper.ToProtocol(item, mission.Type);

            double? distance = null, azimuth = null, gradient = null;
            if (position is { } current && previousPosition is { } previous)
            {
                var legMeters = GeoMath.ApproximateDistanceMeters(
                    previous.LatitudeDegrees, previous.LongitudeDegrees,
                    current.LatitudeDegrees, current.LongitudeDegrees);
                totalDistance += legMeters;
                distance = legMeters;
                azimuth = GeographicCalculations.BearingDegrees(previous, current);

                if (altitude is { } alt && legMeters > 0.5)
                {
                    gradient = (alt.Meters - previousAltitude) / legMeters * 100.0;
                }
            }

            if (position is not null)
            {
                previousPosition = position;
                previousAltitude = altitude?.Meters ?? previousAltitude;
            }

            rows.Add(new MissionItemRow
            {
                Id = item.Id,
                Number = item.Sequence + 1,
                CommandId = protocol.Command,
                Frame = protocol.Frame,
                AutoContinue = protocol.AutoContinue,
                Param1 = FormatParam(protocol.Param1),
                Param2 = FormatParam(protocol.Param2),
                Param3 = FormatParam(protocol.Param3),
                Param4 = FormatParam(protocol.Param4),
                Latitude = position?.LatitudeDegrees,
                Longitude = position?.LongitudeDegrees,
                Altitude = altitude?.Meters,
                Distance = distance,
                Azimuth = azimuth,
                Gradient = gradient,
                // Set the initial selections before notifications are attached so building rows never applies edits.
                SelectedCommand = MissionCommandCatalog.CommandNameFor(protocol.Command),
                SelectedFrame = MissionCommandCatalog.FrameNameFor(protocol.Frame)
            });
        }

        return new MissionRowProjection(rows, totalDistance);
    }

    /// <inheritdoc />
    public MissionItem ToMissionItem(MissionItemRow row, int sequence)
    {
        ArgumentNullException.ThrowIfNull(row);

        var commandId = MissionCommandCatalog.CommandIdFor(row.SelectedCommand) ?? row.CommandId;
        var frameId = MissionCommandCatalog.FrameIdFor(row.SelectedFrame) ?? row.Frame;

        var protocolItem = new MavLinkMissionItem(
            (ushort)sequence,
            frameId,
            commandId,
            false,
            row.AutoContinue,
            ParseParam(row.Param1),
            ParseParam(row.Param2),
            ParseParam(row.Param3),
            // Param4 is yaw/heading where NaN means "not set"; keep an empty cell as NaN.
            ParseParam(row.Param4, float.NaN),
            (int)Math.Round(row.Latitude.HasValue ? row.Latitude.Value * 1e7 : 0.0),
            (int)Math.Round(row.Longitude.HasValue ? row.Longitude.Value * 1e7 : 0.0),
            row.Altitude.HasValue ? (float)row.Altitude.Value : 0.0f,
            MavMissionType.Mission);

        return protocolMapper.FromProtocol(protocolItem);
    }

    private static string FormatParam(float value)
    {
        return float.IsNaN(value) ? string.Empty : value.ToString("0.###", CultureInfo.CurrentCulture);
    }

    private static float ParseParam(string text, float emptyValue = 0f)
    {
        return float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) ? value : emptyValue;
    }
}
