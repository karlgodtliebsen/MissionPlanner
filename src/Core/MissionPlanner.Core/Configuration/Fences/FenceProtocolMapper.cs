using MissionPlanner.Core.Missions.Models;
using MissionPlanner.MavLink.Generated;
using MissionPlanner.MavLink.Missions;
using MissionItemType = MissionPlanner.MavLink.Missions.MavMissionType;

namespace MissionPlanner.Core.Configuration.Fences;

/// <summary>Maps polygon, circle, and return-point fences through MAV_MISSION_TYPE_FENCE.</summary>
public sealed class FenceProtocolMapper : IFenceProtocolMapper
{
    private const byte GlobalFrame = (byte)MavFrame.Global;

    /// <inheritdoc />
    public IReadOnlyList<MavLinkMissionItem> ToProtocol(FencePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var items = new List<MavLinkMissionItem>();
        if (plan.ReturnPoint is { } returnPoint)
        {
            items.Add(NewItem(items.Count, MavCmd.NavFenceReturnPoint, returnPoint));
        }

        foreach (var area in plan.Areas)
        {
            switch (area.Kind)
            {
                case FenceAreaKind.PolygonInclusion:
                case FenceAreaKind.PolygonExclusion:
                    var polygonCommand = area.Kind == FenceAreaKind.PolygonInclusion
                        ? MavCmd.NavFencePolygonVertexInclusion
                        : MavCmd.NavFencePolygonVertexExclusion;
                    foreach (var vertex in area.Vertices)
                    {
                        items.Add(NewItem(items.Count, polygonCommand, vertex, area.Vertices.Count));
                    }

                    break;
                case FenceAreaKind.CircleInclusion:
                case FenceAreaKind.CircleExclusion:
                    if (area.Center is not { } center)
                    {
                        throw new InvalidOperationException($"Fence circle {area.Id} has no center.");
                    }

                    var circleCommand = area.Kind == FenceAreaKind.CircleInclusion
                        ? MavCmd.NavFenceCircleInclusion
                        : MavCmd.NavFenceCircleExclusion;
                    items.Add(NewItem(items.Count, circleCommand, center, (float)area.RadiusMeters));
                    break;
                default:
                    throw new NotSupportedException($"Fence area kind {area.Kind} is not supported.");
            }
        }

        return items;
    }

    /// <inheritdoc />
    public FenceProtocolParseResult FromProtocol(IReadOnlyList<MavLinkMissionItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        var errors = new List<string>();
        var areas = new List<FenceArea>();
        GeoPosition? returnPoint = null;

        for (var index = 0; index < items.Count;)
        {
            var item = items[index];
            if (item.Sequence != index)
            {
                errors.Add($"Fence item {index} reports sequence {item.Sequence}.");
            }

            if (item.MissionType != MissionItemType.Fence)
            {
                errors.Add($"Item {index} is not a fence mission item.");
                index++;
                continue;
            }

            var command = (MavCmd)item.Command;
            switch (command)
            {
                case MavCmd.NavFenceReturnPoint:
                    if (returnPoint is not null)
                    {
                        errors.Add("The vehicle returned more than one fence return point.");
                    }

                    returnPoint = Position(item);
                    index++;
                    break;
                case MavCmd.NavFencePolygonVertexInclusion:
                case MavCmd.NavFencePolygonVertexExclusion:
                    if (!float.IsFinite(item.Param1) || item.Param1 < 3 || item.Param1 > ushort.MaxValue)
                    {
                        errors.Add($"Polygon at sequence {index} has invalid vertex count {item.Param1}.");
                        index++;
                        break;
                    }

                    var count = (int)Math.Round(item.Param1);
                    if (Math.Abs(item.Param1 - count) > 0.0001f || index + count > items.Count)
                    {
                        errors.Add($"Polygon at sequence {index} has invalid vertex count {item.Param1}.");
                        index++;
                        break;
                    }

                    var polygonItems = items.Skip(index).Take(count).ToArray();
                    if (polygonItems.Any(candidate => candidate.Command != item.Command || candidate.MissionType != MissionItemType.Fence))
                    {
                        errors.Add($"Polygon at sequence {index} is not contiguous.");
                        index++;
                        break;
                    }

                    if (polygonItems.Any(candidate => !float.IsFinite(candidate.Param1) || Math.Abs(candidate.Param1 - count) > 0.0001f))
                    {
                        errors.Add($"Polygon at sequence {index} has inconsistent vertex counts.");
                    }

                    areas.Add(new FenceArea(
                        Guid.NewGuid(),
                        command == MavCmd.NavFencePolygonVertexInclusion ? FenceAreaKind.PolygonInclusion : FenceAreaKind.PolygonExclusion,
                        polygonItems.Select(Position).ToArray(),
                        null,
                        0,
                        true));
                    index += count;
                    break;
                case MavCmd.NavFenceCircleInclusion:
                case MavCmd.NavFenceCircleExclusion:
                    areas.Add(FenceArea.Circle(
                        command == MavCmd.NavFenceCircleInclusion ? FenceAreaKind.CircleInclusion : FenceAreaKind.CircleExclusion,
                        Position(item),
                        item.Param1));
                    index++;
                    break;
                default:
                    errors.Add($"Fence item {index} uses unsupported command {item.Command}.");
                    index++;
                    break;
            }
        }

        var plan = new FencePlan(returnPoint, areas);
        return new FenceProtocolParseResult(plan, errors);
    }

    private static MavLinkMissionItem NewItem(int sequence, MavCmd command, GeoPosition position, float param1 = 0) =>
        new(
            checked((ushort)sequence),
            GlobalFrame,
            (ushort)command,
            false,
            true,
            param1,
            0,
            0,
            0,
            checked((int)Math.Round(position.LatitudeDegrees * 1e7)),
            checked((int)Math.Round(position.LongitudeDegrees * 1e7)),
            0,
            MissionItemType.Fence);

    private static GeoPosition Position(MavLinkMissionItem item) => new(item.X / 1e7, item.Y / 1e7);
}
