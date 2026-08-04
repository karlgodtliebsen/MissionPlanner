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
using MissionPlanner.Firmware.Preparation;
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

    [Fact]
    public async Task LatestChannelRefreshWinsWhenCancelledRequestFinishesLate()
    {
        var stableCompletion = new TaskCompletionSource<FirmwareCatalog>(TaskCreationOptions.RunContinuationsAsynchronously);
        var betaCompletion = new TaskCompletionSource<FirmwareCatalog>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken stableToken = default;
        var catalogService = Substitute.For<IFirmwareCatalogService>();
        catalogService.GetCatalogAsync(Arg.Any<FirmwareCatalogRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (call.Arg<FirmwareCatalogRequest>().Channel == FirmwareReleaseChannel.Stable)
            {
                stableToken = call.Arg<CancellationToken>();
                return stableCompletion.Task;
            }

            return betaCompletion.Task;
        });
        var viewModel = CreateViewModel(
            DisconnectedState(),
            Substitute.For<IFirmwareFilePicker>(),
            Substitute.For<IFirmwarePackageReader>(),
            catalogService: catalogService);

        var stableRefresh = viewModel.RefreshCatalogCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => viewModel.IsCatalogRefreshRunning);
        viewModel.SelectedChannel = FirmwareReleaseChannel.Beta;
        await WaitUntilAsync(() => catalogService.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(IFirmwareCatalogService.GetCatalogAsync)) >= 2);
        stableToken.IsCancellationRequested.Should().BeTrue();
        betaCompletion.SetResult(Catalog(Release(FirmwareReleaseChannel.Beta, 2)));
        await WaitUntilAsync(() => viewModel.FirmwareChoices.SingleOrDefault()?.Channel == FirmwareReleaseChannel.Beta);
        stableCompletion.SetResult(Catalog(Release(FirmwareReleaseChannel.Stable, 1)));
        await stableRefresh;

        viewModel.FirmwareChoices.Should().ContainSingle();
        viewModel.FirmwareChoices[0].Channel.Should().Be(FirmwareReleaseChannel.Beta);
        viewModel.IsCatalogRefreshRunning.Should().BeFalse();
    }

    [Fact]
    public async Task RefreshPreservesAStillAvailableManualSelectionWithoutDuplicates()
    {
        var selected = Release(FirmwareReleaseChannel.Stable, 1);
        var other = Release(FirmwareReleaseChannel.Stable, 2);
        var catalogService = Substitute.For<IFirmwareCatalogService>();
        catalogService.GetCatalogAsync(Arg.Any<FirmwareCatalogRequest>(), Arg.Any<CancellationToken>())
            .Returns(Catalog(selected, other));
        var viewModel = CreateViewModel(
            DisconnectedState(),
            Substitute.For<IFirmwareFilePicker>(),
            Substitute.For<IFirmwarePackageReader>(),
            catalogService: catalogService);

        await viewModel.RefreshCatalogCommand.ExecuteAsync(null);
        viewModel.SelectFirmwareCommand.Execute(viewModel.FirmwareChoices.Single(item => item.BoardId == selected.Target.BoardId));
        await viewModel.RefreshCatalogCommand.ExecuteAsync(null);

        viewModel.SelectedFirmware.Should().NotBeNull();
        viewModel.SelectedFirmware!.BoardId.Should().Be(selected.Target.BoardId);
        viewModel.FirmwareChoices.Select(item => item.ArtifactUrl).Should().OnlyHaveUniqueItems();
    }

    private static InstallFirmwareViewModel CreateViewModel(
        FirmwarePageState state,
        IFirmwareFilePicker picker,
        IFirmwarePackageReader reader,
        IFirmwareInstallationService? installationService = null,
        IFirmwareCatalogService? catalogService = null)
    {
        var resolver = Substitute.For<IFirmwarePageModeResolver>();
        resolver.Resolve(Arg.Any<FirmwarePageContext>()).Returns(state);
        return new InstallFirmwareViewModel(
            catalogService ?? Substitute.For<IFirmwareCatalogService>(),
            installationService ?? Substitute.For<IFirmwareInstallationService>(),
            Substitute.For<IFirmwarePreparationService>(),
            Substitute.For<IEmbeddedBootloaderUpdateService>(),
            Substitute.For<IFirmwareSerialDeviceCatalog>(),
            resolver,
            reader,
            picker,
            Substitute.For<IActiveVehicleContext>(),
            Substitute.For<IUserConfirmationService>(),
            ImmediateDispatcher(),
            NullLogger<InstallFirmwareViewModel>.Instance);
    }

    private static IDispatcher ImmediateDispatcher()
    {
        var dispatcher = Substitute.For<IDispatcher>();
        dispatcher.Dispatch(Arg.Any<Action>()).Returns(call =>
        {
            call.Arg<Action>()();
            return true;
        });
        return dispatcher;
    }

    private static FirmwareCatalog Catalog(params FirmwareManifestEntry[] entries) =>
        new(entries, DateTimeOffset.UtcNow, false);

    private static FirmwareManifestEntry Release(FirmwareReleaseChannel channel, int boardId) =>
        new(
            new FirmwareVersion($"4.6.{boardId}"),
            channel,
            new FirmwareBoardTarget(boardId, $"board-{boardId}", FirmwareVehicleType.Copter),
            new FirmwareArtifact(new Uri($"https://firmware.example.test/board-{boardId}.apj"), FirmwareImageFormat.Apj));

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var attempt = 0; attempt < 100 && !condition(); attempt++)
        {
            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        condition().Should().BeTrue();
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
