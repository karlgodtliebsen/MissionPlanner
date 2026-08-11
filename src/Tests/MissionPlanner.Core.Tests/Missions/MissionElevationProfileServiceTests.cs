using FluentAssertions;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Planning;

namespace MissionPlanner.Core.Tests;

public sealed class MissionElevationProfileServiceTests
{
    [Fact]
    public async Task Generate_SamplesRouteAndComputesGlobalClearance()
    {
        var mission = MissionWith(MissionAltitudeReference.MeanSeaLevel);
        var profile = await new MissionElevationProfileService(new FakeTerrain(50)).GenerateAsync(new(mission, null, null, 100, 100));
        profile.Samples.Should().HaveCountGreaterThan(2);
        profile.TotalDistanceMeters.Should().BeGreaterThan(500);
        profile.Samples.All(sample => sample.ClearanceMeters >= 50 && sample.ClearanceMeters <= 70).Should().BeTrue();
    }

    [Fact]
    public async Task Generate_LeavesRelativeClearanceUnavailableWithoutHomeMsl()
    {
        var profile = await new MissionElevationProfileService(new FakeTerrain(50)).GenerateAsync(new(MissionWith(MissionAltitudeReference.Home), null, null));
        profile.Samples.All(sample => sample.PlannedMslMeters is null && sample.ClearanceMeters is null).Should().BeTrue();
    }

    [Fact]
    public async Task Generate_RepresentsMissingTerrainAsGaps()
    {
        var profile = await new MissionElevationProfileService(new FakeTerrain(null)).GenerateAsync(new(MissionWith(MissionAltitudeReference.MeanSeaLevel), null, null));
        profile.UnavailableSamples.Should().Be(profile.Samples.Count);
        profile.Samples.All(sample => sample.TerrainElevationMeters is null).Should().BeTrue();
    }

    private static Mission MissionWith(MissionAltitudeReference reference)
    { var mission = new Mission(MissionId.New(), "Profile"); mission.Add(new WaypointMissionItem(MissionItemId.New(), 0, new(56,10), new(100, reference), TimeSpan.Zero)); mission.Add(new WaypointMissionItem(MissionItemId.New(), 0, new(56,10.01), new(120, reference), TimeSpan.Zero)); return mission; }
    private sealed class FakeTerrain(double? value) : IMissionTerrainElevationProvider
    { public ValueTask<double?> GetElevationAsync(GeoPosition position, CancellationToken cancellationToken = default) => ValueTask.FromResult(value); }
}
