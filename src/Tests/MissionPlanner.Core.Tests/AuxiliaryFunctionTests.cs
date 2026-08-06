using FluentAssertions;
using MissionPlanner.Core.FlightData.Auxiliary;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies conservative auxiliary-function catalog and policy behavior.</summary>
public sealed class AuxiliaryFunctionTests
{
    /// <summary>Unknown function IDs remain visible but cannot execute.</summary>
    [Fact]
    public void UnknownFunctionIsExplicitAndUnavailable()
    {
        var descriptor = new AuxiliaryFunctionCatalog().DescribeUnknown(9876);

        descriptor.Id.Should().Be(9876);
        descriptor.Name.Should().Contain("9876");
        descriptor.IsSupported.Should().BeFalse();
        descriptor.Hazard.Should().Be(AuxiliaryFunctionHazard.High);
    }

    /// <summary>Typed workflow ownership prevents generic command duplication.</summary>
    [Fact]
    public void CameraFunctionRedirectsToPayloadControl()
    {
        var vehicle = CreateVehicle();
        var descriptor = new AuxiliaryFunctionCatalog().GetFunctions(vehicle).Single(item => item.Name == "Camera trigger");
        var request = new AuxiliaryFunctionRequest(vehicle, descriptor, default, true);

        new AuxiliaryFunctionPolicy().GetDenialReason(request).Should().Contain("Payload Control");
    }

    /// <summary>Warning functions require explicit operator confirmation.</summary>
    [Fact]
    public void WarningFunctionRequiresConfirmation()
    {
        var vehicle = CreateVehicle();
        var descriptor = new AuxiliaryFunctionCatalog().GetFunctions(vehicle).Single(item => item.Name == "Landing gear");

        new AuxiliaryFunctionPolicy().GetDenialReason(new(vehicle, descriptor, default, false))
            .Should().Contain("confirmation");
        new AuxiliaryFunctionPolicy().GetDenialReason(new(vehicle, descriptor, default, true)).Should().BeNull();
    }

    private static VehicleState CreateVehicle() => new(new VehicleId(1, 1), 0, 2, 3, 0, 4, 3,
        VehicleConnectionState.Online, DateTimeOffset.UtcNow, VehicleMode.Unknown, false,
        null, null, null, null, null, null, null, null);
}
