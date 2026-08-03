using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Views.InitSetup.InstallFirmware;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Firmware.Catalog;
using MissionPlanner.Firmware.Connected;
using MissionPlanner.Firmware.Devices;
using MissionPlanner.Firmware.Images;
using MissionPlanner.Firmware.Installation;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Presentation;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

public sealed class FirmwarePresentationTests
{
    [Fact]
    public void ConnectedAndUnsupportedModesExposeOnlyTheirAllowedActions()
    {
        var connected = CreateViewModel(new FirmwarePageState(
            FirmwarePageMode.Connected,
            true, false, false, false, false, false,
            false, true, false, true, null),
            Substitute.For<IFirmwareFilePicker>(),
            Substitute.For<IFirmwarePackageReader>());
        var unsupported = CreateViewModel(new FirmwarePageState(
            FirmwarePageMode.UnsupportedPlatform,
            false, false, false, false, false, false,
            false, false, false, true, null),
            Substitute.For<IFirmwareFilePicker>(),
            Substitute.For<IFirmwarePackageReader>());

        connected.IsConnectedMode.Should().BeTrue();
        connected.IsDisconnectedMode.Should().BeFalse();
        connected.CanInstall.Should().BeFalse();
        connected.CanUpdateBootloader.Should().BeTrue();
        connected.InstallCommand.CanExecute(null).Should().BeFalse();
        unsupported.IsUnsupportedMode.Should().BeTrue();
        unsupported.CanInstall.Should().BeFalse();
        unsupported.CanUpdateBootloader.Should().BeFalse();
    }

    [Fact]
    public async Task DisconnectedViewModelExposesChannelsAndParsedCustomPackage()
    {
        var package = new ApjFirmwarePackage(
            50,
            new byte[] { 1, 2, 3 },
            16,
            description: "Test package",
            summary: "CubeOrange",
            version: "4.7.0",
            gitIdentity: "abcdef1");
        var picker = Substitute.For<IFirmwareFilePicker>();
        var selectedStream = new TrackingStream();
        picker.PickAsync(Arg.Any<CancellationToken>()).Returns(
            new FirmwareFileSelection("test.apj", _ => Task.FromResult<Stream>(selectedStream)));
        var reader = Substitute.For<IFirmwarePackageReader>();
        reader.ReadAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(package);
        var viewModel = CreateViewModel(DisconnectedState(), picker, reader);

        await viewModel.LoadCustomFirmwareCommand.ExecuteAsync(null);

        viewModel.IsDisconnectedMode.Should().BeTrue();
        viewModel.Channels.Should().Equal(
            FirmwareReleaseChannel.Stable,
            FirmwareReleaseChannel.Beta,
            FirmwareReleaseChannel.Latest);
        viewModel.HasCustomFirmware.Should().BeTrue();
        viewModel.CustomFirmwareBoardId.Should().Be(50);
        viewModel.CustomFirmwarePlatform.Should().Be("CubeOrange");
        viewModel.InstallCommand.CanExecute(null).Should().BeTrue();
        selectedStream.WasDisposed.Should().BeTrue();
    }

    [Fact]
    public async Task InvalidCustomExtensionIsRejectedBeforeOpeningOrParsing()
    {
        var opened = false;
        var picker = Substitute.For<IFirmwareFilePicker>();
        picker.PickAsync(Arg.Any<CancellationToken>()).Returns(
            new FirmwareFileSelection("legacy.hex", _ => { opened = true; return Task.FromResult<Stream>(new MemoryStream()); }));
        var reader = Substitute.For<IFirmwarePackageReader>();
        var viewModel = CreateViewModel(DisconnectedState(), picker, reader);

        await viewModel.LoadCustomFirmwareCommand.ExecuteAsync(null);

        opened.Should().BeFalse();
        await reader.DidNotReceive().ReadAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        viewModel.StatusMessage.Should().Contain("DFU/legacy");
        viewModel.HasCustomFirmware.Should().BeFalse();
    }

