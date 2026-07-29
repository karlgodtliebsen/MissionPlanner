using FluentAssertions;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Profiles;
using MissionPlanner.Core.ConfigTuning.Comparison;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Parameters;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies versioned atomic JSON parameter profile persistence.</summary>
public sealed class ParameterProfileRepositoryTests
{
    /// <summary>Review warns for mismatched firmware and retains unsupported profile entries.</summary>
    [Fact]
    public void ReviewReportsCompatibilityWithoutWriting()
    {
        var firmware = new VehicleFirmwareIdentity(
            FirmwareFamily.ArduCopter, 2, 3,
            new FirmwareSemanticVersion(4, 6, 0, FirmwareReleaseType.Official),
            null, 0, 0, 0, 0, null, null);
        var session = Substitute.For<IParameterEditSession>();
        session.Scope.Returns(new ParameterEditScope(new VehicleId(1, 1), firmware));
        session.VehicleId.Returns(new VehicleId(1, 1));
        session.Fields.Returns([
            new ParameterEditField(
                "GAIN", MavParamType.Real32, 1, 1, 1,
                new ParameterFieldMetadata("Gain", null, null, null, null, 0.1, false, false, [], []),
                null)
        ]);
        var profile = new ParameterProfile(
            Guid.NewGuid(), "Plane", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            FirmwareFamily.ArduPlane, null, 1, "file",
            new Dictionary<string, double> { ["GAIN"] = 2, ["UNKNOWN"] = 3 }, []);
        var service = new ParameterProfileService(
            new ParameterComparisonService(new ParameterValueEquivalence()));

        var review = service.Review(profile, session);

        review.Warnings.Should().NotBeEmpty();
        review.Comparison.Rows.Single(row => row.Name == "GAIN").CanStage.Should().BeTrue();
        review.Comparison.Rows.Single(row => row.Name == "UNKNOWN").Status.Should().Be(ParameterComparisonStatus.OnlyOnRight);
        session.DidNotReceiveWithAnyArgs().ApplyAsync(
            default(IReadOnlyList<string>),
            TestContext.Current.CancellationToken);
    }

    /// <summary>A replacement round-trips without leaving temporary documents.</summary>
    [Fact]
    public async Task SaveReplacesProfileAtomically()
    {
        var directory = Path.Combine(Path.GetTempPath(), "MissionPlanner.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var repository = new JsonParameterProfileRepository(
                Options.Create(new ParameterProfileRepositoryOptions { Directory = directory }));
            var now = DateTimeOffset.UtcNow;
            var profile = new ParameterProfile(
                Guid.NewGuid(), "Copter defaults", "Test", now, now, null, null, null,
                "unit-test", new Dictionary<string, double> { ["GAIN"] = 1 }, ["test"]);
            await repository.SaveAsync(profile, TestContext.Current.CancellationToken);
            await repository.SaveAsync(
                profile with { UpdatedAt = now.AddMinutes(1), Values = new Dictionary<string, double> { ["GAIN"] = 2 } },
                TestContext.Current.CancellationToken);

            var loaded = await repository.GetAsync(profile.Id, TestContext.Current.CancellationToken);
            var renamed = await repository.RenameAsync(profile.Id, "Renamed", TestContext.Current.CancellationToken);
            var duplicate = await repository.DuplicateAsync(profile.Id, "Copy", TestContext.Current.CancellationToken);

            loaded.Should().NotBeNull();
            loaded!.Values["GAIN"].Should().Be(2);
            renamed.Name.Should().Be("Renamed");
            duplicate.Id.Should().NotBe(profile.Id);
            duplicate.Values["GAIN"].Should().Be(2);
            Directory.EnumerateFiles(directory, "*.tmp").Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
