using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Views.InitSetup.InstallFirmware;
using MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Firmware.Catalog;
using MissionPlanner.Firmware.Devices;
using MissionPlanner.Firmware.Dfu;
using MissionPlanner.Firmware.Model;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

public sealed class FirmwarePanelLoadingTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LandingRejectsUnidentifiedPortWithoutRequestingReboot(bool portBusy)
    {
        using var services = FirmwarePanelViewModelTests.CreateServices();
        var parent = services.GetRequiredService<InstallFirmwareViewModel>();
        var landing = services.GetRequiredService<FirmwareLandingViewModel>();
        await parent.ActivateAsync();
        await landing.ActivateAsync();
        services.GetRequiredService<IActiveVehicleContext>().IsOnline.Returns(false);
        parent.DevicesModel.SelectedDevice = new(new SerialDeviceDescriptor("COM4"), false, "Unknown");
        if (portBusy)
        {
            services.GetRequiredService<MissionPlanner.Firmware.Betaflight.IFirmwareDeviceIdentityService>()
                .EnrichAsync(Arg.Any<IReadOnlyList<SerialDeviceDescriptor>>(), true, Arg.Any<CancellationToken>())
                .Returns(call => new[] { call.Arg<IReadOnlyList<SerialDeviceDescriptor>>()[0] with
                { BetaflightProbeOutcome = MissionPlanner.Firmware.Betaflight.BetaflightProbeOutcome.PortBusy } });
        }
        Assert.True(landing.RebootToDfuCommand.CanExecute(null));

        await landing.RebootToDfuCommand.ExecuteAsync(null);

        Assert.Contains(portBusy ? "Cannot open COM4" : "Could not verify", parent.DfuModel.DfuStatus);
        Assert.Equal(parent.DfuModel.DfuStatus, landing.ErrorMessage);
        Assert.True(landing.HasError);
        Assert.False(parent.IsOperationInProgress);
        await services.GetRequiredService<MissionPlanner.Firmware.Betaflight.IBetaflightDfuHandoff>()
            .DidNotReceiveWithAnyArgs().RebootAsync(default!, default, TestContext.Current.CancellationToken);
        await services.GetRequiredService<IDialogService>().DidNotReceiveWithAnyArgs()
            .ConfirmAsync(default!, default!, TestContext.Current.CancellationToken);
        await landing.DeactivateAsync();
        await parent.DeactivateAsync();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task LandingDfuRebootRequiresConfirmationAndReleasesOperation(bool confirm, bool succeeds)
    {
        using var services = FirmwarePanelViewModelTests.CreateServices();
        var parent = services.GetRequiredService<InstallFirmwareViewModel>();
        var landing = services.GetRequiredService<FirmwareLandingViewModel>();
        await parent.ActivateAsync();
        await landing.ActivateAsync();
        var vehicle = services.GetRequiredService<IActiveVehicleContext>();
        vehicle.IsOnline.Returns(false);
        var source = new SerialDeviceDescriptor("COM4")
        {
            BetaflightIdentity = new("COM4", new Version(1, 46), "BTFL", McuType: "STM32F405", McuUniqueId: "test-uid")
        };
        // Enumeration alone must leave a way to retry identity on the selected port.
        var unprobed = source with
        {
            BetaflightIdentity = null
        };
        parent.DevicesModel.SelectedDevice = new(unprobed, false, "Manual device selection");
        services.GetRequiredService<MissionPlanner.Firmware.Betaflight.IFirmwareDeviceIdentityService>()
            .EnrichAsync(Arg.Is<IReadOnlyList<SerialDeviceDescriptor>>(items => items.Count == 1 && items[0] == unprobed),
                true, Arg.Any<CancellationToken>()).Returns(new[] { source });
        var dialogs = services.GetRequiredService<IDialogService>();
        dialogs.ConfirmAsync(Arg.Any<Ursa.Controls.OverlayDialogOptions>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(confirm);
        var handoff = services.GetRequiredService<MissionPlanner.Firmware.Betaflight.IBetaflightDfuHandoff>();
        var dfuDevice = new DfuDeviceDescriptor("selected-dfu", 0x0483, 0xDF11, DfuDriverState.PresentReady,
            ArrivedAt: DateTimeOffset.UtcNow);
        services.GetRequiredService<IDfuDeviceCatalog>().GetDevicesAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { dfuDevice });
        services.GetRequiredService<IDfuToolLocator>().LocateAsync(Arg.Any<CancellationToken>())
            .Returns(new DfuToolStatus(DfuToolAvailability.NotInstalled));
        handoff.RebootAsync(source, Arg.Any<IProgress<FirmwareProgress>>(), Arg.Any<CancellationToken>())
            .Returns(new MissionPlanner.Firmware.Betaflight.BetaflightDfuHandoffResult(succeeds, "test-not-found", source,
                succeeds ? dfuDevice : null));

        Assert.True(landing.RebootToDfuCommand.CanExecute(null));
        await landing.RebootToDfuCommand.ExecuteAsync(null);

        await handoff.Received(confirm ? 1 : 0).RebootAsync(source, Arg.Any<IProgress<FirmwareProgress>>(), Arg.Any<CancellationToken>());
        Assert.False(parent.IsOperationInProgress);
        if (succeeds)
        {
            Assert.Same(dfuDevice, parent.DfuModel.SelectedDfuDevice?.Descriptor);
            Assert.True(parent.CanUseDfuFirmware);
            Assert.Equal((int)FirmwareSection.Stm32Dfu, parent.SelectedSectionIndex);
            Assert.Equal((int)Stm32DfuSection.Catalogue, parent.SelectedDfuTabIndex);
            Assert.Same(source, parent.DfuModel.CorrelatedHandoff?.Source);
            Assert.True(parent.DfuModel.HasCorrelatedSource);
        }
        else if (confirm)
        {
            Assert.Contains("test-not-found", parent.DfuModel.DfuStatus);
            Assert.Equal((int)Stm32DfuSection.Device, parent.SelectedDfuTabIndex);
        }
        vehicle.IsOnline.Returns(true);
        await services.GetRequiredService<IDfuInstallationService>().DidNotReceiveWithAnyArgs()
            .InstallAsync(default!, default, TestContext.Current.CancellationToken);
        Assert.False(landing.RebootToDfuCommand.CanExecute(null));
        await landing.DeactivateAsync();
        await parent.DeactivateAsync();
        vehicle.IsOnline.Returns(false);
        Assert.False(landing.RebootToDfuCommand.CanExecute(null));
    }

    [Fact]
    public async Task LandingObservesDiscoveryOnlyWhileActiveWithoutStartingScans()
    {
        using var services = FirmwarePanelViewModelTests.CreateServices();
        services.GetRequiredService<IActiveVehicleContext>().IsOnline.Returns(false);
        var landing = services.GetRequiredService<FirmwareLandingViewModel>();
        var devices = services.GetRequiredService<DetectedDeviceViewModel>();
        var notifications = 0;
        landing.PropertyChanged += (_, _) => notifications++;
        await landing.ActivateAsync();
        await landing.ActivateAsync();
        notifications = 0;
        devices.DeviceStatus = "Device discovery failed: permission denied";
        Assert.Equal(1, notifications);
        Assert.Contains("permission denied", landing.SerialDetail);
        Assert.Equal("No vehicle connected", landing.ConnectionSummary);
        await services.GetRequiredService<IFirmwareSerialDeviceCatalog>().DidNotReceiveWithAnyArgs()
            .GetDevicesAsync(TestContext.Current.CancellationToken);
        await services.GetRequiredService<IDfuDeviceCatalog>().DidNotReceiveWithAnyArgs()
            .GetDevicesAsync(TestContext.Current.CancellationToken);
        await landing.DeactivateAsync();
        notifications = 0;
        devices.DeviceStatus = "Ready";
        Assert.Equal(0, notifications);
        await landing.ActivateAsync();
        Assert.Contains("Ready", landing.SerialDetail);
        await landing.DeactivateAsync();
    }

    [Fact]
    public async Task ParentActivationDiscoversDevicesWithoutLoadingCatalogue()
    {
        using var services = FirmwarePanelViewModelTests.CreateServices();
        services.GetRequiredService<IActiveVehicleContext>().IsOnline.Returns(false);
        var parent = services.GetRequiredService<InstallFirmwareViewModel>();
        await parent.ActivateAsync();
        await services.GetRequiredService<IFirmwareCatalogService>().DidNotReceiveWithAnyArgs().GetCatalogAsync(default!, TestContext.Current.CancellationToken);
        await services.GetRequiredService<IFirmwareSerialDeviceCatalog>().ReceivedWithAnyArgs(1).GetDevicesAsync(TestContext.Current.CancellationToken);
        await services.GetRequiredService<IDfuDeviceCatalog>().ReceivedWithAnyArgs(1).GetDevicesAsync(TestContext.Current.CancellationToken);
        await parent.DeactivateAsync();
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    public async Task WorkflowTabsFollowDiscoveredDeviceAndConnection(bool serial, bool dfu, bool connected)
    {
        using var services = FirmwarePanelViewModelTests.CreateServices();
        services.GetRequiredService<IActiveVehicleContext>().IsOnline.Returns(connected);
        services.GetRequiredService<IFirmwareSerialDeviceCatalog>().GetDevicesAsync(Arg.Any<CancellationToken>())
            .Returns(serial ? new[] { new SerialDeviceDescriptor("COM11") } : Array.Empty<SerialDeviceDescriptor>());
        services.GetRequiredService<IDfuDeviceCatalog>().GetDevicesAsync(Arg.Any<CancellationToken>())
            .Returns(dfu ? new[] { new DfuDeviceDescriptor("test", 0x0483, 0xdf11, DfuDriverState.PresentReady) } : Array.Empty<DfuDeviceDescriptor>());
        services.GetRequiredService<IDfuToolLocator>().LocateAsync(Arg.Any<CancellationToken>())
            .Returns(new DfuToolStatus(DfuToolAvailability.Available, "test"));
        var parent = services.GetRequiredService<InstallFirmwareViewModel>();
        await parent.ActivateAsync();
        Assert.Equal(OperatingSystem.IsWindows() && !connected && serial && !dfu, parent.CanUseSerialFirmware);
        Assert.Equal(OperatingSystem.IsWindows() && !connected && dfu, parent.CanUseDfuFirmware);

        // Unloading a workflow view must not stop page-owned discovery.
        await parent.DevicesModel.DeactivateAsync();
        await parent.DfuModel.DeactivateAsync();
        services.GetRequiredService<IFirmwareSerialDeviceCatalog>().GetDevicesAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<SerialDeviceDescriptor>());
        services.GetRequiredService<IDfuDeviceCatalog>().GetDevicesAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<DfuDeviceDescriptor>());
        await parent.RefreshDevicesCommand.ExecuteAsync(null);
        Assert.False(parent.CanUseSerialFirmware);
        Assert.False(parent.CanUseDfuFirmware);
        await parent.DeactivateAsync();
        Assert.False(parent.CanUseSerialFirmware);
        Assert.False(parent.CanUseDfuFirmware);
    }

    [Fact]
    public async Task CatalogueLoadsWithoutParentAndOwnsProgressAndSelectionAcrossActivation()
    {
        using var services = FirmwarePanelViewModelTests.CreateServices();
        services.GetRequiredService<IActiveVehicleContext>().IsOnline.Returns(false);
        var service = services.GetRequiredService<IFirmwareCatalogService>();
        var entry = Entry();
        service.GetCatalogAsync(Arg.Any<FirmwareCatalogRequest>(), Arg.Any<CancellationToken>()).Returns(Catalog(entry));
        var handle = Substitute.For<IDisposable>();
        services.GetRequiredService<IDialogService>().DisplayProgressCancellableAsync(Arg.Any<Func<string>>(), Arg.Any<DialogOptions>(), Arg.Any<CancellationToken>()).Returns(handle);
        var panel = services.GetRequiredService<FirmwareCatalogueViewModel>();
        await panel.ActivateAsync();
        panel.SelectedFirmware = Assert.Single(panel.FirmwareChoices);
        await panel.DeactivateAsync();
        await panel.ActivateAsync();
        Assert.Same(entry, panel.SelectedFirmware!.Entry);
        Assert.False(panel.IsRefreshing);
        Assert.False(panel.IsBusy);
        handle.Received(2).Dispose();
        await services.GetRequiredService<IDfuDeviceCatalog>().DidNotReceiveWithAnyArgs().GetDevicesAsync(TestContext.Current.CancellationToken);
        await services.GetRequiredService<IFirmwareSerialDeviceCatalog>().DidNotReceiveWithAnyArgs().GetDevicesAsync(TestContext.Current.CancellationToken);
        await panel.DeactivateAsync();
    }

    [Fact]
    public async Task CatalogueUnloadCancelsAndJoinsLateResultsAndClosesDialog()
    {
        using var services = FirmwarePanelViewModelTests.CreateServices();
        services.GetRequiredService<IActiveVehicleContext>().IsOnline.Returns(false);
        var service = services.GetRequiredService<IFirmwareCatalogService>();
        var handle = Substitute.For<IDisposable>();
        services.GetRequiredService<IDialogService>().DisplayProgressCancellableAsync(Arg.Any<Func<string>>(), Arg.Any<DialogOptions>(), Arg.Any<CancellationToken>()).Returns(handle);
        var panel = services.GetRequiredService<FirmwareCatalogueViewModel>();
        for (var iteration = 0; iteration < 10; iteration++)
        {
            var started = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
            var result = new TaskCompletionSource<FirmwareCatalog>(TaskCreationOptions.RunContinuationsAsynchronously);
            service.GetCatalogAsync(Arg.Any<FirmwareCatalogRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
            {
                started.TrySetResult(call.Arg<CancellationToken>());
                return result.Task;
            });
            var activation = panel.ActivateAsync();
            var token = await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            var closing = panel.DeactivateAsync();
            Assert.True(token.IsCancellationRequested);
            Assert.False(closing.IsCompleted);
            result.SetResult(Catalog(Entry()));
            await Task.WhenAll(activation, closing);
            Assert.Empty(panel.FirmwareChoices);
            Assert.False(panel.IsRefreshing);
        }
        handle.Received(10).Dispose();
    }

    [Fact]
    public async Task SerialPanelDiscoversWithoutCatalogueAndRetainsExplicitDevice()
    {
        using var services = FirmwarePanelViewModelTests.CreateServices();
        services.GetRequiredService<IActiveVehicleContext>().IsOnline.Returns(false);
        services.GetRequiredService<IFirmwareSerialDeviceCatalog>().GetDevicesAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { new SerialDeviceDescriptor("COM11"), new SerialDeviceDescriptor("COM14") });
        var panel = services.GetRequiredService<DetectedDeviceViewModel>();
        await panel.ActivateAsync();
        Assert.Equal(2, panel.DetectedDevices.Count);
        Assert.Equal("COM11", panel.SelectedDevice!.Descriptor.PortName);
        panel.SelectedDevice = panel.DetectedDevices[1];
        await panel.DeactivateAsync();
        await panel.ActivateAsync();
        Assert.Equal("COM14", panel.SelectedDevice!.Descriptor.PortName);
        services.GetRequiredService<IFirmwareSerialDeviceCatalog>().GetDevicesAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { new SerialDeviceDescriptor("COM11") });
        await panel.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.Equal("COM11", panel.SelectedDevice!.Descriptor.PortName);
        services.GetRequiredService<IFirmwareSerialDeviceCatalog>().GetDevicesAsync(Arg.Any<CancellationToken>())
            .Returns(Array.Empty<SerialDeviceDescriptor>());
        await panel.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.Null(panel.SelectedDevice);
        await services.GetRequiredService<IFirmwareCatalogService>().DidNotReceiveWithAnyArgs().GetCatalogAsync(default!, TestContext.Current.CancellationToken);
        await panel.DeactivateAsync();
    }

    [Fact]
    public async Task DfuPanelDiscoversDevicesAndToolWithoutCatalogueOrParent()
    {
        using var services = FirmwarePanelViewModelTests.CreateServices();
        services.GetRequiredService<IActiveVehicleContext>().IsOnline.Returns(false);
        services.GetRequiredService<IDfuDeviceCatalog>().GetDevicesAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { new DfuDeviceDescriptor("test-device", 0x0483, 0xdf11, DfuDriverState.PresentReady) });
        services.GetRequiredService<IDfuToolLocator>().LocateAsync(Arg.Any<CancellationToken>())
            .Returns(new DfuToolStatus(DfuToolAvailability.Available));
        var panel = services.GetRequiredService<STM32BootloaderViewModel>();
        await panel.ActivateAsync();
        Assert.True(panel.HasDetectedDfuDevice);
        Assert.Contains("ready", panel.DfuStatus);
        await services.GetRequiredService<IFirmwareCatalogService>().DidNotReceiveWithAnyArgs().GetCatalogAsync(default!, TestContext.Current.CancellationToken);
        await panel.DeactivateAsync();
    }

    [Fact]
    public async Task InstallingPreventsPanelReads()
    {
        using var services = FirmwarePanelViewModelTests.CreateServices();
        services.GetRequiredService<IActiveVehicleContext>().IsOnline.Returns(false);
        var catalogue = services.GetRequiredService<FirmwareCatalogueViewModel>();
        var serial = services.GetRequiredService<DetectedDeviceViewModel>();
        var dfu = services.GetRequiredService<STM32BootloaderViewModel>();
        catalogue.InstallationRunning = serial.InstallationRunning = dfu.InstallationRunning = true;
        await Task.WhenAll(catalogue.ActivateAsync(), serial.ActivateAsync(), dfu.ActivateAsync());
        await services.GetRequiredService<IFirmwareCatalogService>().DidNotReceiveWithAnyArgs().GetCatalogAsync(default!, TestContext.Current.CancellationToken);
        await services.GetRequiredService<IFirmwareSerialDeviceCatalog>().DidNotReceiveWithAnyArgs().GetDevicesAsync(TestContext.Current.CancellationToken);
        await services.GetRequiredService<IDfuDeviceCatalog>().DidNotReceiveWithAnyArgs().GetDevicesAsync(TestContext.Current.CancellationToken);
        await Task.WhenAll(catalogue.DeactivateAsync(), serial.DeactivateAsync(), dfu.DeactivateAsync());
    }

    [Fact]
    public async Task ChangingChannelDuringLoadCancelsOldResultAndLoadsLatestChannel()
    {
        using var services = FirmwarePanelViewModelTests.CreateServices();
        services.GetRequiredService<IActiveVehicleContext>().IsOnline.Returns(false);
        var service = services.GetRequiredService<IFirmwareCatalogService>();
        var firstStarted = new TaskCompletionSource<CancellationToken>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstResult = new TaskCompletionSource<FirmwareCatalog>(TaskCreationOptions.RunContinuationsAsynchronously);
        var beta = Entry(FirmwareReleaseChannel.Beta);
        service.GetCatalogAsync(Arg.Any<FirmwareCatalogRequest>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (call.Arg<FirmwareCatalogRequest>()?.Channel == FirmwareReleaseChannel.Beta)
            {
                return Task.FromResult(Catalog(beta));
            }
            firstStarted.TrySetResult(call.Arg<CancellationToken>());
            return firstResult.Task;
        });
        var panel = services.GetRequiredService<FirmwareCatalogueViewModel>();
        var applied = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        panel.FirmwareChoices.CollectionChanged += (_, _) =>
        {
            if (panel.FirmwareChoices.Any(item => item.Entry.Channel == FirmwareReleaseChannel.Beta))
            {
                applied.TrySetResult();
            }
        };
        var activation = panel.ActivateAsync();
        var firstToken = await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        panel.SelectedChannel = FirmwareReleaseChannel.Beta;
        Assert.True(firstToken.IsCancellationRequested);
        firstResult.SetResult(Catalog(Entry()));
        await activation;
        await applied.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await panel.DeactivateAsync();
        Assert.Same(beta, Assert.Single(panel.FirmwareChoices).Entry);
        Assert.False(panel.IsRefreshing);
    }

    [Fact]
    public async Task CatalogueFailureReleasesDialogAndAllowsRetry()
    {
        using var services = FirmwarePanelViewModelTests.CreateServices();
        services.GetRequiredService<IActiveVehicleContext>().IsOnline.Returns(false);
        var service = services.GetRequiredService<IFirmwareCatalogService>();
        service.GetCatalogAsync(Arg.Any<FirmwareCatalogRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<FirmwareCatalog>(new IOException("fixture failure")));
        var handle = Substitute.For<IDisposable>();
        services.GetRequiredService<IDialogService>().DisplayProgressCancellableAsync(Arg.Any<Func<string>>(), Arg.Any<DialogOptions>(), Arg.Any<CancellationToken>()).Returns(handle);
        var panel = services.GetRequiredService<FirmwareCatalogueViewModel>();
        await panel.ActivateAsync();
        Assert.False(panel.IsBusy);
        Assert.False(panel.IsRefreshing);
        Assert.NotNull(panel.ErrorMessage);
        service.GetCatalogAsync(Arg.Any<FirmwareCatalogRequest>(), Arg.Any<CancellationToken>()).Returns(Catalog(Entry()));
        await panel.RefreshAsync(true, TestContext.Current.CancellationToken);
        Assert.Single(panel.FirmwareChoices);
        handle.Received(2).Dispose();
        await panel.DeactivateAsync();
    }

    private static FirmwareManifestEntry Entry(FirmwareReleaseChannel channel = FirmwareReleaseChannel.Stable)
    {
        return new(new FirmwareVersion("4.6.0"), channel,
        new FirmwareBoardTarget(50, "test", FirmwareVehicleType.Copter),
        new FirmwareArtifact(new Uri("https://example.test/firmware.apj"), FirmwareImageFormat.Apj));
    }

    private static FirmwareCatalog Catalog(params FirmwareManifestEntry[] entries)
    {
        return new(entries, DateTimeOffset.UnixEpoch, false);
    }
}
