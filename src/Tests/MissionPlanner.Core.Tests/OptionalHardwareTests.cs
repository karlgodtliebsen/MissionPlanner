using FluentAssertions;
using Microsoft.Extensions.Logging;
using MissionPlanner.Core.Setup;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.MavLink.Parameters;
using NSubstitute;
using MavParamType = MissionPlanner.MavLink.Parameters.MavParamType;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies plugin-style optional-hardware discovery, conflicts, and confirmed edits.</summary>
public sealed class OptionalHardwareTests
{
    private static readonly VehicleId vehicleId = new(1, 1);

    /// <summary>Verifies modules appear only when their parameters are present.</summary>
    [Fact]
    public void ModulesAreDiscoveredByParameterPresence()
    {
        var catalog = new OptionalHardwareCatalog(Modules());
        var parameters = Parameters(("SERIAL1_PROTOCOL", 5), ("GPS_TYPE", 1));

        var available = catalog.GetAvailable(parameters).Select(module => module.Key).ToArray();

        available.Should().Contain("serial").And.Contain("gps");
        available.Should().NotContain("rangefinder", "no rangefinder parameters are present");
    }

    /// <summary>Verifies serial ports sharing an exclusive protocol are flagged before any write.</summary>
    [Fact]
    public void SerialProtocolConflictIsDetected()
    {
        var module = new SerialPortsModule();
        var parameters = Parameters(("SERIAL1_PROTOCOL", 5), ("SERIAL2_PROTOCOL", 5), ("SERIAL3_PROTOCOL", 2));

        var view = module.Build(parameters, Metadata());

        view.Issues.Should().Contain(issue => issue.Message.Contains("share serial protocol 5"));
        view.Issues.Should().NotContain(issue => issue.Message.Contains("protocol 2"), "MAVLink protocols may be shared");
    }

    /// <summary>Verifies sparse rangefinder instances are discovered without a fixed count.</summary>
    [Fact]
    public void SparseRangefinderInstancesAreDiscovered()
    {
        var module = new RangefinderModule();
        var parameters = Parameters(("RNGFND1_TYPE", 1), ("RNGFND3_TYPE", 10));

        module.IsAvailable(parameters).Should().BeTrue();
        var view = module.Build(parameters, Metadata());
        view.Settings.Select(setting => setting.Name).Should().Contain("RNGFND1_TYPE").And.Contain("RNGFND3_TYPE");
        view.Settings.Select(setting => setting.Name).Should().NotContain("RNGFND2_TYPE");
    }

    /// <summary>Verifies only settings belonging to an available module can be written.</summary>
    [Fact]
    public async Task ServiceRejectsUnknownParameterAndWritesKnownOne()
    {
        var registry = new VehicleParameterRegistry();
        Store(registry, "SERIAL1_PROTOCOL", 5);
        var service = CreateService(registry);

        var rejected = await service.SetValueAsync(vehicleId, "NOT_A_MODULE_PARAM", 1, TestContext.Current.CancellationToken);
        rejected.Success.Should().BeFalse();

        var accepted = await service.SetValueAsync(vehicleId, "SERIAL1_PROTOCOL", 23, TestContext.Current.CancellationToken);
        accepted.Success.Should().BeTrue();
        registry.GetParameter(vehicleId, "SERIAL1_PROTOCOL")!.Value.Should().Be(23);
    }

    private static OptionalHardwareService CreateService(VehicleParameterRegistry registry)
    {
        var active = Substitute.For<IActiveVehicleContext>();
        active.VehicleId.Returns(vehicleId);
        active.IsOnline.Returns(true);
        active.State.Returns(State());
        var metadata = Substitute.For<IVehicleParameterMetadataService>();
        metadata.GetAllMetadataAsync(vehicleId, Arg.Any<CancellationToken>()).Returns(new Dictionary<string, ParameterMetadata>());
        var parameterService = Substitute.For<IVehicleParameterService>();
        parameterService.SetParameterAsync(vehicleId, Arg.Any<string>(), Arg.Any<float>(), Arg.Any<MavParamType>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                registry.StoreParameter(vehicleId, new VehicleParameter(call.ArgAt<string>(1), call.ArgAt<float>(2), MavParamType.Int16, 0, 1), CancellationToken.None);
                return Task.FromResult(true);
            });
        return new OptionalHardwareService(active, new OptionalHardwareCatalog(Modules()), registry, metadata, parameterService,
            Substitute.For<ILogger<OptionalHardwareService>>());
    }

    private static IEnumerable<IOptionalHardwareModule> Modules() =>
        [new SerialPortsModule(), new GpsModule(), new RangefinderModule(), new AirspeedModule(), new CanBusModule()];

    private static IReadOnlyDictionary<string, ParameterMetadata> Metadata() => new Dictionary<string, ParameterMetadata>();

    private static void Store(VehicleParameterRegistry registry, string name, float value) =>
        registry.StoreParameter(vehicleId, new VehicleParameter(name, value, MavParamType.Int16, 0, 1), CancellationToken.None);

    private static IReadOnlyDictionary<string, VehicleParameter> Parameters(params (string Name, float Value)[] values) =>
        values.ToDictionary(item => item.Name, item => new VehicleParameter(item.Name, item.Value, MavParamType.Int16, 0, (ushort)values.Length), StringComparer.Ordinal);

    private static VehicleState State()
    {
        var now = DateTimeOffset.UtcNow;
        return new VehicleState(vehicleId, 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, now,
            VehicleMode.Stabilize, false, null, null, null, null, null, null, null, null);
    }
}
