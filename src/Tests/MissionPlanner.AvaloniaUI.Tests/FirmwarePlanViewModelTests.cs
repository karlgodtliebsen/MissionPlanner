using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.App.Views.InitSetup.InstallFirmware;
using MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews;
using MissionPlanner.Firmware.Catalog;
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
    public async Task SuccessfulSerialInstallAlsoReturnsToLanding()
    {
        using var services = Services(null);
        var page = services.GetRequiredService<InstallFirmwareViewModel>();
        await page.ActivateAsync();
        page.SelectedTabIndex = 1;
        PrepareOnline(page);
        page.DevicesModel.SelectedDevice = new(new SerialDeviceDescriptor("COM10")
        {
            BootloaderIdentity = new(50, 5, 1024)
        }, true, "Protocol identity");
        Assert.True(page.InstallCommand.CanExecute(null));
        services.GetRequiredService<IFirmwareInstallationService>()
            .InstallAsync(Arg.Any<FirmwareInstallationRequest>(), Arg.Any<IProgress<FirmwareProgress>>(), Arg.Any<CancellationToken>())
            .Returns(new FirmwareOperationResult(Guid.NewGuid(), FirmwareOperationKind.InstallApplicationFirmware,
                FirmwareOperationState.Completed));
        services.GetRequiredService<IDialogService>()
            .ShowOverlayDialogAsync<DiagnosticsReportView, DiagnosticsReportViewModel>(Arg.Any<DiagnosticsReportViewModel>(),
                Arg.Any<Ursa.Controls.OverlayDialogOptions>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<DiagnosticsReportViewModel>(null!));
        await page.InstallCommand.ExecuteAsync(null);
        Assert.Equal(0, page.SelectedTabIndex);
        Assert.False(page.IsFirmwareSelected);
        Assert.False(page.IsOperationInProgress);
        Assert.False(page.ShowValidationAndCompatibility);
        await page.DeactivateAsync();
    }

    [Theory]
    [InlineData(DfuOperationState.Completed)]
    [InlineData(DfuOperationState.Failed)]
    [InlineData(DfuOperationState.Cancelled)]
    public async Task SuccessfulDfuReturnsToLandingAndRediscoversAfterDiagnosticsClose(DfuOperationState outcome)
    {
        using var services = Services(null);
        var page = services.GetRequiredService<InstallFirmwareViewModel>();
        await page.ActivateAsync();
        page.SelectedTabIndex = 1;
        services.GetRequiredService<IDfuToolLocator>().LocateAsync(Arg.Any<CancellationToken>())
            .Returns(new DfuToolStatus(DfuToolAvailability.Available));
        await page.DfuModel.RefreshAsync(TestContext.Current.CancellationToken);
        PrepareOnline(page);
        page.DfuModel.SelectedDfuDevice = new(new("usb", 0x0483, 0xdf11, DfuDriverState.PresentReady));
        var metadata = new DfuArtifactMetadata(100, 4, 0x08000000, 0x08000003, new string('a', 64),
            [new DfuMemoryRange(0x08000000, new byte[4])], []);
        page.DfuModel.PreparedArtifact = new("firmware_with_bl.hex", "cache.hex", metadata, Metadata().SourceUri, "Board", 50);
        page.DfuConfirmationText = "FLASH Board";
        await page.ReviewDfuTargetCommand.ExecuteAsync(null);
        Assert.True(page.InstallDfuFirmwareCommand.CanExecute(null));
        services.GetRequiredService<IDfuInstallationService>()
            .InstallAsync(Arg.Any<DfuInstallationRequest>(), Arg.Any<IProgress<DfuProgress>>(), Arg.Any<CancellationToken>())
            .Returns(new DfuProgrammingResult(outcome, outcome == DfuOperationState.Completed, outcome == DfuOperationState.Completed, false));
        var shown = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var closed = new TaskCompletionSource<DiagnosticsReportViewModel>(TaskCreationOptions.RunContinuationsAsynchronously);
        services.GetRequiredService<IDialogService>()
            .ShowOverlayDialogAsync<DiagnosticsReportView, DiagnosticsReportViewModel>(Arg.Any<DiagnosticsReportViewModel>(),
                Arg.Any<Ursa.Controls.OverlayDialogOptions>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                shown.TrySetResult();
                return closed.Task;
            });
        var install = page.InstallDfuFirmwareCommand.ExecuteAsync(null);
        if (outcome == DfuOperationState.Completed)
        {
            await shown.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
            Assert.Equal(1, page.SelectedTabIndex);
            Assert.True(page.IsFirmwareSelected);
            // Simulate the reconnected application becoming visible while the report is open.
            services.GetRequiredService<MissionPlanner.Firmware.Devices.IFirmwareSerialDeviceCatalog>()
                .GetDevicesAsync(Arg.Any<CancellationToken>()).Returns(new[] { new SerialDeviceDescriptor("COM11") });
            closed.SetResult(null!);
        }
        await install.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        Assert.False(page.IsOperationInProgress);
        if (outcome == DfuOperationState.Completed)
        {
            Assert.Equal(0, page.SelectedTabIndex);
            Assert.False(page.IsFirmwareSelected);
            Assert.Null(page.DfuConfirmationText);
            Assert.Null(page.DfuModel.PreparedArtifact);
            Assert.Empty(page.DfuModel.DfuDevices);
            Assert.Contains("COM11", services.GetRequiredService<FirmwareLandingViewModel>().SerialSummary);
        }
        else
        {
            Assert.Equal(1, page.SelectedTabIndex);
            Assert.True(page.IsFirmwareSelected);
        }
        await page.DeactivateAsync();
    }

    [Fact]
    public async Task LocalSelectorLoadsPlatformsWithoutOpeningOnlineSelector()
    {
        using var services = Services(null);
        var page = services.GetRequiredService<InstallFirmwareViewModel>();
        await page.ActivateAsync();
        page.DfuModel.SelectedDfuDevice = new(new("usb", 0x0483, 0xdf11, DfuDriverState.PresentReady));
        var entry = new FirmwareManifestEntry(new FirmwareVersion("4.6.0"), FirmwareReleaseChannel.Stable,
            new FirmwareBoardTarget(50, "Board", FirmwareVehicleType.Copter),
            new FirmwareArtifact(Metadata().SourceUri, FirmwareImageFormat.Apj));
        services.GetRequiredService<IFirmwareCatalogService>()
            .GetCatalogAsync(Arg.Any<FirmwareCatalogRequest>(), Arg.Any<CancellationToken>())
            .Returns(new FirmwareCatalog([entry], DateTimeOffset.UtcNow, false));
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + "_with_bl.hex");
        await File.WriteAllTextAsync(path, "fixture", TestContext.Current.CancellationToken);
        try
        {
            services.GetRequiredService<IFirmwareFilePicker>()
                .PickAsync(FirmwareArtifactFormat.WithBootloaderHex, Arg.Any<CancellationToken>())
                .Returns(new FirmwareFileSelection("firmware_with_bl.hex", _ => throw new InvalidOperationException(), path));
            Assert.Empty(page.OnlineFirmwareModel.KnownPlatforms);
            await page.ShowLocalFirmwareSelectorCommand.ExecuteAsync(null);
            Assert.Contains("Board", page.OnlineFirmwareModel.KnownPlatforms);
            Assert.Equal(path, page.SelectedArtifact.LocalFile);
            Assert.Null(page.OnlineFirmwareModel.SelectedFirmware);
            Assert.False(page.ShowValidationAndCompatibility);
            page.LocalDfuPlatform = "Board";
            Assert.True(page.PrepareSelectedHexCommand.CanExecute(null));
        }
        finally
        {
            await page.DeactivateAsync();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ClosingOnlineSelectorPreparesDfuAndExposesInlineConfirmation()
    {
        using var services = Services(null);
        var page = services.GetRequiredService<InstallFirmwareViewModel>();
        await page.ActivateAsync();
        page.DfuModel.SelectedDfuDevice = new(new("usb", 0x0483, 0xdf11, DfuDriverState.PresentReady));
        services.GetRequiredService<IDialogService>()
            .CreateOptions(Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>())
            .Returns(new Ursa.Controls.OverlayDialogOptions());
        services.GetRequiredService<IDialogService>()
            .ShowOverlayDialogAsync<FirmwareCatalogueView, FirmwareCatalogueViewModel>(
                Arg.Any<FirmwareCatalogueViewModel>(), Arg.Any<Ursa.Controls.OverlayDialogOptions>(),
                Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                PrepareOnline(page);
                page.ValidatedModel.PreparedFirmware = null;
                return page.OnlineFirmwareModel;
            });
        var metadata = new DfuArtifactMetadata(100, 4, 0x08000000, 0x08000003, new string('a', 64),
            [new DfuMemoryRange(0x08000000, new byte[4])], []);
        services.GetRequiredService<IDfuArtifactResolver>()
            .ResolveAsync(Arg.Any<DfuInstallationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new DfuArtifact("firmware_with_bl.hex", "cache.hex", metadata, Metadata().SourceUri, "Board", 50));
        await page.ShowOnlineFirmwareSelectorCommand.ExecuteAsync(null);
        Assert.True(page.ShowValidationAndCompatibility);
        Assert.True(page.ShowDfuConfirmation);
        Assert.False(page.ShowOnlineValidation);
        Assert.Null(page.LocalDfuPlatform);
        Assert.Contains("FLASH Board", page.DfuConfirmationPlaceholder);
        await page.DeactivateAsync();
    }

    [Fact]
    public async Task OnlineDfuSelectionShowsReleaseDetailsAndEnablesHexPreparationWithoutLocalPlatform()
    {
        using var services = Services(null);
        var page = services.GetRequiredService<InstallFirmwareViewModel>();
        await page.ActivateAsync();
        page.DfuModel.SelectedDfuDevice = new(new("usb", 0x0483, 0xdf11, DfuDriverState.PresentReady));
        PrepareOnline(page);
        page.ValidatedModel.PreparedFirmware = null;
        Assert.Null(page.LocalDfuPlatform);
        Assert.Equal("Official catalogue", page.SelectedArtifact.Source);
        Assert.Equal("Board", page.SelectedArtifact.Platform);
        Assert.Equal("4.6.0", page.SelectedArtifact.Version);
        Assert.Equal(50, page.SelectedArtifact.BoardId);
        Assert.True(page.ShowHexPreparation);
        Assert.True(page.PrepareSelectedHexCommand.CanExecute(null));
        Assert.True(page.ShowValidationAndCompatibility);
        Assert.True(page.ShowOnlineValidation);
        Assert.Contains("Validate combined HEX", page.WorkflowNextStep);

        services.GetRequiredService<IDfuArtifactResolver>()
            .ResolveAsync(Arg.Any<DfuInstallationRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromException<DfuArtifact>(new InvalidOperationException("Invalid HEX checksum")));
        await page.PrepareSelectedHexCommand.ExecuteAsync(null);
        Assert.True(page.ShowValidationAndCompatibility);
        Assert.False(page.ShowDfuConfirmation);
        Assert.Contains("Invalid HEX checksum", page.WorkflowProgress);
        Assert.Contains("Validate combined HEX", page.WorkflowNextStep);
        page.ClearFirmwareSelectionCommand.Execute(null);
        Assert.DoesNotContain("Invalid HEX checksum", page.WorkflowProgress);
        Assert.Contains("Select online firmware", page.WorkflowNextStep);
        await page.DeactivateAsync();
    }

    [Fact]
    public async Task SelectedLocalHexShowsProvenanceBeforeValidation()
    {
        using var services = Services(null);
        var page = services.GetRequiredService<InstallFirmwareViewModel>();
        await page.ActivateAsync();
        page.DfuModel.LocalDfuFirmwarePath = "Board_with_bl.hex";
        Assert.Equal("Local file", page.SelectedArtifact.Source);
        Assert.Equal("Board_with_bl.hex", page.SelectedArtifact.LocalFile);
        Assert.Equal(FirmwareArtifactFormat.WithBootloaderHex, page.SelectedArtifact.Format);
        Assert.True(page.HasLocalArtifact);
        Assert.True(page.IsFirmwareSelected);
        Assert.False(page.SelectedArtifact.ArtifactValid);
        Assert.Null(page.SelectedArtifact.Sha256);
        Assert.False(page.CurrentPlan.CanExecute);

        page.DfuModel.LocalDfuPlatform = " Board ";
        Assert.Equal("Board", page.SelectedArtifact.Platform);
        Assert.False(page.SelectedArtifact.ArtifactValid);
        page.DfuModel.LocalDfuFirmwarePath = "Other_with_bl.hex";
        Assert.Equal("Other_with_bl.hex", page.SelectedArtifact.LocalFile);

        page.ClearFirmwareSelectionCommand.Execute(null);
        Assert.False(page.HasLocalArtifact);
        Assert.False(page.IsFirmwareSelected);
        Assert.Equal("No firmware selected", page.SelectedArtifact.Source);
        await page.DeactivateAsync();
    }

    [Fact]
    public async Task ControllerChoicesRemainVisibleWithoutASelectionAndHideWhenRemoved()
    {
        using var services = Services(null);
        var page = services.GetRequiredService<InstallFirmwareViewModel>();
        await page.ActivateAsync();
        page.DevicesModel.DetectedDevices = [new(new SerialDeviceDescriptor("COM10"), false, "test")];
        Assert.True(page.ShowPhysicalController);
        Assert.False(page.HasPhysicalController);
        page.DevicesModel.DetectedDevices = [];
        Assert.False(page.ShowPhysicalController);
        await page.DeactivateAsync();
    }

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
            {
                Verification = FirmwareRuntimeVerification.Verified
            }
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
        Assert.Contains("Select online firmware or a local firmware file", page.WorkflowNextStep);
        PrepareOnline(page);
        page.DfuModel.LocalDfuFirmwarePath = "Board_with_bl.hex";
        page.DfuModel.LocalDfuPlatform = "an arbitrary sentence";
        Assert.False(page.PrepareSelectedHexCommand.CanExecute(null));
        page.DfuModel.LocalDfuPlatform = "Board";
        Assert.True(page.IsFirmwareSelected);
        Assert.True(page.HasPhysicalController);
        Assert.False(page.ShowValidationAndCompatibility);
        Assert.Contains("Firmware selected", page.WorkflowProgress);
        Assert.Contains("Validate combined HEX", page.WorkflowNextStep);
        Assert.False(page.CanValidateCompatibility);
        var metadata = new DfuArtifactMetadata(100, 4, 0x08000000, 0x08000003, new string('a', 64),
            [new DfuMemoryRange(0x08000000, new byte[4])], []);
        services.GetRequiredService<IDfuArtifactResolver>().ResolveAsync(Arg.Any<DfuInstallationRequest>(), Arg.Any<CancellationToken>())
            .Returns(new DfuArtifact("Board_with_bl.hex", "Board_with_bl.hex", metadata, Platform: "Board"));
        await page.PrepareSelectedHexCommand.ExecuteAsync(null);
        Assert.True(page.CanValidateCompatibility);
        Assert.True(page.SelectedArtifact.ArtifactValid);
        Assert.False(page.CurrentPlan.CanExecute);
        Assert.True(page.ShowValidationAndCompatibility);
        Assert.Contains("Combined HEX validated", page.WorkflowProgress);
        Assert.Contains("FLASH Board", page.WorkflowNextStep);
        page.DfuConfirmationText = "wrong";
        Assert.False(page.ReviewDfuTargetCommand.CanExecute(null));
        page.DfuConfirmationText = "FLASH Board";
        Assert.True(page.ReviewDfuTargetCommand.CanExecute(null));
        await page.ReviewDfuTargetCommand.ExecuteAsync(null);
        Assert.True(page.CurrentPlan.CanExecute);
        Assert.True(page.ExecuteCurrentPlanCommand.CanExecute(null));
        Assert.Contains("DFU controller target confirmed", page.WorkflowProgress);
        Assert.Contains("Click Install firmware", page.WorkflowNextStep);
        page.DfuModel.SelectedDfuDevice = new(new("other-usb", 0x0483, 0xdf11, DfuDriverState.PresentReady));
        Assert.False(page.CurrentPlan.CanExecute);
        Assert.Null(page.DfuConfirmationText);
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
        Assert.False(page.IsFirmwareSelected);
        Assert.False(page.HasPhysicalController);
        Assert.False(page.ShowPhysicalController);
        Assert.False(page.ShowValidationAndCompatibility);
        Assert.False(page.CanValidateCompatibility);
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
        Assert.True(page.IsFirmwareSelected);
        Assert.True(page.HasPhysicalController);
        Assert.True(page.ShowPhysicalController);
        Assert.True(page.ShowValidationAndCompatibility);
        Assert.True(page.CanValidateCompatibility);
        Assert.Equal(executable, page.CurrentPlan.CanExecute);
        Assert.Equal(executable, page.InstallCommand.CanExecute(null));
        Assert.Equal(executable, page.ExecuteCurrentPlanCommand.CanExecute(null));
        Assert.Equal(!local, page.HasOnlineArtifact);
        var selectedDevice = page.DevicesModel.SelectedDevice;
        page.ClearFirmwareSelectionCommand.Execute(null);
        Assert.Same(selectedDevice, page.DevicesModel.SelectedDevice);
        Assert.True(page.HasPhysicalController);
        Assert.True(page.ShowPhysicalController);
        Assert.False(page.IsFirmwareSelected);
        Assert.False(page.ShowValidationAndCompatibility);
        Assert.False(page.CanValidateCompatibility);
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

    private static ApjFirmwarePackage Package()
    {
        return new(50, new byte[] { 1, 2, 3 }, 1024);
    }

    private static MissionPlanner.Firmware.Downloads.FirmwareArtifactMetadata Metadata()
    {
        return new("cache", new Uri("https://example.test/firmware.apj"), DateTimeOffset.UtcNow, 3, new string('a', 64));
    }

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
