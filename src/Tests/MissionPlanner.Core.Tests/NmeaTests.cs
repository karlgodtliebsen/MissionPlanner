using System.Globalization;
using System.Text;
using System.Threading.Channels;
using MissionPlanner.Core.Setup.Advanced.Nmea;
using MissionPlanner.Core.Setup.Advanced.Output;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Handlers;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Firmware;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.Shared.Models.Vehicles.Models;
using MissionPlanner.Test.Support;
using MissionPlanner.Transport;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

public sealed class NmeaTests
{
    private static readonly DateTimeOffset now = DateTimeOffset.Parse("1994-03-23T13:35:19+01:00");

    [Theory]
    [InlineData(1, 1, "N", "E", "69", "3F")]
    [InlineData(-1, 1, "S", "E", "74", "22")]
    [InlineData(1, -1, "N", "W", "7B", "2D")]
    [InlineData(-1, -1, "S", "W", "66", "30")]
    public void GoldenSentencesAreInvariantAndUseCorrectHemispheres(int latitudeSign, int longitudeSign, string ns, string ew, string ggaChecksum, string rmcChecksum)
    {
        var prior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("da-DK");
            var state = State();
            state = state with { Gps = state.Gps with { LatitudeDegrees = state.Gps.LatitudeDegrees * latitudeSign, LongitudeDegrees = state.Gps.LongitudeDegrees * longitudeSign } };
            var batch = NmeaFormatter.Format(state, now, new());
            Assert.Equal($"$GPGGA,123519.00,4807.0380,{ns},01131.0000,{ew},1,08,0.9,545.4,M,46.9,M,,*{ggaChecksum}\r\n"
                + $"$GPRMC,123519.00,A,4807.0380,{ns},01131.0000,{ew},22.40,84.40,230394,,*{rmcChecksum}\r\n", batch.Text);
            Assert.True(batch.ValidFix);
            Assert.Equal(2, batch.SentenceCount);
        }
        finally { CultureInfo.CurrentCulture = prior; }
    }

    [Fact]
    public void CoordinateRoundingCarriesAndMidnightUsesUtcDate()
    {
        Assert.Equal("1300.0000", NmeaFormatter.Coordinate(12 + 59.99999999 / 60, false));
        Assert.Equal("18000.0000", NmeaFormatter.Coordinate(-179.9999999999, true));
        Assert.Equal("9000.0000", NmeaFormatter.Coordinate(90, false));
        Assert.Equal("00000.0000", NmeaFormatter.Coordinate(0, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => NmeaFormatter.Coordinate(90.1, false));
        var midnight = DateTimeOffset.Parse("2026-01-01T01:00:00+01:00");
        var state = State() with { Gps = State().Gps with { ObservedAt = midnight } };
        var fields = NmeaFormatter.Format(state, midnight, new(Gga: false)).Text.Split(',');
        Assert.Equal("000000.00", fields[1]);
        Assert.Equal("010126", fields[9]);
    }

    [Fact]
    public void MissingMeasurementsRemainBlankAndStaleFixIsVoid()
    {
        var state = State();
        state = state with { Gps = state.Gps with { HorizontalDilution = null, SatellitesUsed = null, GroundSpeedMetersPerSecond = null,
            CourseDegrees = null, AltitudeMslMeters = null, GeoidSeparationMeters = null } };
        var lines = NmeaFormatter.Format(state, now, new()).Text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        var gga = lines[0].Split(',');
        Assert.Equal("1", gga[6]);
        Assert.Empty(gga[7]);
        Assert.Empty(gga[8]);
        Assert.Empty(gga[9]);
        Assert.Empty(gga[11]);
        var rmc = lines[1].Split(',');
        Assert.Empty(rmc[7]);
        Assert.Empty(rmc[8]);
        var stale = NmeaFormatter.Format(state, now.AddSeconds(4), new());
        Assert.False(stale.ValidFix);
        Assert.Equal("0", stale.Text.Split("\r\n")[0].Split(',')[6]);
        Assert.Equal("V", stale.Text.Split("\r\n")[1].Split(',')[2]);
        Assert.DoesNotContain("4807", stale.Text);
        Assert.False(NmeaFormatter.Format(null, now, new()).ValidFix);
        Assert.False(NmeaFormatter.Format(state, now.AddSeconds(-1), new()).ValidFix);
        Assert.Throws<ArgumentException>(() => NmeaFormatter.Format(state, now, new(false, false)));
        Assert.Throws<ArgumentException>(() => NmeaFormatter.Format(state, now, new(RateHz: 11)));
    }

    [Theory]
    [InlineData((byte)2, GpsFixType.Fix2D, "1")]
    [InlineData((byte)3, GpsFixType.Fix3D, "1")]
    [InlineData((byte)4, GpsFixType.DifferentialGps, "2")]
    [InlineData((byte)5, GpsFixType.RtkFloat, "5")]
    [InlineData((byte)6, GpsFixType.RtkFixed, "4")]
    public async Task AuthoritativeGpsHandlerRetainsReceiverFieldsAndActualSatelliteUsage(byte wireFix, GpsFixType expected, string quality)
    {
        var endpoint = new TransportEndPoint("test");
        var session = new VehicleSession(State(), endpoint, Substitute.For<IDateTimeProvider>());
        var registry = Substitute.For<IVehicleRegistry>();
        registry.GetRequired(session.Id).Returns(session);
        var handler = new NavigationTelemetryHandler(registry, Substitute.For<IDomainEventHub>());
        var used = new byte[20];
        used[0] = used[1] = used[2] = 1;
        await handler.HandleAsync(new GpsStatusMessage(1, 1, endpoint, 5, new byte[20], used, new byte[20], new byte[20], new byte[20], now), TestContext.Current.CancellationToken);
        await handler.HandleAsync(new GpsRawIntMessage(1, 1, endpoint, 0, wireFix, 48.1173, 11.5166666667, 545.4,
            90, 100, 100, 9000, 9, 592.3, null, null, null, null, null, now), TestContext.Current.CancellationToken);
        Assert.Equal(expected, session.State.Gps.FixType);
        Assert.Equal(3, session.State.Gps.SatellitesUsed);
        Assert.Equal(9, session.State.Gps.SatellitesVisible);
        Assert.Equal(46.9, session.State.Gps.GeoidSeparationMeters!.Value, 6);
        var fields = NmeaFormatter.Format(session.State, now, new()).Text.Split("\r\n")[0].Split(',');
        Assert.Equal(quality, fields[6]);
        Assert.Equal("03", fields[7]);
        await handler.HandleAsync(new GpsStatusMessage(1, 1, endpoint, 30, new byte[20], used, new byte[20], new byte[20], new byte[20], now.AddSeconds(4)), TestContext.Current.CancellationToken);
        Assert.Null(session.State.Gps.SatellitesUsed); // Truncated status cannot establish total satellites used.
        Assert.False(NmeaFormatter.Format(session.State, now.AddSeconds(4), new()).ValidFix);
    }

    [Fact]
    public async Task FakeClockSchedulesOneBatchPerTickAndTenRestartsLeaveNoTimers()
    {
        var clock = new ManualTimeProvider(now);
        var active = Substitute.For<IActiveVehicleContext>();
        active.IsOnline.Returns(true);
        active.VehicleId.Returns(State().VehicleId);
        active.State.Returns(State());
        var sink = new Sink();
        var output = new BoundedOutputSession(new Factory(sink), new(), clock);
        var session = new NmeaSession(active, output, clock);
        for (var cycle = 0; cycle < 10; cycle++)
        {
            await session.StartAsync(new(OutputEndpointKind.Udp, "127.0.0.1"), new(), new(RateHz: 2), TestContext.Current.CancellationToken);
            clock.Advance(TimeSpan.FromMilliseconds(499));
            Assert.Equal(0, session.ScheduledSentences);
            clock.Advance(TimeSpan.FromMilliseconds(1));
            var bytes = await sink.Writes.Reader.ReadAsync(TestContext.Current.CancellationToken);
            Assert.Contains("$GPGGA", Encoding.ASCII.GetString(bytes));
            Assert.Equal(2, session.ScheduledSentences);
            clock.Advance(TimeSpan.FromMilliseconds(500));
            await sink.Writes.Reader.ReadAsync(TestContext.Current.CancellationToken);
            await session.StopAsync();
            Assert.Equal(4, session.WrittenSentences);
            Assert.Equal(0, session.DroppedSentences);
            Assert.Equal(0, clock.TimerCount);
            Assert.Empty(session.Preview.Text);
        }
    }

    [Fact]
    public async Task EndpointFailureStopsSchedulerAndReportsDrops()
    {
        var clock = new ManualTimeProvider(now);
        var active = Substitute.For<IActiveVehicleContext>();
        active.IsOnline.Returns(true);
        active.VehicleId.Returns(State().VehicleId);
        active.State.Returns(State());
        var sink = new Sink { Fail = true };
        var output = new BoundedOutputSession(new Factory(sink), new(), clock);
        var session = new NmeaSession(active, output, clock);
        await session.StartAsync(new(OutputEndpointKind.Udp, "127.0.0.1"), new(), new(), TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromSeconds(1));
        await output.Completion.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await session.StopAsync();
        Assert.Equal("Faulted", session.Snapshot().State);
        Assert.Equal(2, session.DroppedSentences);
        Assert.Equal(0, session.WrittenSentences);
        Assert.Equal(0, clock.TimerCount);
    }

    private static VehicleState State()
    {
        var state = new VehicleState(new VehicleId(1, 1), 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, now,
            VehicleMode.Unknown, false, null, null, null, null, null, null, null, null);
        return state with { Gps = new(GpsFixType.Fix3D, 12, .9, null, 22.4 * 1852 / 3600, 84.4, null, null, now)
        {
            LatitudeDegrees = 48.1173, LongitudeDegrees = 11 + 31d / 60, AltitudeMslMeters = 545.4,
            GeoidSeparationMeters = 46.9, SatellitesUsed = 8, SatelliteUsageObservedAt = now
        } };
    }
    private sealed class Factory(Sink sink) : IOutputSinkFactory
    {
        public string? UnavailableReason(OutputEndpoint endpoint) => null;
        public Task<IOutputSink> OpenAsync(OutputEndpoint endpoint, CancellationToken token) => Task.FromResult<IOutputSink>(sink);
    }
    private sealed class Sink : IOutputSink
    {
        public bool Fail { get; init; }
        public Channel<byte[]> Writes { get; } = Channel.CreateUnbounded<byte[]>();
        public Task WriteAsync(ReadOnlyMemory<byte> data, CancellationToken token)
        {
            if (Fail) { throw new IOException("Output failed."); }
            Writes.Writer.TryWrite(data.ToArray());
            return Task.CompletedTask;
        }
        public void Abort() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
