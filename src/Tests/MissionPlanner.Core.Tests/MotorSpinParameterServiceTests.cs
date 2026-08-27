using FluentAssertions;
using MissionPlanner.Core.Setup.OptionalHardware.Motor;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies normalized motor-spin recommendations and confirmed writes.</summary>
public sealed class MotorSpinParameterServiceTests
{
    private static readonly VehicleId vehicleId = new(1, 1);

    /// <summary>Verifies percentage and normalized conversion in both directions.</summary>
    [Theory]
    [InlineData(5, 0.05f)]
    [InlineData(10, 0.10f)]
    [InlineData(15, 0.15f)]
    public void ConvertsPercentAndNormalizedValues(double percent, float normalized)
    {
        MotorSpinPercentage.ToNormalized(percent).Should().BeApproximately(normalized, 0.00001f);
        MotorSpinPercentage.ToPercent(normalized).Should().BeApproximately(percent, 0.00001d);
        MotorSpinPercentage.ToWholePercent(normalized).Should().Be((int)percent);
    }

    /// <summary>Verifies binary REAL32 noise does not escape into UI percentage formulas.</summary>
    [Theory]
    [InlineData(0.10000000149011612f, 10)]
    [InlineData(0.15000000596046448f, 15)]
    public void ConvertsNormalizedSpinValuesToWholePercent(float normalized, int expected)
    {
        MotorSpinPercentage.ToWholePercent(normalized).Should().Be(expected);
        var state = new MotorSpinParameterState(normalized, normalized);
        state.SpinArmPercent.Should().Be(expected);
        state.SpinMinPercent.Should().Be(expected);
    }

    /// <summary>Verifies the armed-spin recommendation adds two percentage points.</summary>
    [Fact]
    public void RecommendsSpinArmFromTestThrottle()
    {
        var fixture = CreateFixture(0.05f, 0.13f);

        var recommendation = fixture.Service.RecommendSpinArm(vehicleId, 8);

        recommendation.Success.Should().BeTrue();
        recommendation.Percent.Should().Be(10);
        recommendation.NormalizedValue.Should().BeApproximately(0.10f, 0.00001f);
    }

    /// <summary>Verifies the in-flight minimum recommendation adds three percentage points to normalized spin arm.</summary>
    [Fact]
    public void RecommendsSpinMinFromNormalizedSpinArm()
    {
        var fixture = CreateFixture(0.10f, 0.11f);

        var recommendation = fixture.Service.RecommendSpinMin(vehicleId);

        recommendation.Success.Should().BeTrue();
        recommendation.Percent.Should().BeApproximately(13, 0.00001d);
        recommendation.NormalizedValue.Should().BeApproximately(0.13f, 0.00001f);
    }

    /// <summary>Verifies user-selected positive margins are used in both formulas.</summary>
    [Fact]
    public void RecommendsUserSelectedMargins()
    {
        var fixture = CreateFixture(0.12f, 0.18f);

        fixture.Service.RecommendSpinArm(vehicleId, 10, 4).Percent.Should().Be(14);
        fixture.Service.RecommendSpinMin(vehicleId, 5).Percent.Should().Be(17);
    }

    /// <summary>Verifies margins and calculated totals remain inside the setup safety envelope.</summary>
    [Fact]
    public void RejectsInvalidMarginsAndTotals()
    {
        var fixture = CreateFixture(0.17f, 0.19f);

        fixture.Service.RecommendSpinArm(vehicleId, 10, 0).Success.Should().BeFalse();
        fixture.Service.RecommendSpinArm(vehicleId, 18, 2).Success.Should().BeFalse();
        fixture.Service.RecommendSpinMin(vehicleId, 0).Success.Should().BeFalse();
        fixture.Service.RecommendSpinMin(vehicleId, 3).Success.Should().BeFalse();
    }