    [Fact]
    public async Task OperationProgressNormalizesPercentageAndShowsBootloaderDetail()
    {
        var package = new ApjFirmwarePackage(50, new byte[] { 1, 2, 3 }, 16);
        var picker = Substitute.For<IFirmwareFilePicker>();
        picker.PickAsync(Arg.Any<CancellationToken>()).Returns(
            new FirmwareFileSelection("test.apj", _ => Task.FromResult<Stream>(new MemoryStream())));
        var reader = Substitute.For<IFirmwarePackageReader>();
        reader.ReadAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(package);
        var installer = Substitute.For<IFirmwareInstallationService>();
        installer.InstallAsync(
                Arg.Any<FirmwareInstallationRequest>(),
                Arg.Any<IProgress<FirmwareProgress>>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                call.ArgAt<IProgress<FirmwareProgress>>(1).Report(new FirmwareProgress(
                    FirmwareOperationState.Programming,
                    43,
                    "program.progress",
                    technicalDetail: "Device: COM9; board ID: 50"));
                return Task.FromResult(new FirmwareOperationResult(
                    Guid.NewGuid(),
                    FirmwareOperationKind.InstallApplicationFirmware,
                    FirmwareOperationState.Completed));
            });
        var viewModel = CreateViewModel(DisconnectedState(), picker, reader, installer);

        await viewModel.LoadCustomFirmwareCommand.ExecuteAsync(null);
        await viewModel.InstallCommand.ExecuteAsync(null);
        for (var attempt = 0; attempt < 20 && viewModel.OperationProgress.TechnicalDetail is null; attempt++)
            await Task.Delay(10, TestContext.Current.CancellationToken);

        viewModel.OperationProgress.Progress.Should().Be(0.43);
        viewModel.OperationProgress.TechnicalDetail.Should().Contain("board ID: 50");
    }

    [Fact]
    public async Task InteractionAdapterReturnsConfirmationAndPublishesManualRequest()
    {
        var confirmation = Substitute.For<IUserConfirmationService>();
        confirmation.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);
        var service = new FirmwareInteractionService(confirmation);

        var accepted = await service.ConfirmInstallationAsync(
            new FirmwareInstallationConfirmation(50, 50, 5, 1000, "test"),
            TestContext.Current.CancellationToken);
        await service.RequestAsync("bootloader.manual-reconnect", TestContext.Current.CancellationToken);

        accepted.Should().BeTrue();
        await confirmation.Received(2).ConfirmAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static InstallFirmwareViewModel CreateViewModel(
        FirmwarePageState state,
        IFirmwareFilePicker picker,
        IFirmwarePackageReader reader,
        IFirmwareInstallationService? installationService = null)
    {
        var resolver = Substitute.For<IFirmwarePageModeResolver>();
        resolver.Resolve(Arg.Any<FirmwarePageContext>()).Returns(state);
        return new InstallFirmwareViewModel(
            Substitute.For<IFirmwareCatalogService>(),
            installationService ?? Substitute.For<IFirmwareInstallationService>(),
            Substitute.For<IEmbeddedBootloaderUpdateService>(),
            Substitute.For<IFirmwareSerialDeviceCatalog>(),
            resolver,
            reader,
            picker,
            Substitute.For<IActiveVehicleContext>(),
            Substitute.For<IUserConfirmationService>(),
            Substitute.For<IDispatcher>(),
            NullLogger<InstallFirmwareViewModel>.Instance);
    }

    private static FirmwarePageState DisconnectedState() => new(
        FirmwarePageMode.Disconnected,
        false, true, true, true, true, true,
        true, false, false, true, null);

    private sealed class TrackingStream : MemoryStream
    {
        public bool WasDisposed { get; private set; }
        protected override void Dispose(bool disposing)
        {
            WasDisposed = true;
            base.Dispose(disposing);
        }
    }
}
