using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.Firmware.Devices;
using MissionPlanner.Firmware.Discovery;
using MissionPlanner.Firmware.Entry;
using MissionPlanner.Firmware.Exceptions;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Protocol;
using MissionPlanner.Firmware.Operations;

namespace MissionPlanner.Firmware.Tests;

public sealed class BootloaderEntryStrategyTests
{
    [Fact]
    public async Task AlreadyInBootloaderReturnsIdentifiedClient()
    {
        var discovered = new DiscoveredBootloader(Device(), new BootloaderIdentity(50, 4, 1024), new NoOpClient());
        var strategy = new AlreadyInBootloaderEntryStrategy(new FakeDiscovery(discovered));

        var result = await strategy.TryEnterAsync(Context(), TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(BootloaderEntryOutcome.BootloaderIdentified);
        result.Bootloader.Should().BeSameAs(discovered);
        await discovered.DisposeAsync();
    }

    [Fact]
    public async Task ActiveSessionDeviceIsNotProbedWithBootloaderProtocol()
    {
        var discovery = new CountingDiscovery();
        var strategy = new AlreadyInBootloaderEntryStrategy(discovery);

        var result = await strategy.TryEnterAsync(Context(applicationDevice: Device(), active: true), TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(BootloaderEntryOutcome.NotApplicable);
        result.Code.Should().Be("entry.port-owned-by-vehicle-session");
        discovery.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task TemporaryMavLinkReleasesOwnershipBeforeDiscoveryContinues()
    {
        var gateway = new FakeTemporaryGateway();
        var strategy = new TemporaryMavLinkRebootEntryStrategy(gateway);

        var result = await strategy.TryEnterAsync(Context(applicationDevice: Device()), TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(BootloaderEntryOutcome.ContinueDiscovery);
        gateway.ChannelDisposedBeforeReturn.Should().BeTrue();
        gateway.CallCount.Should().Be(1);
    }

    [Fact]
    public async Task TemporaryMavLinkDoesNotCompeteWithActiveSession()
    {
        var gateway = new FakeTemporaryGateway();
        var strategy = new TemporaryMavLinkRebootEntryStrategy(gateway);

        var result = await strategy.TryEnterAsync(Context(Device(), true), TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(BootloaderEntryOutcome.NotApplicable);
        gateway.CallCount.Should().Be(0);
    }

    [Fact]
    public async Task TemporaryMavLinkPortOwnershipConflictIsRecoverable()
    {
        var strategy = new TemporaryMavLinkRebootEntryStrategy(new ThrowingTemporaryGateway());

        var result = await strategy.TryEnterAsync(Context(applicationDevice: Device()), TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(BootloaderEntryOutcome.ContinueDiscovery);
        result.Code.Should().Be("entry.temporary-mavlink-failed");
    }

    [Fact]
    public async Task ManualStrategyPublishesInteractionRequest()
    {
        var interaction = new FakeInteraction();
        var strategy = new ManualReconnectBootloaderEntryStrategy(interaction);

        var result = await strategy.TryEnterAsync(Context(), TestContext.Current.CancellationToken);

        result.Outcome.Should().Be(BootloaderEntryOutcome.ContinueDiscovery);
        interaction.Code.Should().Be(FirmwareInteractionCodes.ManualBootloaderReconnect);
    }

    [Fact]
    public async Task ManualStrategyRejectionCancelsBeforeDiscovery()
    {
        var interaction = new FakeInteraction(accepted: false);
        var strategy = new ManualReconnectBootloaderEntryStrategy(interaction);

        var act = () => strategy.TryEnterAsync(Context(), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<OperationCanceledException>()
            .WithMessage("*operator rejected*");
    }

    [Fact]
    public async Task ManualStrategyPropagatesExternalCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var strategy = new ManualReconnectBootloaderEntryStrategy(new FakeInteraction());

        var act = () => strategy.TryEnterAsync(Context(), cancellation.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RecoverableFailurePermitsNextStrategy()
    {
        var calls = new List<int>();
        var discovered = new DiscoveredBootloader(Device(), new BootloaderIdentity(50, 4, 1024), new NoOpClient());
        var service = new BootloaderEntryService(
            [new ScriptedStrategy(200, BootloaderEntryOutcome.ContinueDiscovery, calls), new ScriptedStrategy(100, BootloaderEntryOutcome.Failed, calls)],
            new FakeDiscovery(discovered),
            NullLogger<BootloaderEntryService>.Instance);

        var result = await service.EnterAsync(Context(), TestContext.Current.CancellationToken);

        calls.Should().Equal(100, 200);
        result.Outcome.Should().Be(BootloaderEntryOutcome.BootloaderIdentified);
        await discovered.DisposeAsync();
    }

    [Fact]
    public async Task DiscoveryFailureAfterRebootPermitsManualFallback()
    {
        var calls = new List<int>();
        var discovery = new SequencedDiscovery();
        var service = new BootloaderEntryService(
            [
                new ScriptedStrategy(200, BootloaderEntryOutcome.ContinueDiscovery, calls),
                new ScriptedStrategy(300, BootloaderEntryOutcome.ContinueDiscovery, calls)
            ],
            discovery,
            NullLogger<BootloaderEntryService>.Instance);

        var result = await service.EnterAsync(Context(), TestContext.Current.CancellationToken);

        calls.Should().Equal(200, 300);
        discovery.CallCount.Should().Be(2);
        result.Outcome.Should().Be(BootloaderEntryOutcome.BootloaderIdentified);
        await result.Bootloader!.DisposeAsync();
    }

    private static BootloaderEntryContext Context(SerialDeviceDescriptor? applicationDevice = null, bool active = false) =>
        new(new BootloaderDiscoveryRequest(), applicationDevice, active);

    [Theory]
    [InlineData(0, 0, false)] // Already in bootloader: no reboot and no prompt.
    [InlineData(1, 1, false)] // Automatic reboot succeeds.
    [InlineData(2, 1, true)] // Automatic detection times out; manual reconnect succeeds.
    [InlineData(3, 1, true)] // Both detection phases fail.
    public async Task RealStrategiesRunAutomaticEntryBeforeManualFallback(int misses, int reboots, bool prompted)
    {
        var gateway = new FakeTemporaryGateway();
        var interaction = new FakeInteraction();
        var discovery = new EntryDiscovery(misses);
        var states = new List<FirmwareOperationState>();
        var coordinator = new FirmwareOperationCoordinator(NullLogger<FirmwareOperationCoordinator>.Instance);
        using var operation = coordinator.Begin(FirmwareOperationKind.InstallApplicationFirmware);
        operation.Transition(new(FirmwareOperationState.EnteringBootloader, null, "test.entry"));
        var service = new BootloaderEntryService([
            new ManualReconnectBootloaderEntryStrategy(interaction),
            new TemporaryMavLinkRebootEntryStrategy(gateway),
            new AlreadyInBootloaderEntryStrategy(discovery)
        ], discovery, NullLogger<BootloaderEntryService>.Instance);
        var result = await service.EnterAsync(Context(Device()) with { Progress = p =>
        {
            states.Add(p.State);
            operation.Transition(p);
        } }, TestContext.Current.CancellationToken);
        gateway.CallCount.Should().Be(reboots);
        (interaction.Code == FirmwareInteractionCodes.ManualBootloaderReconnect).Should().Be(prompted);
        states[0].Should().Be(FirmwareOperationState.CheckingForBootloader);
        if (prompted)
        {
            states.IndexOf(FirmwareOperationState.WaitingForBootloader).Should().BeLessThan(states.IndexOf(FirmwareOperationState.ManualBootloaderReconnectRequired));
        }
        result.Outcome.Should().Be(misses == 3 ? BootloaderEntryOutcome.Failed : BootloaderEntryOutcome.BootloaderIdentified);
        if (result.Bootloader is not null)
        {
            operation.Transition(new(FirmwareOperationState.IdentifyingBootloader, null, "test.identified"));
            operation.Transition(new(FirmwareOperationState.CheckingCompatibility, null, "test.validating"));
            await result.Bootloader.DisposeAsync();
        }
        operation.Transition(new(FirmwareOperationState.Cancelled, null, "test.complete"));
    }

    [Fact]
    public async Task CancellationDuringAutomaticDiscoveryDoesNotPrompt()
    {
        using var cancellation = new CancellationTokenSource();
        var interaction = new FakeInteraction();
        var discovery = new EntryDiscovery(1, cancellation);
        var service = new BootloaderEntryService([
            new AlreadyInBootloaderEntryStrategy(discovery),
            new TemporaryMavLinkRebootEntryStrategy(new FakeTemporaryGateway()),
            new ManualReconnectBootloaderEntryStrategy(interaction)
        ], discovery, NullLogger<BootloaderEntryService>.Instance);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.EnterAsync(Context(Device()), cancellation.Token));
        interaction.Code.Should().BeNull();
    }

    private sealed class EntryDiscovery(int misses, CancellationTokenSource? cancel = null) : IBootloaderDiscoveryService
    {
        private int calls;
        public Task<DiscoveredBootloader> FindAsync(BootloaderDiscoveryRequest request, IProgress<FirmwareProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            calls++;
            if (calls > 1 && cancel is not null)
            {
                cancel.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
            }
            return calls <= misses
                ? Task.FromException<DiscoveredBootloader>(new FirmwareDeviceNotFoundException("deadline expired"))
                : Task.FromResult(new DiscoveredBootloader(Device(), new BootloaderIdentity(50, 4, 1024), new NoOpClient()));
        }
    }
    private static SerialDeviceDescriptor Device() => new("COM7", "device-1");

    private sealed class FakeDiscovery(DiscoveredBootloader? result) : IBootloaderDiscoveryService
    {
        public Task<DiscoveredBootloader> FindAsync(BootloaderDiscoveryRequest request, IProgress<FirmwareProgress>? progress = null, CancellationToken cancellationToken = default) =>
            result is null ? Task.FromException<DiscoveredBootloader>(new FirmwareDeviceNotFoundException("not found")) : Task.FromResult(result);
    }
    private sealed class CountingDiscovery : IBootloaderDiscoveryService
    {
        public int CallCount { get; private set; }

        public Task<DiscoveredBootloader> FindAsync(
            BootloaderDiscoveryRequest request,
            IProgress<FirmwareProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            throw new InvalidOperationException("The known application port must not be probed as a bootloader.");
        }
    }
    private sealed class FakeTemporaryGateway : ITemporaryMavLinkBootloaderGateway
    {
        public int CallCount { get; private set; }
        public bool ChannelDisposedBeforeReturn { get; private set; }
        public Task<bool> RebootToBootloaderAsync(SerialDeviceDescriptor applicationDevice, CancellationToken cancellationToken = default)
        {
            CallCount++;
            ChannelDisposedBeforeReturn = true;
            return Task.FromResult(true);
        }
    }
    private sealed class ThrowingTemporaryGateway : ITemporaryMavLinkBootloaderGateway
    {
        public Task<bool> RebootToBootloaderAsync(SerialDeviceDescriptor applicationDevice, CancellationToken cancellationToken = default) =>
            Task.FromException<bool>(new UnauthorizedAccessException("port is owned"));
    }
    private sealed class FakeInteraction(bool accepted = true) : IBootloaderEntryInteraction
    {
        public string? Code { get; private set; }
        public Task<bool> RequestAsync(string interactionCode, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Code = interactionCode;
            return Task.FromResult(accepted);
        }
    }
    private sealed class SequencedDiscovery : IBootloaderDiscoveryService
    {
        public int CallCount { get; private set; }
        public Task<DiscoveredBootloader> FindAsync(BootloaderDiscoveryRequest request, IProgress<FirmwareProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return CallCount == 1
                ? Task.FromException<DiscoveredBootloader>(new FirmwareDeviceNotFoundException("not found after automatic reboot"))
                : Task.FromResult(new DiscoveredBootloader(Device(), new BootloaderIdentity(50, 4, 1024), new NoOpClient()));
        }
    }
    private sealed class ScriptedStrategy(int priority, BootloaderEntryOutcome outcome, ICollection<int> calls) : IBootloaderEntryStrategy
    {
        public int Priority => priority;
        public Task<BootloaderEntryResult> TryEnterAsync(BootloaderEntryContext context, CancellationToken cancellationToken = default)
        {
            calls.Add(priority);
            return Task.FromResult(new BootloaderEntryResult(outcome, $"strategy.{priority}"));
        }
    }
    private sealed class NoOpClient : IArduPilotBootloaderClient
    {
        public Task<BootloaderIdentity> IdentifyAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task EraseAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task ProgramAsync(ApjFirmwarePackage package, IProgress<FirmwareProgress>? progress = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<FirmwareVerificationResult> VerifyAsync(ApjFirmwarePackage package, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task RebootAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
