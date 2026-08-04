using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.Firmware.Compatibility;
using MissionPlanner.Firmware.Discovery;
using MissionPlanner.Firmware.Downloads;
using MissionPlanner.Firmware.Entry;
using MissionPlanner.Firmware.Exceptions;
using MissionPlanner.Firmware.Installation;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Operations;
using MissionPlanner.Firmware.Protocol;
using MissionPlanner.Firmware.Recovery;

namespace MissionPlanner.Firmware.Tests;

public sealed class FirmwareInstallationServiceTests
{
    [Fact]
    public async Task SuccessfulInstallRequiresVerificationAndDisposesPort()
    {
        var fixture = new Fixture();
        var reports = new List<FirmwareProgress>();

        var result = await fixture.Service.InstallAsync(
            fixture.Request,
            new InlineProgress(reports.Add),
            TestContext.Current.CancellationToken);

        result.State.Should().Be(FirmwareOperationState.Completed);
        result.Failure.Should().BeNull();
        fixture.Client.Calls.Should().Equal("erase", "program", "verify", "reboot", "dispose");
        fixture.Interaction.ConfirmCalls.Should().Be(1);
        fixture.Interaction.ManualCalls.Should().Be(0);
        fixture.Client.DestructiveTokens.Should().OnlyContain(token => !token.CanBeCanceled);
        result.ApplicationDevice!.PortName.Should().Be("COM11");
        result.ReconnectSuggested.Should().BeTrue();
        reports.Single(report => report.State == FirmwareOperationState.IdentifyingBootloader)
            .TechnicalDetail.Should().Contain("COM9").And.Contain("board ID: 50").And.Contain("bootloader revision: 4");
    }

    [Fact]
    public async Task CompatibilityFailureCannotReachConfirmationOrErase()
    {
        var fixture = new Fixture(bootloader: new BootloaderIdentity(9, 4, 16));

        var result = await fixture.Service.InstallAsync(fixture.Request, cancellationToken: TestContext.Current.CancellationToken);

        result.State.Should().Be(FirmwareOperationState.Failed);
        result.Failure.Should().NotBeNull();
        result.Failure!.Stage.Should().Be(FirmwareOperationState.CheckingCompatibility);
        result.Failure.Code.Should().Be("installation.compatibility-failed");
        fixture.Interaction.ConfirmCalls.Should().Be(0);
        fixture.Client.Calls.Should().Equal("dispose");
    }

    [Fact]
    public async Task DeclinedFinalConfirmationCannotErase()
    {
        var fixture = new Fixture(confirm: false);

        var result = await fixture.Service.InstallAsync(fixture.Request, cancellationToken: TestContext.Current.CancellationToken);

        result.State.Should().Be(FirmwareOperationState.Cancelled);
        fixture.Client.Calls.Should().Equal("dispose");
    }

    [Fact]
    public async Task VerificationMismatchFailsAndNeverReboots()
    {
        var fixture = new Fixture(verificationSucceeds: false);

        var result = await fixture.Service.InstallAsync(fixture.Request, cancellationToken: TestContext.Current.CancellationToken);

        result.State.Should().Be(FirmwareOperationState.Failed);
        result.Failure!.Stage.Should().Be(FirmwareOperationState.Verifying);
        result.Failure.Code.Should().Be("installation.verification-failed");
        fixture.Client.Calls.Should().Equal("erase", "program", "verify", "dispose");
    }

    [Fact]
    public async Task ConnectedVehicleProducesTypedConflictAndReleasesOperationLease()
    {
        var fixture = new Fixture(connected: true);

        var act = async () => await fixture.Service.InstallAsync(fixture.Request, cancellationToken: TestContext.Current.CancellationToken);

        var exception = await act.Should().ThrowAsync<FirmwareConnectionConflictException>();
        exception.Which.OperationId.Should().NotBeNull();
        exception.Which.State.Should().Be(FirmwareOperationState.Failed);
        exception.Which.Message.Should().Contain("Operation:").And.Contain("state: Failed");
        fixture.Client.Calls.Should().BeEmpty();
        fixture.Coordinator.Begin(FirmwareOperationKind.InstallApplicationFirmware).RequestCancellation().Should().BeTrue();
    }

    [Fact]
    public async Task MissingReturningApplicationDoesNotTurnSuccessfulFlashIntoFailure()
    {
        var fixture = new Fixture(applicationDetected: false);

        var result = await fixture.Service.InstallAsync(fixture.Request, cancellationToken: TestContext.Current.CancellationToken);

        result.State.Should().Be(FirmwareOperationState.Completed);
        result.ApplicationDevice.Should().BeNull();
        result.ReconnectSuggested.Should().BeFalse();
    }

