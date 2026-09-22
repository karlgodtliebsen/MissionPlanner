using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Core.Services.Abstractions;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Library.Factory.Domain.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Exercises reconnect against fake transports without opening hardware ports.</summary>
public sealed class VehicleReconnectTests
{
    /// <summary>Captured settings retain baud rate, and stale requests cannot replace a newer connection.</summary>
    [Fact]
    public async Task CapturesSettingsAndProtectsReplacementConnection()
    {
        var fixture = new Fixture();
        await using var service = fixture.Service;
        var connected = await service.ConnectSerialAsync("COM11", 57600, TestContext.Current.CancellationToken);
        Assert.True(connected.Success);
        var target = service.CaptureReconnectTarget()!;
        Assert.Equal("COM11", target.Address);
        Assert.Equal(57600, target.BaudRate);
        Assert.Equal(connected.ConnectionId, target.ConnectionId);
        fixture.Session.ClearReceivedCalls();
        var result = await service.ReconnectAsync(target with { ConnectionId = Guid.NewGuid() },
            cancellationToken: TestContext.Current.CancellationToken);
        Assert.False(result.Success);
        Assert.True(service.IsConnected);
        Assert.DoesNotContain(fixture.Session.ReceivedCalls(), c => c.GetMethodInfo().Name is "DisconnectAsync" or "CreateSerialConnection");
    }

    /// <summary>Reconnect waits after owned teardown, retries the same endpoint and stops after success.</summary>
    [Fact]
    public async Task RetriesOriginalEndpointAfterStabilization()
    {
        var fixture = new Fixture();
        await using var service = fixture.Service;
        await service.ConnectSerialAsync("COM11", 57600, TestContext.Current.CancellationToken);
        var target = service.CaptureReconnectTarget()!;
        var attempts = 0;
        var elapsed = Stopwatch.StartNew();
        fixture.Session.CreateSerialConnection("COM11", 57600, null, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Assert.True(elapsed.Elapsed >= TimeSpan.FromMilliseconds(1800));
                return ++attempts < 3
                    ? Task.FromException<CancellationTokenSource>(new IOException("Port settling"))
                    : Task.FromResult(new CancellationTokenSource());
            });
        var progress = new Messages();
        var result = await service.ReconnectAsync(target, progress, TestContext.Current.CancellationToken);
        Assert.True(result.Success);
        Assert.Equal(3, attempts);
        Assert.Contains(progress.Values, p => p.Contains("attempt 3 of 3"));
        Assert.Contains(progress.Values, p => p.Contains("disconnect completely"));
        Assert.NotEqual(target.ConnectionId, service.CaptureReconnectTarget()!.ConnectionId);
    }

    /// <summary>Cancelling the stabilization delay prevents all transport opens.</summary>
    [Fact]
    public async Task CancellationDuringDelayDoesNotOpenTransport()
    {
        var fixture = new Fixture();
        await using var service = fixture.Service;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        cancellation.CancelAfter(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ReconnectAsync(new(Guid.NewGuid(), "Serial", "COM11", 0, 115200), cancellationToken: cancellation.Token));
        Assert.DoesNotContain(fixture.Session.ReceivedCalls(), c => c.GetMethodInfo().Name == "CreateSerialConnection");
    }

    private sealed class Messages : IProgress<string>
    {
        internal readonly List<string> Values = [];
        public void Report(string value) => Values.Add(value);
    }

    private sealed class Fixture
    {
        internal readonly IVehicleConnectionSession Session = Substitute.For<IVehicleConnectionSession>();
        internal readonly VehicleConnectionService Service;

        internal Fixture()
        {
            var clock = Substitute.For<IDateTimeProvider>();
            clock.UtcNow.Returns(DateTimeOffset.UtcNow);
            var registry = Substitute.For<IVehicleRegistry>();
            var state = new VehicleState(new(1, 1), 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, clock.UtcNow,
                VehicleMode.Unknown, false, null, null, null, null, null, null, null, null);
            registry.Vehicles.Returns(new[] { new VehicleSession(state, new TransportEndPoint("test"), clock) });
            var factory = Substitute.For<IDomainFactory>();
            factory.Create<IMavLinkCommandService, IVehicleConnectionSession>(Session).Returns(Substitute.For<IMavLinkCommandService>());
            var planner = Substitute.For<IPlannerSettingsService>();
            planner.Current.Returns(new PlannerSettings { Legacy = new() { DownloadParametersInBackground = false } });
            Session.CreateSerialConnection(Arg.Any<string>(), Arg.Any<int>(), Arg.Any<Action<TransportEndpoint>?>(), Arg.Any<CancellationToken>())
                .Returns(_ => new CancellationTokenSource());
            Service = new(Session, Substitute.For<IDomainEventHub>(), clock, factory, registry, planner,
                Substitute.For<IVehicleParameterLoadStatusContext>(), NullLogger<VehicleConnectionService>.Instance);
        }
    }
}
