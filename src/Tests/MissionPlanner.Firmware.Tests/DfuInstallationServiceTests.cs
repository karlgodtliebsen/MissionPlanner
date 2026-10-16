using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MissionPlanner.Firmware.Betaflight;
using MissionPlanner.Firmware.Dfu;
using MissionPlanner.Firmware.Installation;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Operations;
using MissionPlanner.Firmware.Recovery;

namespace MissionPlanner.Firmware.Tests;

public sealed class DfuInstallationServiceTests
{
    [Fact]
    public async Task VerifiedProgrammingAndApplicationRediscoveryCompleteSeparately()
    {
        var fixture = new Fixture(applicationReturns: true);

        var result = await fixture.Service.InstallAsync(fixture.Request, cancellationToken: TestContext.Current.CancellationToken);

        result.State.Should().Be(DfuOperationState.Completed);
        result.ProgrammingSucceeded.Should().BeTrue();
        result.VerificationSucceeded.Should().BeTrue();
        result.ApplicationRediscovered.Should().BeTrue();
        result.OperationId.Should().NotBeNull();
        fixture.Programmer.ProgramCalls.Should().Be(1);
        fixture.Discovery.Request!.PhysicalLocation.Should().Be("USB-PORT-1");
    }

    [Fact]
    public async Task MissingApplicationIsWarningAndNotProgrammingFailure()
    {
        var fixture = new Fixture(applicationReturns: false);

        var result = await fixture.Service.InstallAsync(fixture.Request, cancellationToken: TestContext.Current.CancellationToken);

        result.State.Should().Be(DfuOperationState.Completed);
        result.Outcome.Should().Be(DfuProgrammingOutcome.Succeeded);
        result.VerificationSucceeded.Should().BeTrue();
        result.ApplicationRediscovered.Should().BeFalse();
        result.Warnings.Should().Contain("dfu.application-not-rediscovered");
    }

    [Fact]
    public async Task DriverProblemStopsBeforeProviderExecution()
    {
        var fixture = new Fixture(applicationReturns: true, DfuDriverState.PresentWrongDriver);

        var result = await fixture.Service.InstallAsync(fixture.Request, cancellationToken: TestContext.Current.CancellationToken);

        result.Failure!.Code.Should().Be("dfu.driver-not-ready");
        fixture.Programmer.ProgramCalls.Should().Be(0);
    }

    [Fact]
    public async Task RejectedFinalConfirmationCancelsSafely()
    {
        var fixture = new Fixture(applicationReturns: true) { Confirm = false };

        var result = await fixture.Service.InstallAsync(fixture.Request, cancellationToken: TestContext.Current.CancellationToken);

        result.State.Should().Be(DfuOperationState.Cancelled);
        fixture.Programmer.ProgramCalls.Should().Be(0);
    }

    /// <summary>Cancelling rediscovery retains the already verified programming result and releases ownership.</summary>
    [Fact]
    public async Task CancelAfterProgrammingPreservesVerifiedResult()
    {
        using var cancellation = new CancellationTokenSource();
        var fixture = new Fixture(applicationReturns: false);
        fixture.Discovery.OnFind = () => cancellation.Cancel();

        var result = await fixture.Service.InstallAsync(fixture.Request, cancellationToken: cancellation.Token);

        Assert.Equal(DfuOperationState.Cancelled, result.State);
        Assert.True(result.ProgrammingSucceeded);
        Assert.True(result.VerificationSucceeded);
        Assert.False(result.ApplicationRediscovered);
        Assert.Equal(DfuProgrammingOutcome.Succeeded, result.Outcome);
        Assert.Equal(DfuOperationState.WaitingForApplication, result.Failure?.Stage);
        using var next = fixture.Coordinator.Begin(FirmwareOperationKind.ProbeFirmwareIdentity);
        next.Transition(new FirmwareProgress(FirmwareOperationState.Completed, null, "test.ownership-released"));
    }

    private sealed class Fixture
    {
        private readonly MutableCatalog catalog;
        private readonly Interaction interaction;

        public Fixture(bool applicationReturns, DfuDriverState driverState = DfuDriverState.PresentReady)
        {
            Device = new DfuDeviceDescriptor("usb1", 0x0483, 0xdf11, driverState, SerialNumber: "ABC", PnpInstanceId: "USB\\DFU");
            Artifact = new DfuArtifact("board_with_bl.hex", "C:\\firmware\\board_with_bl.hex",
                new DfuArtifactMetadata(10, 2, 0x08000000, 0x08010000, "hash", [new DfuMemoryRange(0x08000000, new byte[] { 1, 2 })], [], AppearsToContainBootloader: true, AppearsToContainApplication: true),
                Platform: "Board", BoardId: 1);
            Request = new DfuInstallationRequest("Board", 1, Device, Artifact, ConfirmationPhrase: "FLASH Board");
            catalog = new MutableCatalog(Device);
            interaction = new Interaction(() => Confirm);
            Programmer = new Programmer(catalog);
            Coordinator = new FirmwareOperationCoordinator(NullLogger<FirmwareOperationCoordinator>.Instance);
            Discovery = new Discovery(applicationReturns);
            Service = new DfuInstallationService(Coordinator, new Locator(), new Resolver(Artifact), catalog,
                new Monitor(), Programmer, new Safety(), interaction, Discovery,
                Options.Create(new DfuOptions { DfuDisappearanceTimeout = TimeSpan.FromMilliseconds(20) }),
                NullLogger<DfuInstallationService>.Instance, new Topology(catalog));
        }