    /// <summary>Verifies unsafe ordering and excessive motor-test throttle are rejected before writes.</summary>
    [Fact]
    public async Task RejectsUnsafeRecommendationBeforeParameterWrite()
    {
        var fixture = CreateFixture(0.08f, 0.10f);

        var ordering = await fixture.Service.SetSpinArmAsync(vehicleId, 8, cancellationToken: TestContext.Current.CancellationToken);
        var excessive = await fixture.Service.SetSpinArmAsync(vehicleId, 20, cancellationToken: TestContext.Current.CancellationToken);

        ordering.Success.Should().BeFalse("MOT_SPIN_ARM cannot equal the current MOT_SPIN_MIN");
        excessive.Success.Should().BeFalse();
        await fixture.Parameters.DidNotReceiveWithAnyArgs().SetParameterAsync(default, default!, default, default, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies missing parameters independently disable their operations without sending writes.</summary>
    [Fact]
    public async Task MissingParametersAreUnavailable()
    {
        var armMissing = CreateFixture(null, 0.13f);
        var minMissing = CreateFixture(0.10f, null);

        (await armMissing.Service.SetSpinArmAsync(vehicleId, 8, cancellationToken: TestContext.Current.CancellationToken)).Success.Should().BeFalse();
        (await minMissing.Service.SetSpinMinAsync(vehicleId, cancellationToken: TestContext.Current.CancellationToken)).Success.Should().BeFalse();
        armMissing.Service.GetState(vehicleId).HasSpinArm.Should().BeFalse();
        minMissing.Service.GetState(vehicleId).HasSpinMin.Should().BeFalse();
        await armMissing.Parameters.DidNotReceiveWithAnyArgs().SetParameterAsync(default, default!, default, default, TestContext.Current.CancellationToken);
        await minMissing.Parameters.DidNotReceiveWithAnyArgs().SetParameterAsync(default, default!, default, default, TestContext.Current.CancellationToken);
    }

    /// <summary>Verifies failed writes preserve registry state and can be retried successfully.</summary>
    [Fact]
    public async Task FailedWritePreservesValueAndCanBeRetried()
    {
        var fixture = CreateFixture(0.05f, 0.13f, writeSuccess: false);

        var failed = await fixture.Service.SetSpinArmAsync(vehicleId, 8, cancellationToken: TestContext.Current.CancellationToken);

        failed.Success.Should().BeFalse();
        fixture.Registry.GetParameter(vehicleId, "MOT_SPIN_ARM")!.Value.Should().BeApproximately(0.05f, 0.00001f);

        fixture.Parameters.SetParameterAsync(
                vehicleId,
                "MOT_SPIN_ARM",
                Arg.Any<float>(),
                Arg.Any<MavParamType>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                Store(fixture.Registry, "MOT_SPIN_ARM", call.ArgAt<float>(2));
                return true;
            });

        var retried = await fixture.Service.SetSpinArmAsync(vehicleId, 8, cancellationToken: TestContext.Current.CancellationToken);
        retried.Success.Should().BeTrue();
        fixture.Registry.GetParameter(vehicleId, "MOT_SPIN_ARM")!.Value.Should().BeApproximately(0.10f, 0.00001f);
    }

    /// <summary>Verifies confirmed MOT_SPIN_MIN writes the normalized recommendation.</summary>
    [Fact]
    public async Task WritesConfirmedNormalizedSpinMin()
    {
        var fixture = CreateFixture(0.10f, 0.11f);

        var result = await fixture.Service.SetSpinMinAsync(vehicleId, cancellationToken: TestContext.Current.CancellationToken);

        result.Success.Should().BeTrue();
        fixture.Registry.GetParameter(vehicleId, "MOT_SPIN_MIN")!.Value.Should().BeApproximately(0.13f, 0.00001f);
        await fixture.Parameters.Received(1).SetParameterAsync(
            vehicleId,
            "MOT_SPIN_MIN",
            Arg.Is<float>(value => Math.Abs(value - 0.13f) < 0.00001f),
            MavParamType.Real32,
            Arg.Any<CancellationToken>());
    }

    private static Fixture CreateFixture(float? spinArm, float? spinMin, bool writeSuccess = true)
    {
        var registry = new VehicleParameterRegistry();
        if (spinArm is { } arm)
        {
            Store(registry, "MOT_SPIN_ARM", arm);
        }

        if (spinMin is { } min)
        {
            Store(registry, "MOT_SPIN_MIN", min);
        }

        var metadata = Substitute.For<IVehicleParameterMetadataService>();
        metadata.GetMetadataAsync(vehicleId, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((ParameterMetadata?)null);
        var parameters = Substitute.For<IVehicleParameterService>();
        parameters.SetParameterAsync(vehicleId, Arg.Any<string>(), Arg.Any<float>(), Arg.Any<MavParamType>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                if (writeSuccess)
                {
                    Store(registry, call.ArgAt<string>(1), call.ArgAt<float>(2));
                }

                return writeSuccess;
            });
        return new Fixture(new MotorSpinParameterService(registry, metadata, parameters), registry, parameters);
    }

    private static void Store(VehicleParameterRegistry registry, string name, float value)
    {
        registry.StoreParameter(
            vehicleId,
            new VehicleParameter(name, value, MavParamType.Real32, 0, 2),
            CancellationToken.None);
    }

    private sealed record Fixture(
        MotorSpinParameterService Service,
        VehicleParameterRegistry Registry,
        IVehicleParameterService Parameters);
}
