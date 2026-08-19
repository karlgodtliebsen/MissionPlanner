using FluentAssertions;
using MissionPlanner.Core.Setup.OptionalHardware;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

public sealed class ExternalSerialToolTests
{
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
        session.Writes.Should().Equal("+++", "ATI\r\n", "ATI5\r\n", "RTI5\r\n");
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
        public Task<string> ReadLineAsync(TimeSpan timeout, CancellationToken cancellationToken = default) => Task.FromResult(responses.Dequeue());
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