        public bool Confirm { get; set; } = true;
        public DfuDeviceDescriptor Device { get; }
        public DfuArtifact Artifact { get; }
        public DfuInstallationRequest Request { get; }
        public Programmer Programmer { get; }
        public DfuInstallationService Service { get; }
        public Discovery Discovery { get; }
        public FirmwareOperationCoordinator Coordinator { get; }
    }

    private sealed class Connection : IFirmwareConnectionGateway
    {
        public bool IsVehicleConnected => false;
        public ConnectionTransportKind? ActiveTransportKind => null;
        public Task RequestDisconnectAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class Locator : IDfuToolLocator
    {
        public Task<DfuToolStatus> LocateAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new DfuToolStatus(DfuToolAvailability.Available, "tool.exe", new Version(2, 20)));
    }

    private sealed class Resolver(DfuArtifact artifact) : IDfuArtifactResolver
    {
        public Task<DfuArtifact> ResolveAsync(DfuInstallationRequest request, CancellationToken cancellationToken = default) => Task.FromResult(artifact);
    }

    private sealed class MutableCatalog(params DfuDeviceDescriptor[] devices) : IDfuDeviceCatalog
    {
        public IReadOnlyList<DfuDeviceDescriptor> Devices { get; set; } = devices;
        public Task<IReadOnlyList<DfuDeviceDescriptor>> GetDevicesAsync(CancellationToken cancellationToken = default) => Task.FromResult(Devices);
    }

    private sealed class Monitor : IDfuDeviceMonitor
    {
        public async IAsyncEnumerable<IReadOnlyList<DfuDeviceDescriptor>> WatchAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            yield break;
        }
    }

    private sealed class Programmer(MutableCatalog catalog) : IDfuProgrammer
    {
        public int ProgramCalls { get; private set; }
        public Task<DfuProviderCapabilities> GetCapabilitiesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new DfuProviderCapabilities(true, true, true, true, false, false));
        public Task<DfuDeviceInformation> InspectAsync(DfuDeviceDescriptor selected, CancellationToken cancellationToken = default) =>
            Task.FromResult(new DfuDeviceInformation(selected, "0x413", "A", 2 * 1024 * 1024, [], []));
        public Task<DfuProgrammingResult> ProgramAndVerifyAsync(DfuProgrammingRequest request, IProgress<DfuProgress>? progress = null, CancellationToken cancellationToken = default)
        {
            ProgramCalls++;
            catalog.Devices = [];
            return Task.FromResult(new DfuProgrammingResult(DfuOperationState.Completed, true, true, false, Outcome: DfuProgrammingOutcome.Succeeded));
        }
    }

    private sealed class Safety : IDfuTargetSafetyService
    {
        public DfuTargetSafetyResult Evaluate(DfuTargetSafetyRequest request) => new(DfuTargetSafetyDecision.Allowed, ["RememberedAssociationMatched"]);
    }

    private sealed class Interaction(Func<bool> confirm) : IDfuUserInteraction
    {
        public Task<bool> ConfirmAsync(DfuInstallationConfirmation confirmation, CancellationToken cancellationToken = default) => Task.FromResult(confirm());
        public Task<bool> AcknowledgePowerCycleAsync(DfuInstallationConfirmation confirmation, CancellationToken cancellationToken = default) => Task.FromResult(true);
    }

    private sealed class Discovery(bool returnsDevice) : IFirmwareApplicationDiscoveryService
    {
        public FirmwareApplicationDiscoveryRequest? Request { get; private set; }
        public Action? OnFind { get; set; }

        public Task<SerialDeviceDescriptor?> FindAsync(FirmwareApplicationDiscoveryRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            OnFind?.Invoke();
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<SerialDeviceDescriptor?>(returnsDevice ? new SerialDeviceDescriptor("COM9", "application") : null);
        }
    }

    private sealed class Topology(MutableCatalog catalog) : IUsbTopologyProvider
    {
        public Task<string?> GetLocationAsync(string? instanceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(catalog.Devices.Any(device => device.PnpInstanceId == instanceId) ? "USB-PORT-1" : null);
        }
    }
}
