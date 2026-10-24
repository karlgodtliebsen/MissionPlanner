using FluentAssertions;
using MissionPlanner.Core.Setup.OptionalHardware;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

public sealed class ExternalSerialToolTests
{
    [Theory]
    [InlineData("S1:SERIAL_SPEED=57", "S1", "SERIAL_SPEED", "57")]
    [InlineData(" S3: NETID=25 ", "S3", "NETID", "25")]
    [InlineData("[3] S15:MAX_WINDOW=131", "S15", "MAX_WINDOW", "131")]
    public void RealSikRegisterNamesAreParsed(string line, string register, string name, string value)
    {
        Assert.True(SikRadioConfigurator.TryParseSetting(line, out var actualRegister, out var actualName, out var actualValue));
        Assert.Equal((register, name, value), (actualRegister, actualName, actualValue));
    }

    [Fact]
    public async Task PairedSikWriteVerifiesRemoteBeforeLocalAndNeverExitsAfterReboot()
    {
        var session = new ScriptedSession("OK", "OK", "30", "OK", "OK", "30", "OK");
        var factory = Substitute.For<IDirectSerialSessionFactory>();
        factory.OpenAsync("test", 57600, Arg.Any<CancellationToken>()).Returns(session);
        await new SikRadioConfigurator(factory).ApplyPairedAsync("test", 57600,
            new Dictionary<string, string> { ["S3"] = "30" }, new Dictionary<string, string> { ["S3"] = "30" }, TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "+++", "RTS3=30\r\n", "RTS3?\r\n", "RT&W\r\n", "ATS3=30\r\n", "ATS3?\r\n", "AT&W\r\n", "RTZ\r\n", "ATZ\r\n" }, session.Writes);
    }

    [Theory]
    [InlineData("NETID", "26")]
    [InlineData("OTHER_MEANING", "25")]
    public async Task ReviewedSikEditsRejectChangedValueOrRegisterMeaning(string name, string value)
    {
        var session = new ScriptedSession("OK", "SiK 2.0", $"[2] S3:{name}={value}", "[2] OK");
        var factory = Substitute.For<IDirectSerialSessionFactory>(); factory.OpenAsync("test", 57600, Arg.Any<CancellationToken>()).Returns(session);
        var expected = new SikRadioSnapshot("SiK 2.0", new Dictionary<string, string> { ["S3"] = "25" }, new Dictionary<string, string>()) { Names = new Dictionary<string, string> { ["S3"] = "NETID" } };
        await Assert.ThrowsAsync<InvalidOperationException>(() => new SikRadioConfigurator(factory).ApplyReviewedAsync("test", 57600, expected,
            new Dictionary<string, string> { ["S3"] = "30" }, new Dictionary<string, string>(), TestContext.Current.CancellationToken));
        Assert.DoesNotContain(session.Writes, command => command.Contains('=') || command.Contains("&W", StringComparison.Ordinal) || command == "ATZ\r\n");
        Assert.Equal("ATO\r\n", session.Writes.Last());
    }

    [Fact]
    public async Task SikRefusedRegisterDoesNotSaveOrReboot()
    {
        var session = new ScriptedSession("OK", "ERROR");
        var factory = Substitute.For<IDirectSerialSessionFactory>();
        factory.OpenAsync("test", 57600, Arg.Any<CancellationToken>()).Returns(session);
        await Assert.ThrowsAsync<IOException>(() => new SikRadioConfigurator(factory).ApplyAsync("test", 57600,
            new Dictionary<string, string> { ["S3"] = "30" }, TestContext.Current.CancellationToken));
        Assert.Equal(new[] { "+++", "ATS3=30\r\n", "ATO\r\n" }, session.Writes);
    }
    [Fact]
    public async Task SikProbeParsesLocalAndRemoteSettings()
    {
        var session = new ScriptedSession("OK", "SiK 2.0", "S0: 25", "OK", "S0: 20", "OK");
        var factory = Substitute.For<IDirectSerialSessionFactory>();
        factory.OpenAsync("COM7", 57600, Arg.Any<CancellationToken>()).Returns(session);

        var result = await new SikRadioConfigurator(factory).ReadAsync("COM7", 57600, TestContext.Current.CancellationToken);

        result.Identity.Should().Be("SiK 2.0");
        result.LocalSettings["S0"].Should().Be("25");
        result.RemoteSettings["S0"].Should().Be("20");
        session.Writes.Should().Equal("+++", "ATI\r\n", "ATI5\r\n", "RTI5\r\n", "ATO\r\n");
    }

    [Theory]
    [InlineData(BluetoothAtDialect.Hc05, "AT+NAME=Plane", "AT+PSWD=1234", "AT+UART=57600,0,0")]
    [InlineData(BluetoothAtDialect.Hc06, "AT+NAMEPlane", "AT+PIN1234", "AT+BAUD57600")]
    public async Task BluetoothApplyUsesDetectedDialectAndDoesNotLogPin(
        BluetoothAtDialect dialect, string nameCommand, string pinCommand, string baudCommand)
    {
        var session = new ScriptedSession("OK", "OK", "OK");
        var factory = Substitute.For<IDirectSerialSessionFactory>();
        factory.OpenAsync("COM8", 9600, Arg.Any<CancellationToken>()).Returns(session);

        await new BluetoothSerialConfigurator(factory).ApplyAsync(
            "COM8",
            new BluetoothModuleSnapshot(dialect, 9600, "module"),
            new BluetoothModuleSettings("Plane", 57600, "1234"),
            TestContext.Current.CancellationToken);

        session.Writes.Should().Equal(nameCommand + "\r\n", pinCommand + "\r\n", baudCommand + "\r\n");
    }

    private sealed class ScriptedSession(params string[] responses) : IDirectSerialSession
    {
        private readonly Queue<string> responses = new(responses);
        public string PortName => "test";
        public List<string> Writes { get; } = [];
        public Task WriteAsync(string value, CancellationToken cancellationToken = default) { Writes.Add(value); return Task.CompletedTask; }
        public Task<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => Task.FromResult(0);
        public Task<string> ReadLineAsync(TimeSpan timeout, CancellationToken cancellationToken = default) => Task.FromResult(responses.Dequeue());
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
