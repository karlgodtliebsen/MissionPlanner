using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.Firmware.Compatibility;
using MissionPlanner.Firmware.Discovery;
using MissionPlanner.Firmware.Diagnostics;
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
    public async Task AuthoritativeBootloaderBoardIdentityGovernsCompatibilityNotUsbHints()
    {
        // The bootloader authoritatively reports board 50 (matches the package). The application
        // device carries USB metadata and a friendly name that would suggest a different board.
        var fixture = new Fixture(bootloader: new BootloaderIdentity(50, 4, 16));
        var misleadingDevice = new SerialDeviceDescriptor("COM10", productName: "ArduPilot",
            usbIdentifier: new(4617, 9999), boardHints: ["some-other-board-9"]);
        var request = fixture.Request with
        {
            EntryContext = new BootloaderEntryContext(new BootloaderDiscoveryRequest(misleadingDevice), misleadingDevice)
        };

        var result = await fixture.Service.InstallAsync(request, cancellationToken: TestContext.Current.CancellationToken);

        result.State.Should().Be(FirmwareOperationState.Completed);
        // Confirmation and compatibility used the authoritative bootloader board ID, never the USB hint.
        fixture.Interaction.LastConfirmation!.BootloaderBoardId.Should().Be(50);
        fixture.Client.Calls.Should().Equal("erase", "program", "verify", "reboot", "dispose");
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

    [Theory]
    [InlineData(FirmwareInstallationSource.LocalCustom)]
    [InlineData(FirmwareInstallationSource.OfficialCatalogue)]
    public async Task EverySourceUsesStrictBoardCompatibility(FirmwareInstallationSource source)
    {
        var fixture = new Fixture(bootloader: new BootloaderIdentity(9, 4, 16));
        var request = fixture.Request with
        {
            Source = source,
            LocalFileName = source == FirmwareInstallationSource.LocalCustom ? "custom.apj" : null,
            CompatibilityPolicy = new FirmwareCompatibilityPolicy(AllowBoardIdMismatch: true)
        };
        var result = await fixture.Service.InstallAsync(request, cancellationToken: TestContext.Current.CancellationToken);
        result.State.Should().Be(FirmwareOperationState.Failed);
        fixture.Interaction.ConfirmCalls.Should().Be(0);
        fixture.Client.Calls.Should().Equal("dispose");
        result.DiagnosticReport!.BoardIdOverride.Should().Be(FirmwareBoardIdOverrideState.RequestedNotUsed);
    }
    [Fact]
    public async Task OfficialRequestCannotEnableBoardMismatchOverride()
    {
        var fixture = new Fixture(bootloader: new BootloaderIdentity(9, 4, 16));
        var request = fixture.Request with
        {
            Source = FirmwareInstallationSource.OfficialCatalogue,
            CompatibilityPolicy = new FirmwareCompatibilityPolicy(AllowBoardIdMismatch: true)
        };

        var result = await fixture.Service.InstallAsync(request, cancellationToken: TestContext.Current.CancellationToken);

        result.State.Should().Be(FirmwareOperationState.Failed);
        fixture.Interaction.ConfirmCalls.Should().Be(0);
        fixture.Client.Calls.Should().Equal("dispose");
        result.DiagnosticReport!.BoardIdOverride.Should().Be(FirmwareBoardIdOverrideState.RequestedNotUsed);
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
    [InlineData("reboot", FirmwareOperationState.Rebooting)]
    public async Task DestructiveProtocolFailureIsReportedAtExactStage(string failure, FirmwareOperationState stage)
    {
        var fixture = new Fixture(clientFailure: failure);

        var result = await fixture.Service.InstallAsync(fixture.Request, cancellationToken: TestContext.Current.CancellationToken);

        result.State.Should().Be(FirmwareOperationState.Failed);
        result.Failure!.Stage.Should().Be(stage);
        fixture.Client.Calls.Contains("reboot").Should().Be(failure == "reboot");
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

    [Theory]
    [InlineData(1, true, FirmwareOperationState.Completed)]
    [InlineData(0, true, FirmwareOperationState.Failed)]
    [InlineData(1, false, FirmwareOperationState.Failed)]
    public async Task NormalUpgradeRequiresReturningVerifiedVersion(int patch, bool returns, FirmwareOperationState expectedState)
    {
        var gateway = new FakeUpgrade(patch);
        var fixture = new Fixture(connected: true, applicationDetected: returns, upgrade: gateway);
        var request = fixture.Request with
        {
            EntryContext = new BootloaderEntryContext(new BootloaderDiscoveryRequest(new SerialDeviceDescriptor("COM10")),
                new SerialDeviceDescriptor("COM10")),
            ExpectedRelease = UpgradeRelease()
        };
        var result = await fixture.Service.InstallAsync(request, cancellationToken: TestContext.Current.CancellationToken);
        result.State.Should().Be(expectedState);
        gateway.ReleaseCalls.Should().Be(1);
        gateway.ReconnectCalls.Should().Be(returns ? 1 : 0);
        result.ReconnectSuggested.Should().BeFalse();
        if (expectedState == FirmwareOperationState.Completed)
        {
            result.InstalledIdentity!.FlightVersion!.Patch.Should().Be(1);
        }
    }

    [Fact]
    public async Task NormalUpgradeBoardMismatchStopsBeforeErase()
    {
        var fixture = new Fixture(bootloader: new BootloaderIdentity(9, 4, 16), upgrade: new FakeUpgrade(1));
        var result = await fixture.Service.InstallAsync(fixture.Request with { ExpectedRelease = UpgradeRelease() },
            cancellationToken: TestContext.Current.CancellationToken);
        result.State.Should().Be(FirmwareOperationState.Failed);
        fixture.Client.Calls.Should().Equal("dispose");
    }

    [Theory]
    [InlineData(1002, true, FirmwareOperationState.Completed)]
    [InlineData(134, true, FirmwareOperationState.Failed)]
    [InlineData(1002, false, FirmwareOperationState.Cancelled)]
    public async Task ExplicitWrongTargetRecoveryRequiresMatchingBootloaderAndConfirmation(int board, bool confirm, FirmwareOperationState expected)
    {
        var upgrade = new FakeUpgrade(1, true);
        var fixture = new Fixture(connected: true, confirm: confirm, bootloader: new(board, 5, 16), upgrade: upgrade);
        var release = new FirmwareManifestEntry(new("4.7.1", new Version(4, 7, 1)), FirmwareReleaseChannel.Stable,
            new(1002, "omnibusf4", FirmwareVehicleType.Copter, FirmwareVehicleType.Copter),
            new(new Uri("https://example.test/test.apj"), FirmwareImageFormat.Apj));
        var request = fixture.Request with
        {
            EntryContext = new(new(new SerialDeviceDescriptor("COM12")), new("COM12")),
            Package = new(1002, new byte[] { 1, 2, 3, 4 }, 16, summary: "omnibusf4"),
            ExpectedRelease = release,
            Mode = FirmwareInstallMode.Recovery
        };
        var result = await fixture.Service.InstallAsync(request, cancellationToken: TestContext.Current.CancellationToken);
        result.State.Should().Be(expected);
        fixture.Client.Calls.Contains("erase").Should().Be(expected == FirmwareOperationState.Completed);
        if (board == 1002)
        {
            fixture.Interaction.LastConfirmation!.RequiredPhrase.Should().Be("RECOVER omnibusf4");
            fixture.Interaction.LastConfirmation.IdentityDecision!.RequiresExplicitConfirmation.Should().BeTrue();
        }
        else
        {
            fixture.Interaction.ConfirmCalls.Should().Be(0);
        }
    }

    [Fact]
    public async Task LocalRecoveryUsesEmbeddedIdentityWithoutCatalogueProvenance()
    {
        var upgrade = new FakeUpgrade(1, true);
        var fixture = new Fixture(connected: true, upgrade: upgrade);
        var package = new ApjFirmwarePackage(50, new byte[] { 1, 2, 3, 4 }, 16, summary: "omnibusf4",
            rawMetadata: new Dictionary<string, string>
            {
                ["firmware_version"] = "\"4.7.1\"",
                ["vehicle_type"] = "\"Copter\""
            });
        var request = fixture.Request with
        {
            EntryContext = new(new(new SerialDeviceDescriptor("COM12")), new("COM12")),
            Package = package,
            Source = FirmwareInstallationSource.LocalCustom,
            LocalFileName = "misleading-speedybeef4.apj",
            Mode = FirmwareInstallMode.Recovery
        };
        var result = await fixture.Service.InstallAsync(request, cancellationToken: TestContext.Current.CancellationToken);
        result.State.Should().Be(FirmwareOperationState.Completed);
        result.DiagnosticReport!.CreateReport().Should().Contain("ApjManifest").And.NotContain("OfficialCatalogue");
        upgrade.LocalReconnectCalls.Should().Be(1);
    }

    [Fact]
    public async Task RecoveryCancellationDuringBootloaderTransitionNeverErases()
    {
        var fixture = new Fixture(upgrade: new FakeUpgrade(1), entryFailure: new OperationCanceledException());
        var request = fixture.Request with { Mode = FirmwareInstallMode.Recovery, ExpectedRelease = UpgradeRelease() };
        var result = await fixture.Service.InstallAsync(request, cancellationToken: TestContext.Current.CancellationToken);
        result.State.Should().Be(FirmwareOperationState.Cancelled);
        fixture.Client.Calls.Should().NotContain("erase");
    }

    private static FirmwareManifestEntry UpgradeRelease() => new(new FirmwareVersion("4.7.1", new Version(4, 7, 1)),
        FirmwareReleaseChannel.Stable, new FirmwareBoardTarget(50, "omnibusf4", FirmwareVehicleType.Copter, FirmwareVehicleType.Copter),
        new FirmwareArtifact(new Uri("https://example.test/test.apj"), FirmwareImageFormat.Apj));

    private sealed class FakeUpgrade(int patch, bool wrongTarget = false) : IFirmwareUpgradeConnection
    {
        public RunningFirmwareIdentity? ReadRunningIdentity(SerialDeviceDescriptor device) => wrongTarget
            ? RunningFirmwareIdentity.FromTelemetry(Identity(1) with { BoardVersion = 134u << 16 },
                ["speedybeef4 003D0052 32355116 38393232"]) : null;
        public Task<VehicleFirmwareIdentity> ReleaseForRecoveryAsync(SerialDeviceDescriptor device, CancellationToken cancellationToken)
        {
            ReleaseCalls++;
            return Task.FromResult(Identity(1));
        }
        public int ReleaseCalls { get; private set; }
        public int LocalReconnectCalls { get; private set; }
        public Task<VehicleFirmwareIdentity> ReconnectAsync(SerialDeviceDescriptor device, SelectedFirmwareIdentity selected,
            VehicleFirmwareIdentity? original, CancellationToken cancellationToken)
        {
            LocalReconnectCalls++;
            return Task.FromResult(Identity(patch));
        }
        public int ReconnectCalls { get; private set; }
        public Task<VehicleFirmwareIdentity> ReleaseAsync(SerialDeviceDescriptor device, FirmwareManifestEntry release, CancellationToken cancellationToken)
        {
            ReleaseCalls++;
            return Task.FromResult(Identity(0));
        }
        public Task<VehicleFirmwareIdentity> ReconnectAsync(SerialDeviceDescriptor device, FirmwareManifestEntry release,
            VehicleFirmwareIdentity? original, CancellationToken cancellationToken)
        {
            ReconnectCalls++;
            return Task.FromResult(Identity(patch));
        }
        private static VehicleFirmwareIdentity Identity(int patch) => new(FirmwareFamily.ArduCopter, 2, 3,
            new FirmwareSemanticVersion(4, 7, (byte)patch, FirmwareReleaseType.Official), null, 0, 0, 0, 0, 123, null);
    }

    private sealed class Fixture
    {
        public Fixture(bool connected = false, bool confirm = true, bool verificationSucceeds = true, BootloaderIdentity? bootloader = null, bool applicationDetected = true, string? clientFailure = null, Exception? entryFailure = null, Exception? downloadFailure = null, Action? onErase = null, IFirmwareUpgradeConnection? upgrade = null)
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
                new FirmwareCompatibilityService(),
                Interaction,
                ApplicationDiscovery,
                NullLogger<FirmwareInstallationService>.Instance, upgrade);
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
        public FirmwareInstallationConfirmation? LastConfirmation { get; private set; }
        public Task<bool> ConfirmInstallationAsync(FirmwareInstallationConfirmation confirmation, CancellationToken cancellationToken = default) { ConfirmCalls++; LastConfirmation = confirmation; return Task.FromResult(confirm); }
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
        public Task RebootAsync(CancellationToken cancellationToken = default) { Calls.Add("reboot"); DestructiveTokens.Add(cancellationToken); return failure == "reboot" ? Task.FromException(new IOException("reboot")) : Task.CompletedTask; }
        public ValueTask DisposeAsync() { Calls.Add("dispose"); return ValueTask.CompletedTask; }
    }
}
