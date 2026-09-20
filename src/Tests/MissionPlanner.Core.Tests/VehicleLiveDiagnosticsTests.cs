using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.Core.Diagnostics;
using MissionPlanner.Core.DomainEvents;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.Core.Tests;

/// <summary>Exercises always-on diagnostics without any Inspector or Avalonia objects.</summary>
public sealed class VehicleLiveDiagnosticsTests
{
    /// <summary>Journal capacity, vehicle isolation, disconnect retention and high-rate state handling.</summary>
    [Fact]
    public async Task RetainsBoundedEvidenceAndAuthoritativeStateWithoutUi()
    {
        using var hub = new EventHub(NullLogger<EventHub>.Instance);
        using var domain = new DomainEventHub(NullLogger<EventHub>.Instance);
        using var diagnostics = new VehicleLiveDiagnostics(hub, domain, TimeProvider.System,
            Options.Create(new VehicleLiveDiagnosticOptions { JournalCapacity = 3 }));
        var first = new VehicleId(4, 1);
        var second = new VehicleId(8, 1);
        var state = State(first);
        await domain.PublishDomainEventAsync(new VehicleStateUpdated(state), TestContext.Current.CancellationToken);
        await UntilAsync(() => diagnostics.GetSnapshot(first).State is not null);
        for (var i = 0; i < 1000; i++)
        {
            await domain.PublishDomainEventAsync(new VehicleStateUpdated(state), TestContext.Current.CancellationToken);
        }
        await domain.PublishDomainEventAsync(new VehicleDisconnected(first, DateTimeOffset.UtcNow, "TransportFault"),
            TestContext.Current.CancellationToken);
        await UntilAsync(() => diagnostics.GetSnapshot(first).Disconnected);
        Assert.Same(state, diagnostics.GetSnapshot(first).State);
        Assert.True(diagnostics.GetRecentEvents(first).Count <= 3);

        diagnostics.ClearEvents(first);
        Parallel.For(0, 100, i =>
        {
            diagnostics.AddMarker(first, i.ToString());
            _ = diagnostics.GetRecentEvents(first);
            _ = diagnostics.GetSnapshot(first);
        });
        Assert.Equal(3, diagnostics.GetRecentEvents(first).Count);
        diagnostics.AddMarker(second, "other vehicle");
        Assert.Equal("other vehicle", Assert.Single(diagnostics.GetRecentEvents(second)).Message);
        Assert.DoesNotContain(diagnostics.GetRecentEvents(first), item => item.VehicleId == second);
        diagnostics.ClearEvents(first);
        Assert.Empty(diagnostics.GetRecentEvents(first));
        Assert.Same(state, diagnostics.GetSnapshot(first).State);
    }

    /// <summary>Export includes full state and retained payloads beyond the UI limit, isolated by vehicle.</summary>
    [Fact]
    public async Task ClipboardSnapshotIncludesAllRetainedEvidence()
    {
        using var hub = new EventHub(NullLogger<EventHub>.Instance);
        using var domain = new DomainEventHub(NullLogger<EventHub>.Instance);
        using var diagnostics = new VehicleLiveDiagnostics(hub, domain, TimeProvider.System,
            Options.Create(new VehicleLiveDiagnosticOptions { RawCapacity = 300 }));
        var id = new VehicleId(4, 1);
        await domain.PublishDomainEventAsync(new VehicleStateUpdated(State(id)), TestContext.Current.CancellationToken);
        await UntilAsync(() => diagnostics.GetSnapshot(id).State is not null);
        diagnostics.ClearEvents(id);
        diagnostics.AddMarker(id, "export marker");
        diagnostics.AddMarker(new VehicleId(9, 1), "other vehicle");
        for (var index = 0; index < 250; index++)
        {
            var sample = new VehicleRawDiagnostic(id, DateTimeOffset.UtcNow, 4, 1, (uint)index,
                "TEST", $"sample {index}", "0102FF");
            await ((IVehicleTelemetryEventHub)hub).PublishAsync(sample, TestContext.Current.CancellationToken);
            await UntilAsync(() => diagnostics.GetRaw(id).FirstOrDefault()?.MessageId == (uint)index);
        }

        var json = diagnostics.CreateSnapshotJson(id);
        using var document = System.Text.Json.JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal(250, root.GetProperty("Raw").GetArrayLength());
        Assert.Equal(249, root.GetProperty("Raw")[0].GetProperty("MessageId").GetInt32());
        Assert.Equal("0102FF", root.GetProperty("Raw")[0].GetProperty("Payload").GetString());
        Assert.Equal(4, root.GetProperty("Vehicle").GetProperty("State").GetProperty("VehicleId").GetProperty("SystemId").GetInt32());
        Assert.Equal("export marker", root.GetProperty("Events")[0].GetProperty("Message").GetString());
        Assert.Single(root.GetProperty("Events").EnumerateArray());
        Assert.True(root.TryGetProperty("Arming", out _));
        Assert.True(root.TryGetProperty("Parameters", out _));
        Assert.True(root.TryGetProperty("CapturedAt", out _));
        Assert.Equal(300, root.GetProperty("Retention").GetProperty("RawCapacity").GetInt32());
        diagnostics.ClearEvents(id);
        Assert.Contains("export marker", json);
        Assert.DoesNotContain("other vehicle", json);
    }

    internal static VehicleState State(VehicleId id) => new(id, 0, 2, 3, 0, 4, 3,
        VehicleConnectionState.Online, DateTimeOffset.UtcNow, VehicleMode.Unknown, false,
        null, null, null, null, null, null, null, null);

    internal static async Task UntilAsync(Func<bool> condition)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(3));
        while (!condition())
        {
            await Task.Delay(1, timeout.Token);
        }
    }
}
