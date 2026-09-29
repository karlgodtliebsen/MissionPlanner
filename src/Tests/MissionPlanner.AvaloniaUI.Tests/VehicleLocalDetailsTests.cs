using MissionPlanner.App.Views.InitSetup.MandatoryHardware.Services;
using MissionPlanner.Core.Vehicles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.App.Configuration;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

public sealed class VehicleLocalDetailsTests
{
    [Fact]
    public void ApplicationRegistersOnePersistentVehicleDetailsStore()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ApplicationSettings:Channel"] = "AUTO",
            ["TransportEndpoint:Protocol"] = "udp"
        }).Build();
        var services = new ServiceCollection();
        services.AddApplicationConfiguration(configuration);
        var registration = Assert.Single(services, item => item.ServiceType == typeof(IVehicleLocalDetailsStore));
        Assert.Equal(ServiceLifetime.Singleton, registration.Lifetime);
        Assert.Equal(typeof(JsonVehicleLocalDetailsStore), registration.ImplementationType);
        using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IVehicleLocalDetailsStore>();
        Assert.Same(store, provider.GetRequiredService<IVehicleLocalDetailsStore>());
    }

    internal static VehicleState State(byte id = 1, ulong uid = 42)
    {
        var state = new VehicleState(new VehicleId(id, 1), 0, 2, 3, 0, 0, 3,
            VehicleConnectionState.Online, DateTimeOffset.UtcNow, default, false,
            null, null, null, null, null, null, null, null);
        return state with { Identity = state.Identity with { Firmware = state.Identity.Firmware with { HardwareUid = uid } } };
    }

    [Fact]
    public void PersistedDetailsFollowHardwareAcrossConnectionsAndSystemIds()
    {
        var directory = Path.Combine(Path.GetTempPath(), "mp-details-" + Guid.NewGuid());
        var path = Path.Combine(directory, "details.json");
        try
        {
            var details = new VehicleLocalDetails("BETAFPV Pavo20 Pro", "https://example.com/pavo", "Pavo 20 Pro");
            var store = new JsonVehicleLocalDetailsStore(path);
            store.Save(VehicleLocalDetails.GetKey(State().Identity.Firmware)!, details);
            var reopened = new JsonVehicleLocalDetailsStore(path);
            var session = new VehicleSession(State(16), new TransportEndPoint("COM99"), Substitute.For<IDateTimeProvider>(), reopened);
            Assert.Equal(details, session.State.LocalDetails);
            Assert.Null(new VehicleSession(State(16, 43), new TransportEndPoint("COM99"), Substitute.For<IDateTimeProvider>(), reopened).State.LocalDetails);
            Assert.Null(VehicleLocalDetails.GetKey(State(uid: 0).Identity.Firmware));
            reopened.Save(VehicleLocalDetails.GetKey(State().Identity.Firmware)!, details with { Nickname = "New name" });
            session.ApplyHeartbeat(new(0, 2, 3, 0, 0, 3, DateTimeOffset.UtcNow));
            Assert.Equal("New name", session.State.LocalDetails!.Nickname);
            var awaitingIdentity = new VehicleSession(State(uid: 0), new TransportEndPoint("COM4"), Substitute.For<IDateTimeProvider>(), reopened);
            Assert.Null(awaitingIdentity.State.LocalDetails);
            awaitingIdentity.ApplyFirmwareIdentity(new(0, 0, 0, [], 0, 0, 42, [], DateTimeOffset.UtcNow));
            Assert.Equal("New name", awaitingIdentity.State.LocalDetails!.Nickname);
        }
        finally { if (Directory.Exists(directory)) Directory.Delete(directory, true); }
    }

    [Fact]
    public void MemoryFallbackAndCorruptFileAreHandledWithoutOverwritingExistingData()
    {
        var memory = new JsonVehicleLocalDetailsStore(null);
        var details = new VehicleLocalDetails("Product", "", "Nickname");
        Assert.Contains("session", memory.Save("uid:42", details));
        Assert.Equal(details, memory.Get("uid:42"));
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "broken json");
            var store = new JsonVehicleLocalDetailsStore(path);
            Assert.Contains("preserved", store.Save("uid:42", details));
            Assert.Equal("broken json", File.ReadAllText(path));
        }
        finally { File.Delete(path); }
    }
}
