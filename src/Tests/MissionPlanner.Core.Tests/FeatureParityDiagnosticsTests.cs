using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Transport;
using System.Net;
using System.Net.Sockets;
using MissionPlanner.Core.Setup.Advanced.Inspector;
using MissionPlanner.Core.Setup.OptionalHardware;
using MissionPlanner.MavLink.Parameters;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

public sealed class FeatureParityDiagnosticsTests
{
    [Theory]
    [InlineData(360, 3)]
    [InlineData(540, 4)]
    [InlineData(720, 4)]
    public void ExactRtcmFragmentBoundariesTerminate(int length, int count)
    {
        var fragments = GpsRtcmFragmenter.Fragment(new byte[length], 7);
        Assert.Equal(count, fragments.Count);
        Assert.Equal(length, fragments.Sum(fragment => fragment.Data.Length));
        Assert.Equal(length == 720 ? 180 : 0, fragments[^1].Data.Length);
        Assert.All(fragments, fragment => Assert.Equal(7, fragment.Flags >> 3));
    }

    [Fact]
    public void FieldHistoryIsBoundedDeduplicatedAndResetsOnSelection()
    {
        var now = DateTimeOffset.UtcNow;
        var history = new InspectorFieldHistory();
        InspectorDetails Details(int count, string field = "value") => new(
            new(new("Inbound", 1, 1, 30), "ATTITUDE", count, count * 20, now, now.AddMilliseconds(count), 4, 80, 20, 0, "Unsigned"),
            "", new Dictionary<string, string> { [field] = count.ToString(System.Globalization.CultureInfo.InvariantCulture), ["text"] = "hello", ["invalid"] = "NaN" });
        Assert.Equal(new[] { "value" }, InspectorFieldHistory.NumericFields(Details(1)));
        for (var count = 1; count <= 600; count++)
        {
            Assert.True(history.Observe(Details(count), "value"));
            Assert.False(history.Observe(Details(count), "value"));
        }
        Assert.Equal(512, history.Samples.Count);
        Assert.Equal(89, history.Samples[0].Value);
        history.Observe(Details(601, "other"), "other");
        Assert.Single(history.Samples);
        history.Clear();
        Assert.Empty(history.Samples);
    }

