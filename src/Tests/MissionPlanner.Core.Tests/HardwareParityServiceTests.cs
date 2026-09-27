using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

public sealed class HardwareParityServiceTests
{
    [Fact]
    public async Task DiscoversPartialOutput32AndWritesOnlySupportedFields()
    {
        var id = new VehicleId(1, 1);
        var active = Active(id);
        var registry = new VehicleParameterRegistry();
        registry.StoreParameter(id, new("SERVO32_FUNCTION", 33, MavParamType.Int32, 0, 1), CancellationToken.None);
        var parameters = Substitute.For<IVehicleParameterService>();
        parameters.SetParameterAsync(id, "SERVO32_FUNCTION", 34, MavParamType.Int32, Arg.Any<CancellationToken>()).Returns(call =>
        {
            registry.StoreParameter(id, new("SERVO32_FUNCTION", 34, MavParamType.Int32, 0, 1), CancellationToken.None);
            return true;
        });
        var metadata = Substitute.For<IVehicleParameterMetadataService>();
        metadata.GetAllMetadataAsync(id, Arg.Any<CancellationToken>()).Returns(new Dictionary<string, ParameterMetadata>());
        var service = new ServoOutputConfigurationService(active, registry, metadata, parameters,
            Substitute.For<IDateTimeProvider>(), NullLogger<ServoOutputConfigurationService>.Instance);
        var info = Assert.Single((await service.GetConfigurationAsync(id, TestContext.Current.CancellationToken)).Outputs);
        Assert.Equal(32, info.ChannelNumber);
        Assert.Null(info.LivePwm);
        Assert.Equal("FUNCTION", Assert.Single(info.AvailableFields!));
        var result = await service.SetOutputAsync(id, new(32, false, 34, 0, 0, 0) { AvailableFields = info.AvailableFields }, TestContext.Current.CancellationToken);
        Assert.True(result.Success);
        await parameters.Received(1).SetParameterAsync(id, Arg.Any<string>(), Arg.Any<float>(), Arg.Any<MavParamType>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AdsbRequiresMatchingReadbackRatherThanSendSuccess()
    {
        var id = new VehicleId(1, 1);
        var registry = new VehicleParameterRegistry();
        registry.StoreParameter(id, new("ADSB_ENABLE", 0, MavParamType.Int32, 0, 1), CancellationToken.None);
        var parameters = Substitute.For<IVehicleParameterService>();
        parameters.SetParameterAsync(id, "ADSB_ENABLE", 1, MavParamType.Int32, Arg.Any<CancellationToken>()).Returns(true);
        parameters.RequestParameterAsync(id, "ADSB_ENABLE", Arg.Any<CancellationToken>()).Returns(true);
        var service = new AdsbService(Active(id), registry, Substitute.For<IVehicleParameterMetadataService>(), parameters);
        var applying = service.ApplyAsync(id, "ADSB_ENABLE", 1, TestContext.Current.CancellationToken);
        Assert.False(applying.IsCompleted);
        registry.StoreParameter(new VehicleId(2, 1), new("ADSB_ENABLE", 1, MavParamType.Int32, 0, 1), CancellationToken.None);
        Assert.False(applying.IsCompleted);
        registry.StoreParameter(id, new("ADSB_ENABLE", 1, MavParamType.Int32, 0, 1), CancellationToken.None);
        Assert.True((await applying).Success);
    }

    private static IActiveVehicleContext Active(VehicleId id)
    {
        var active = Substitute.For<IActiveVehicleContext>();
        active.VehicleId.Returns(id);
        active.IsOnline.Returns(true);
        active.State.Returns(new VehicleState(id, 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online,
            DateTimeOffset.UtcNow, VehicleMode.Unknown, false, null, null, null, null, null, null, null, null));
        return active;
    }
}
