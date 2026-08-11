using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Vehicles.Models;

namespace MissionPlanner.App.Views.Missions;

/// <summary>Projects mission domain state into a Mapsui-independent presentation snapshot.</summary>
public static class MissionMapProjection
{
    /// <summary>Creates marker, route, and bounds data for a mission.</summary>
    public static MissionMapSnapshot Create(Mission mission, GeoPosition? homePosition)
    {
        ArgumentNullException.ThrowIfNull(mission);

        var markers = new List<MissionMapMarker>();
        var route = new List<GeoPosition>();
        if (homePosition is { IsValid: true } home)
        {
            markers.Add(new MissionMapMarker("H: Home", home, MissionMapMarkerKind.Home));
            route.Add(home);
        }

        foreach (var item in mission.Items)
        {
            if (MissionItemListViewModel.PositionOf(item) is not { IsValid: true } position)
            {
                continue;
            }

            markers.Add(new MissionMapMarker(
                $"{item.Sequence + 1}: {item.Command}",
                position,
                MissionMapMarkerKind.MissionItem));
            route.Add(position);
        }

        return new MissionMapSnapshot(markers, route, GeographicCalculations.CalculateBounds(route));
    }
}
