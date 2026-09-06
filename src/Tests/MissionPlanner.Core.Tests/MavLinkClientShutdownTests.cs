using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.MavLink.Client;
using MissionPlanner.Transport;
using MissionPlanner.Transport.Abstractions;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

public sealed class MavLinkClientShutdownTests
{
    [Fact]
    public async Task StopClosesTransportBeforeWaitingForReadThatIgnoresCancellation()
    {
        var transport = Substitute.For<IMavLinkTransport>();
        transport.IsConnected.Returns(true);
        var reading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var closed = new TaskCompletionSource<TransportReceiveResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        transport.ReadAsync(Arg.Any<Memory<byte>>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            reading.TrySetResult();
            return new ValueTask<TransportReceiveResult>(closed.Task);
        });
        transport.DisconnectAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            closed.TrySetException(new ObjectDisposedException("serial stream"));
            return Task.CompletedTask;
        });
        await using var client = new MavLinkClient(transport, Options.Create(new MavLinkClientPipelineOptions()),
            Substitute.For<IDateTimeProvider>(), NullLogger<MavLinkClient>.Instance);
        await client.StartAsync(TestContext.Current.CancellationToken);
        await reading.Task.WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        try
        {
            await client.StopAsync().WaitAsync(TimeSpan.FromSeconds(2), TestContext.Current.CancellationToken);
        }
        finally
        {
            // Ensure a regression fails without leaving a blocked reader in the test host.
            closed.TrySetException(new ObjectDisposedException("test cleanup"));
        }
        await transport.Received().DisposeAsync();
    }
}