    [Fact]
    public void GraphExportPinsSourceUnitsAndInvariantValues()
    {
        var now = DateTimeOffset.Parse("2026-10-24T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var history = new InspectorFieldHistory();
        var row = new InspectorRow(new("Inbound", 5, 7, 30), "ATTITUDE", 1, 20, now, now, 4, 80, 20, 0, "Unsigned");
        Assert.True(history.Observe(new(row, "", new Dictionary<string, string> { ["Roll"] = "-1.25" }), "Roll"));
        Assert.True(history.Observe(new(row with { Count = 2, LastSeen = now.AddSeconds(.5) }, "", new Dictionary<string, string> { ["Roll"] = "2.5" }), "Roll"));
        var csv = System.Text.Encoding.UTF8.GetString(history.ExportCsv());
        Assert.Contains("elapsed_seconds,direction,system_id,component_id,message_id,field,units,value", csv);
        Assert.Contains(",0,\"Inbound\",5,7,30,\"Roll\",\"rad\",-1.25", csv);
        Assert.Contains(",0.5,\"Inbound\",5,7,30,\"Roll\",\"rad\",2.5", csv);
        Assert.Equal("m", InspectorFieldHistory.Units(new("Inbound", 1, 1, 33), "AltitudeMslMeters"));
        history.Clear();
        Assert.Single(System.Text.Encoding.UTF8.GetString(history.ExportCsv()).Split("\r\n", StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void ReportedLoggingAndOsdModulesDoNotInventParameters()
    {
        var parameters = new[] { "INS_LOG_BAT_MASK", "LOG_DISARMED", "SR0_EXTRA1", "SR1_RC_CHAN", "SR1_RANDOM", "GPS_TYPE" }
            .ToDictionary(name => name, name => new VehicleParameter(name, 0, MavParamType.Real32, 0, 6));
        var logging = new FftLoggingModule().Build(parameters, new Dictionary<string, ParameterMetadata>());
        var osd = new ExternalOsdModule().Build(parameters, new Dictionary<string, ParameterMetadata>());
        Assert.Equal(new[] { "INS_LOG_BAT_MASK", "LOG_DISARMED" }, logging.Settings.Select(setting => setting.Name));
        Assert.Equal(new[] { "SR0_EXTRA1", "SR1_RC_CHAN" }, osd.Settings.Select(setting => setting.Name));
    }

    [Fact]
    public async Task TcpSourceReadsActualLocalStreamAndCloses()
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(5));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var factory = new RtkCorrectionSourceFactory(Substitute.For<IDirectSerialSessionFactory>(), Substitute.For<IHttpClientFactory>());
        var accepting = listener.AcceptTcpClientAsync(timeout.Token);
        await using var source = await factory.OpenAsync(new(RtkSourceKind.Tcp, "127.0.0.1", port), timeout.Token);
        using var server = await accepting;
        await server.GetStream().WriteAsync(new byte[] { 0xd3, 1, 2, 3 }, timeout.Token);
        var received = new byte[4];
        var count = 0;
        while (count < received.Length)
        {
            count += await source.ReadAsync(received.AsMemory(count), timeout.Token);
        }
        Assert.Equal(new byte[] { 0xd3, 1, 2, 3 }, received);
    }
    [Fact]
    public void RtcmCrcMatchesIndependentStandardVector()
    {
        Assert.Equal(0xCDE703, Rtcm3Framer.Crc24Q("123456789"u8));
        Assert.Equal(0, Rtcm3Framer.Crc24Q([]));
        var valid = Convert.FromHexString("D300023ED0A4E000");
        // The packet fixture below is supplied separately from the CRC helper.
        Assert.Equal(0xA4E000, Rtcm3Framer.Crc24Q(valid.AsSpan(0, 5)));
        Assert.Single(new Rtcm3Framer().Push(valid));
        valid[1] = 0x80;
        Assert.Empty(new Rtcm3Framer().Push(valid));
    }

    [Fact]
    public async Task UdpSourcePreservesDatagramRemainderAndCancellationReleasesPort()
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(5));
        int port;
        using (var reservation = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0))) port = ((IPEndPoint)reservation.Client.LocalEndPoint!).Port;
        var factory = new RtkCorrectionSourceFactory(Substitute.For<IDirectSerialSessionFactory>(), Substitute.For<IHttpClientFactory>());
        await using (var source = await factory.OpenAsync(new(RtkSourceKind.Udp, "127.0.0.1", port), deadline.Token))
        {
            using var sender = new UdpClient();
            await sender.SendAsync(new byte[] { 1, 2, 3, 4, 5 }, new IPEndPoint(IPAddress.Loopback, port), deadline.Token);
            var part = new byte[3];
            Assert.Equal(3, await source.ReadAsync(part, deadline.Token)); Assert.Equal(new byte[] { 1, 2, 3 }, part);
            Assert.Equal(2, await source.ReadAsync(part, deadline.Token)); Assert.Equal(new byte[] { 4, 5 }, part[..2]);
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => source.ReadAsync(part, cancelled.Token));
        }
        using var reopened = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public async Task CorrectionDistributionUsesCapturedDistinctOnlineEndpoints(bool all, int expectedEndpoints)
    {
        var now = DateTimeOffset.UtcNow;
        var clock = Substitute.For<IDateTimeProvider>(); clock.UtcNow.Returns(now);
        VehicleSession Vehicle(byte system, string endpoint, bool online = true) => new(
            new VehicleState(new(system, 1), 0, 2, 3, 0, 4, 3, online ? VehicleConnectionState.Online : VehicleConnectionState.Offline,
                now, VehicleMode.Stabilize, false, null, null, null, null, null, null, null, null), new(endpoint), clock);
        var targets = new[] { Vehicle(1, "shared"), Vehicle(2, "shared"), Vehicle(3, "other"), Vehicle(4, "offline", false) };
        var active = Substitute.For<IActiveVehicleContext>(); active.IsOnline.Returns(true); active.VehicleId.Returns(targets[0].Id);
        var registry = Substitute.For<IVehicleRegistry>(); registry.Vehicles.Returns(targets);
        var source = new MemoryCorrectionSource(Convert.FromHexString("D300023ED0A4E000"));
        var factory = Substitute.For<IRtkCorrectionSourceFactory>(); factory.OpenAsync(Arg.Any<RtkSourceOptions>(), Arg.Any<CancellationToken>()).Returns(source);
        var transport = Substitute.For<IMavLinkConnection>();
        var endpoints = new List<TransportEndPoint>();
        transport.SendRawAsync(Arg.Any<ReadOnlyMemory<byte>>(), Arg.Any<TransportEndPoint>(), Arg.Any<CancellationToken>()).Returns(call =>
        { endpoints.Add(call.Arg<TransportEndPoint>()); return ValueTask.CompletedTask; });
        var encoder = Substitute.For<IMavLinkWireMessageEncoder>(); encoder.Encode(Arg.Any<GeneratedMavLinkMessage>()).Returns([1]);
        using var service = new RtkInjectionService(factory, active, registry, transport, encoder);
        await service.StartAsync(new(RtkSourceKind.Tcp, "unused", 1234, AllConnectedVehicles: all), TestContext.Current.CancellationToken);
        await service.StopAsync();
        Assert.Equal(expectedEndpoints, endpoints.Count); Assert.Equal(expectedEndpoints, endpoints.Distinct().Count());
        Assert.DoesNotContain(endpoints, endpoint => endpoint == targets[3].EndPoint);
        Assert.True(source.Disposed);
    }

    private sealed class MemoryCorrectionSource(byte[] bytes) : IRtkCorrectionSource
    {
        private readonly MemoryStream stream = new(bytes);
        public bool Disposed { get; private set; }
        public async Task<int> ReadAsync(Memory<byte> buffer, CancellationToken token) => await stream.ReadAsync(buffer, token);
        public ValueTask DisposeAsync() { Disposed = true; stream.Dispose(); return ValueTask.CompletedTask; }
    }

}
