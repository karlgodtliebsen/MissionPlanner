using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Views.InitSetup.InstallFirmware;
using MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Firmware.Compatibility;
using MissionPlanner.Firmware.Dfu;
using MissionPlanner.Firmware.Entry;
using MissionPlanner.Firmware.Installation;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Preparation;
using MissionPlanner.Firmware.Workflow;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

public sealed class FirmwarePlanViewModelTests
{
    [Fact]
    public async Task FirmwareProgressUsesDialogServiceAndCancellationClosesOwnedHandle()
    {
        var entryService = Substitute.For<IBootloaderEntryService>();
        using var services = FirmwarePanelViewModelTests.CreateServices(collection => collection.AddSingleton(entryService));
        var page = services.GetRequiredService<InstallFirmwareViewModel>();
        await page.ActivateAsync();
        services.GetRequiredService<IActiveVehicleContext>().IsOnline.Returns(false);
        page.DevicesModel.SelectedDevice = new(new SerialDeviceDescriptor("COM10")
        {
            RuntimeProbe = new(FirmwareRuntimeKind.ArduPilot, FirmwareBootEnvironment.None, "test")
            { Verification = FirmwareRuntimeVerification.Verified }
        }, false, "test");
        var handle = Substitute.For<IDisposable>();
        DialogOptions? options = null;
        Func<string>? message = null;
        Action<FirmwareProgress>? progress = null;
        var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        services.GetRequiredService<IDialogService>()
            .DisplayProgressCancellableAsync(Arg.Any<Func<string>>(), Arg.Any<DialogOptions>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                options = call.Arg<DialogOptions>();
                message = call.Arg<Func<string>>();
                return handle;
            });
        entryService.EnterAsync(Arg.Any<BootloaderEntryContext>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var token = call.Arg<CancellationToken>();
                progress = call.Arg<BootloaderEntryContext>()!.Progress;
                started.SetResult(token);
                return WaitForCancellation(token);
            });
        var pending = page.EnterArduPilotBootloaderCommand.ExecuteAsync(null);
        var operationToken = await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Equal("Entering ArduPilot bootloader", options!.Title);
        Assert.Contains("Entering ArduPilot bootloader", message!());
        var updated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        page.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(page.ProgressMessage) && page.ProgressMessage.Contains("Received 1024 bytes"))
            {
                updated.TrySetResult();
            }
        };
        progress!(new(FirmwareOperationState.WaitingForBootloader, null, "test.wait", technicalDetail: "Received 1024 bytes"));
        await updated.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.Contains("Received 1024 bytes", message());
        options.RequestCancellation!();
        Assert.True(operationToken.IsCancellationRequested,
            $"CanCancel={operationToken.CanBeCanceled}; busy={page.IsOperationInProgress}; completed={pending.IsCompleted}; error={page.ErrorMessage}; status={page.StatusMessage}");
        await pending.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        handle.Received(1).Dispose();
        Assert.False(page.IsOperationInProgress);
        await page.DeactivateAsync();

        static async Task<BootloaderEntryResult> WaitForCancellation(CancellationToken token)
        {
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException("Entry must be cancelled.");
        }
    }

    [Fact]
    public async Task DfuPlanRequiresExplicitTargetReviewAndInvalidatesItWhenTargetChanges()
    {
        using var services = Services(ConnectionTransportKind.Udp);
        var page = services.GetRequiredService<InstallFirmwareViewModel>();
        await page.ActivateAsync();
        services.GetRequiredService<IDfuToolLocator>().LocateAsync(Arg.Any<CancellationToken>())
            .Returns(new DfuToolStatus(DfuToolAvailability.Available));
        await page.DfuModel.RefreshAsync(TestContext.Current.CancellationToken);
        page.DfuModel.SelectedDfuDevice = new(new("usb", 0x0483, 0xdf11, DfuDriverState.PresentReady));
        page.DfuModel.LocalDfuFirmwarePath = "Board_with_bl.hex";
        page.DfuModel.LocalDfuPlatform = "Board";
        var metadata = new DfuArtifactMetadata(100, 4, 0x08000000, 0x08000003, new string('a', 64),
            [new DfuMemoryRange(0x08000000, new byte[4])], []);
        services.GetRequiredService<IDfuArtifactResolver>().ResolveAsync(Arg.Any<DfuInstallationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new DfuArtifact("Board_with_bl.hex", "Board_with_bl.hex", metadata, Platform: "Board"));
        await page.PrepareSelectedHexCommand.ExecuteAsync(null);
        Assert.True(page.SelectedArtifact.ArtifactValid);
        Assert.False(page.CurrentPlan.CanExecute);
        services.GetRequiredService<IDialogService>().PromptAsync(Arg.Any<Ursa.Controls.OverlayDialogOptions>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("FLASH Board");
        await page.ReviewDfuTargetCommand.ExecuteAsync(null);
        Assert.True(page.CurrentPlan.CanExecute);
        Assert.True(page.ExecuteCurrentPlanCommand.CanExecute(null));
        page.DfuModel.SelectedDfuDevice = new(new("other-usb", 0x0483, 0xdf11, DfuDriverState.PresentReady));
        Assert.False(page.CurrentPlan.CanExecute);
        await page.DeactivateAsync();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(ConnectionTransportKind.Udp)]
    [InlineData(ConnectionTransportKind.Tcp)]
    public async Task NoTargetAllowsPreparationWithOrWithoutNetworkTelemetry(ConnectionTransportKind? transport)
    {
        using var services = Services(transport);
        var page = services.GetRequiredService<InstallFirmwareViewModel>();
        await page.ActivateAsync();
        Assert.True(page.CurrentPlan.Capabilities.CanBrowseOnlineFirmware);
        Assert.True(page.CurrentPlan.Capabilities.CanSelectLocalFirmware);
        Assert.True(page.CurrentPlan.Capabilities.CanRefreshPhysicalDevices);
        Assert.False(page.CurrentPlan.CanExecute);
        Assert.False(page.IsConnectedMode);
        Assert.Equal("target.absent", page.CurrentPlan.BlockCode);
        await page.DeactivateAsync();
    }

    [Theory]
    [InlineData(FirmwareRuntimeKind.Betaflight, FirmwareBootEnvironment.None, FirmwareArtifactFormat.WithBootloaderHex)]
    [InlineData(FirmwareRuntimeKind.Unknown, FirmwareBootEnvironment.None, FirmwareArtifactFormat.None)]
    [InlineData(FirmwareRuntimeKind.ArduPilot, FirmwareBootEnvironment.None, FirmwareArtifactFormat.Apj)]
    [InlineData(FirmwareRuntimeKind.None, FirmwareBootEnvironment.ArduPilotBootloader, FirmwareArtifactFormat.Apj)]
    public async Task SerialControllerUsesObservedRuntimeAndBootEvidence(FirmwareRuntimeKind runtime, FirmwareBootEnvironment boot, FirmwareArtifactFormat format)
    {
        using var services = Services(ConnectionTransportKind.Udp);
        var page = services.GetRequiredService<InstallFirmwareViewModel>();
        await page.ActivateAsync();
        page.DevicesModel.SelectedDevice = new(new SerialDeviceDescriptor("COM10", usbIdentifier: new(4617, 22337))
        {
            RuntimeProbe = new(runtime, boot, "test-protocol-evidence"),
            BootloaderIdentity = boot == FirmwareBootEnvironment.ArduPilotBootloader ? new(50, 5, 1024) : null
        }, false, "USB hint");
        Assert.Equal(runtime, page.CurrentPlan.Context.Runtime);
        Assert.Equal(format, page.CurrentPlan.RequiredArtifactFormat);
        Assert.False(page.CurrentPlan.CanExecute);
        Assert.False(page.CurrentPlan.Context.TargetPortOwned);
        await page.DeactivateAsync();
    }

    [Fact]
    public async Task GenericDfuIdentityDoesNotProveBoardOrAcceptApj()
    {
        using var services = Services(ConnectionTransportKind.Tcp);
        var page = services.GetRequiredService<InstallFirmwareViewModel>();
        await page.ActivateAsync();
        PrepareOnline(page);
        page.DfuModel.SelectedDfuDevice = new(new("usb", 0x0483, 0xdf11, DfuDriverState.PresentReady));
        Assert.Equal(FirmwareArtifactFormat.WithBootloaderHex, page.CurrentPlan.RequiredArtifactFormat);
        Assert.Equal(FirmwareIdentityConfidence.Unknown, page.CurrentPlan.Context.IdentityConfidence);
        Assert.False(page.CurrentPlan.CanExecute);
        await page.DeactivateAsync();
    }

    [Theory]
    [InlineData(false, 50, true)]
    [InlineData(true, 50, true)]
    [InlineData(false, 51, false)]
    [InlineData(true, 51, false)]
    public async Task LocalAndOnlineUseTheSameBoardCompatibilityAndPlanCommand(bool local, int detectedBoard, bool executable)
    {
        using var services = Services(ConnectionTransportKind.Udp);
        var page = services.GetRequiredService<InstallFirmwareViewModel>();
        await page.ActivateAsync();
        if (local)
        {
            var imported = new LocalFirmwarePreparationResult(Package(), Metadata(), "custom.apj", "custom.apj", false);
            services.GetRequiredService<IFirmwarePreparationService>()
                .ImportAsync(Arg.Any<Stream>(), Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(imported);
            await page.LocalFirmwareModel.LoadCustomFirmwareAsync(TestContext.Current.CancellationToken,
                new("custom.apj", _ => Task.FromResult<Stream>(new MemoryStream())));
        }
        else
        {
            PrepareOnline(page);
        }
        Assert.True(page.SelectedArtifact.ArtifactValid);
        Assert.False(page.SelectedArtifact.TargetCompatible);
        page.DevicesModel.SelectedDevice = new(new SerialDeviceDescriptor("COM10") { BootloaderIdentity = new(detectedBoard, 5, 1024) }, true, "Protocol identity");
        Assert.Equal(executable, page.CurrentPlan.CanExecute);
        Assert.Equal(executable, page.InstallCommand.CanExecute(null));
        Assert.Equal(executable, page.ExecuteCurrentPlanCommand.CanExecute(null));
        Assert.Equal(!local, page.HasOnlineArtifact);
        var selectedDevice = page.DevicesModel.SelectedDevice;
        page.ClearFirmwareSelectionCommand.Execute(null);
        Assert.Same(selectedDevice, page.DevicesModel.SelectedDevice);
        Assert.False(page.SelectedArtifact.ArtifactValid);
        Assert.False(page.CurrentPlan.CanExecute);
        await page.DeactivateAsync();
    }

    [Fact]
    public async Task MavLinkProvenArduPilotIsApplicationRuntimeWithUnresolvedExactBoard()
    {
        using var services = Services(ConnectionTransportKind.Udp);
        var page = services.GetRequiredService<InstallFirmwareViewModel>();
        await page.ActivateAsync();
        // A controller such as "ArduPilot (COM10)" proven by an isolated MAVLink probe.
        page.DevicesModel.SelectedDevice = new(new SerialDeviceDescriptor("COM10", productName: "ArduPilot",
            usbIdentifier: new(4617, 22337))
        {
            RuntimeProbe = new(FirmwareRuntimeKind.ArduPilot, FirmwareBootEnvironment.None, "runtime.ardupilot")
            {
                Evidence = FirmwareRuntimeEvidence.MavLinkProbe,
                Verification = FirmwareRuntimeVerification.Verified
            }
        }, false, "USB hint");

        // Runtime is no longer Unknown, and it is represented as an application (no bootloader active).
        Assert.Equal(FirmwareRuntimeKind.ArduPilot, page.CurrentPlan.Context.Runtime);
        Assert.Equal(FirmwareBootEnvironment.None, page.CurrentPlan.Context.BootEnvironment);
        Assert.Equal(FirmwareArtifactFormat.Apj, page.CurrentPlan.RequiredArtifactFormat);
        // The exact board stays unresolved: runtime knowledge is never exact-board proof.
        Assert.Null(page.CurrentPlan.Context.Bootloader);
        Assert.NotEqual(FirmwareIdentityConfidence.Verified, page.CurrentPlan.Context.IdentityConfidence);
        Assert.False(page.CurrentPlan.CanExecute);
        Assert.Contains("Operating mode: Application", page.PhysicalControllerSummary);
        Assert.Contains("MavLinkProbe", page.PhysicalControllerSummary);
        PrepareOnline(page);
        Assert.True(page.CurrentPlan.CanExecute);
        Assert.True(page.InstallCommand.CanExecute(null));
        Assert.True(page.ExecuteCurrentPlanCommand.CanExecute(null));
        Assert.False(page.SelectedArtifact.TargetCompatible);
        Assert.Null(page.CurrentPlan.Context.Bootloader);
        await page.DeactivateAsync();
    }

    [Fact]
    public async Task UsbArduPilotNameAloneDoesNotProduceVerifiedRuntimeOrExactBoard()
    {
        using var services = Services(ConnectionTransportKind.Udp);
        var page = services.GetRequiredService<InstallFirmwareViewModel>();
        await page.ActivateAsync();
        // USB product text says "ArduPilot", but no protocol identity was obtained.
        page.DevicesModel.SelectedDevice = new(new SerialDeviceDescriptor("COM10", productName: "ArduPilot",
            usbIdentifier: new(4617, 22337))
        {
            RuntimeProbe = new(FirmwareRuntimeKind.Unknown, FirmwareBootEnvironment.None, "runtime.not-probed")
        }, false, "USB hint");

        Assert.Equal(FirmwareRuntimeKind.Unknown, page.CurrentPlan.Context.Runtime);
        Assert.Equal(FirmwareArtifactFormat.None, page.CurrentPlan.RequiredArtifactFormat);
        Assert.NotEqual(FirmwareIdentityConfidence.Verified, page.CurrentPlan.Context.IdentityConfidence);
        Assert.Null(page.CurrentPlan.Context.Bootloader);
        Assert.False(page.CurrentPlan.CanExecute);
        await page.DeactivateAsync();
    }

    [Fact]
    public async Task SameSerialPortBlocksButAnotherPortRemainsAvailable()
    {
        using var services = Services(ConnectionTransportKind.Serial);
        var gateway = services.GetRequiredService<IFirmwareConnectionGateway>();
        gateway.ActiveSerialPort.Returns("COM10");
        var page = services.GetRequiredService<InstallFirmwareViewModel>();
        await page.ActivateAsync();
        page.DevicesModel.SelectedDevice = new(new SerialDeviceDescriptor("COM10"), false, "test");
        Assert.Equal("target.port-owned", page.CurrentPlan.BlockCode);
        page.DevicesModel.SelectedDevice = new(new SerialDeviceDescriptor("COM7"), false, "test");
        Assert.True(page.CurrentPlan.Capabilities.CanProbeRuntime);
        await page.DeactivateAsync();
    }

    private static ServiceProvider Services(ConnectionTransportKind? transport)
    {
        return FirmwarePanelViewModelTests.CreateServices(services =>
        {
            var gateway = Substitute.For<IFirmwareConnectionGateway>();
            gateway.IsVehicleConnected.Returns(transport is not null);
            gateway.ActiveTransportKind.Returns(transport);
            services.AddSingleton(gateway);
            services.AddSingleton<IFirmwareCompatibilityService, FirmwareCompatibilityService>();
            services.AddSingleton<IDfuTargetSafetyService, DfuTargetSafetyService>();
            services.AddSingleton(Options.Create(new DfuOptions()));
        });
    }

    private static ApjFirmwarePackage Package() => new(50, new byte[] { 1, 2, 3 }, 1024);
    private static MissionPlanner.Firmware.Downloads.FirmwareArtifactMetadata Metadata() =>
        new("cache", new Uri("https://example.test/firmware.apj"), DateTimeOffset.UtcNow, 3, new string('a', 64));

    private static void PrepareOnline(InstallFirmwareViewModel page)
    {
        var entry = new FirmwareManifestEntry(new FirmwareVersion("4.6.0"), FirmwareReleaseChannel.Stable,
            new FirmwareBoardTarget(50, "Board", FirmwareVehicleType.Copter),
            new FirmwareArtifact(Metadata().SourceUri, FirmwareImageFormat.Apj));
        page.OnlineFirmwareModel.SetCatalogue([entry], [], false);
        page.OnlineFirmwareModel.SelectedFirmware = page.OnlineFirmwareModel.FirmwareChoices.Single();
        page.ValidatedModel.PreparedFirmware = new(entry, Metadata(), Package(), Metadata().Sha256, false, "cache", []);
    }
}