    [Theory]
    [InlineData("erase", FirmwareOperationState.Erasing)]
    [InlineData("program", FirmwareOperationState.Programming)]
    public async Task DestructiveProtocolFailureIsReportedAtExactStage(string failure, FirmwareOperationState stage)
    {
        var fixture = new Fixture(clientFailure: failure);

        var result = await fixture.Service.InstallAsync(fixture.Request, cancellationToken: TestContext.Current.CancellationToken);

        result.State.Should().Be(FirmwareOperationState.Failed);
        result.Failure!.Stage.Should().Be(stage);
        fixture.Client.Calls.Should().NotContain("reboot");
    }

    [Fact]
    public async Task MissingDeviceIsDistinguishedFromProtocolFailure()
    {
        var fixture = new Fixture(entryFailure: new FirmwareDeviceNotFoundException("missing"));

        var result = await fixture.Service.InstallAsync(fixture.Request, cancellationToken: TestContext.Current.CancellationToken);

        result.Failure!.Code.Should().Be("installation.device-not-found");
        fixture.Client.Calls.Should().BeEmpty();
    }

    [Theory]
    [InlineData(false, "installation.download-failed")]
    [InlineData(true, "installation.package-invalid")]
    public async Task DownloadAndPackageFailuresOccurBeforeDeviceAccess(bool invalidPackage, string code)
    {
        var exception = invalidPackage
            ? (Exception)new FirmwarePackageException("invalid")
            : new FirmwareDownloadException("download");
        var fixture = new Fixture(downloadFailure: exception);

        var result = await fixture.Service.InstallAsync(fixture.Request, cancellationToken: TestContext.Current.CancellationToken);

        result.Failure!.Code.Should().Be(code);
        fixture.Client.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task CancellationBeforeEraseEndsCancelled()
    {
        var fixture = new Fixture(entryFailure: new OperationCanceledException());

        var result = await fixture.Service.InstallAsync(fixture.Request, cancellationToken: TestContext.Current.CancellationToken);

        result.State.Should().Be(FirmwareOperationState.Cancelled);
        fixture.Client.Calls.Should().BeEmpty();
    }

    [Fact]
    public async Task CancellationRaisedAfterEraseStartsEndsFailedNotAbruptlyCancelled()
    {
        var fixture = new Fixture(clientFailure: "cancel-erase");

        var result = await fixture.Service.InstallAsync(fixture.Request, cancellationToken: TestContext.Current.CancellationToken);

        result.State.Should().Be(FirmwareOperationState.Failed);
        result.Failure!.Stage.Should().Be(FirmwareOperationState.Erasing);
    }

    [Fact]
    public async Task CallerCancellationDuringEraseIsDeferredUntilPortIsSafelyRebootedAndDisposed()
    {
        using var cancellation = new CancellationTokenSource();
        var fixture = new Fixture(onErase: cancellation.Cancel);

        var result = await fixture.Service.InstallAsync(fixture.Request, cancellationToken: cancellation.Token);

        result.State.Should().Be(FirmwareOperationState.Cancelled);
        fixture.Client.Calls.Should().Equal("erase", "program", "verify", "reboot", "dispose");
        fixture.Client.DestructiveTokens.Should().OnlyContain(token => !token.CanBeCanceled);
        fixture.ApplicationDiscovery.Calls.Should().Be(0);
    }

    private sealed class Fixture
    {
        public Fixture(bool connected = false, bool confirm = true, bool verificationSucceeds = true, BootloaderIdentity? bootloader = null, bool applicationDetected = true, string? clientFailure = null, Exception? entryFailure = null, Exception? downloadFailure = null, Action? onErase = null)
        {
            Coordinator = new FirmwareOperationCoordinator(NullLogger<FirmwareOperationCoordinator>.Instance);
            Client = new FakeClient(verificationSucceeds, clientFailure, onErase);
            Interaction = new FakeInteraction(confirm);
            var found = new DiscoveredBootloader(new SerialDeviceDescriptor("COM9", "bootloader"), bootloader ?? new BootloaderIdentity(50, 4, 16), Client);
            ApplicationDiscovery = new FixedApplicationDiscovery(applicationDetected ? new SerialDeviceDescriptor("COM11", "application") : null);
            Service = new FirmwareInstallationService(
                Coordinator,
                new FakeConnection(connected),
                new FailureDownloader(downloadFailure),
                new FixedEntry(found, entryFailure),
                new UnusedDiscovery(),
                new FirmwareCompatibilityService(),
                Interaction,
                ApplicationDiscovery,
                NullLogger<FirmwareInstallationService>.Instance);
            Request = downloadFailure is null
                ? new FirmwareInstallationRequest(
                    new BootloaderEntryContext(new BootloaderDiscoveryRequest()),
                    Package: new ApjFirmwarePackage(50, new byte[] { 1, 2, 3, 4 }, 16))
                : new FirmwareInstallationRequest(
                    new BootloaderEntryContext(new BootloaderDiscoveryRequest()),
                    Artifact: new FirmwareArtifact(new Uri("https://example.test/test.apj"), FirmwareImageFormat.Apj, 10));
        }
        public FirmwareOperationCoordinator Coordinator { get; }
        public FakeClient Client { get; }
        public FakeInteraction Interaction { get; }
        public FixedApplicationDiscovery ApplicationDiscovery { get; }
        public FirmwareInstallationService Service { get; }
        public FirmwareInstallationRequest Request { get; }
    }

    private sealed class FakeConnection(bool connected) : IFirmwareConnectionGateway
    {
        public bool IsVehicleConnected => connected;
        public ConnectionTransportKind? ActiveTransportKind => connected ? ConnectionTransportKind.Serial : null;
        public Task RequestDisconnectAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("Automatic disconnect is not allowed.");
    }
    private sealed class FakeInteraction(bool confirm) : IFirmwareUserInteraction
    {
        public int ConfirmCalls { get; private set; }
        public int ManualCalls { get; private set; }
        public Task<bool> ConfirmInstallationAsync(FirmwareInstallationConfirmation confirmation, CancellationToken cancellationToken = default) { ConfirmCalls++; return Task.FromResult(confirm); }
        public Task<bool> AcknowledgeManualActionAsync(FirmwareManualAction action, CancellationToken cancellationToken = default) { ManualCalls++; return Task.FromResult(confirm); }
    }
    private sealed class FixedEntry(DiscoveredBootloader found, Exception? failure) : IBootloaderEntryService
    {
        public Task<BootloaderEntryResult> EnterAsync(BootloaderEntryContext context, CancellationToken cancellationToken = default) =>
            failure is null
                ? Task.FromResult(new BootloaderEntryResult(BootloaderEntryOutcome.BootloaderIdentified, "test", found))
                : Task.FromException<BootloaderEntryResult>(failure);
    }
    private sealed class UnusedDiscovery : IBootloaderDiscoveryService
    {
        public Task<DiscoveredBootloader> FindAsync(BootloaderDiscoveryRequest request, IProgress<FirmwareProgress>? progress = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Discovery should not be called.");
    }

    private sealed class InlineProgress(Action<FirmwareProgress> report) : IProgress<FirmwareProgress>
    {
        public void Report(FirmwareProgress value) => report(value);
    }
    private sealed class FailureDownloader(Exception? failure) : IFirmwareArtifactDownloader
    {
        public Task<DownloadedFirmwareArtifact> DownloadAsync(FirmwareArtifact artifact, IProgress<FirmwareProgress>? progress = null, CancellationToken cancellationToken = default) =>
            Task.FromException<DownloadedFirmwareArtifact>(failure ?? new InvalidOperationException("Downloader should not be called."));
    }
    private sealed class FixedApplicationDiscovery(SerialDeviceDescriptor? device) : IFirmwareApplicationDiscoveryService
    {
        public int Calls { get; private set; }
        public Task<SerialDeviceDescriptor?> FindAsync(FirmwareApplicationDiscoveryRequest request, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(device);
        }
    }
    private sealed class FakeClient(bool verificationSucceeds, string? failure, Action? onErase) : IArduPilotBootloaderClient
    {
        public List<string> Calls { get; } = [];
        public List<CancellationToken> DestructiveTokens { get; } = [];
        public Task<BootloaderIdentity> IdentifyAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task EraseAsync(CancellationToken cancellationToken = default) { Calls.Add("erase"); DestructiveTokens.Add(cancellationToken); onErase?.Invoke(); return failure switch { "erase" => Task.FromException(new IOException("erase")), "cancel-erase" => Task.FromCanceled(new CancellationToken(true)), _ => Task.CompletedTask }; }
        public Task ProgramAsync(ApjFirmwarePackage package, IProgress<FirmwareProgress>? progress = null, CancellationToken cancellationToken = default) { Calls.Add("program"); DestructiveTokens.Add(cancellationToken); return failure == "program" ? Task.FromException(new IOException("program")) : Task.CompletedTask; }
        public Task<FirmwareVerificationResult> VerifyAsync(ApjFirmwarePackage package, CancellationToken cancellationToken = default) { Calls.Add("verify"); DestructiveTokens.Add(cancellationToken); return Task.FromResult(new FirmwareVerificationResult(verificationSucceeds, 1, verificationSucceeds ? 1u : 2u)); }
        public Task RebootAsync(CancellationToken cancellationToken = default) { Calls.Add("reboot"); DestructiveTokens.Add(cancellationToken); return Task.CompletedTask; }
        public ValueTask DisposeAsync() { Calls.Add("dispose"); return ValueTask.CompletedTask; }
    }
}
