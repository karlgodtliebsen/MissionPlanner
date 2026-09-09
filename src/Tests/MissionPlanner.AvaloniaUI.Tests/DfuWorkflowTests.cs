using Microsoft.Extensions.DependencyInjection;
using MissionPlanner.App.Views.InitSetup.InstallFirmware;
using MissionPlanner.App.Views.InitSetup.InstallFirmware.SubViews;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Firmware.Dfu;
using MissionPlanner.Firmware.Model;
using NSubstitute;

namespace MissionPlanner.AvaloniaUI.Tests;

public sealed class DfuWorkflowTests
{
    [Theory]
    [InlineData("firmware.apj", true, false)]
    [InlineData("firmware.hex", true, false)]
    [InlineData("firmware_with_bl.hex", false, false)]
    [InlineData("firmware_with_bl.hex", true, true)]
    public async Task CustomHexSelectionValidatesNameAndLocalPath(string name, bool local, bool accepted)
    {
        using var services = FirmwarePanelViewModelTests.CreateServices();
        var dfu = services.GetRequiredService<STM32BootloaderViewModel>();
        dfu.SelectedDfuDevice = new(new("usb", 0x0483, 0xDF11, DfuDriverState.PresentReady));
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + "_with_bl.hex");
        await File.WriteAllTextAsync(path, "fixture", TestContext.Current.CancellationToken);
        try
        {
            services.GetRequiredService<IFirmwareFilePicker>().PickAsync(Arg.Any<CancellationToken>())
                .Returns(new FirmwareFileSelection(name, _ => throw new InvalidOperationException("Picker must not parse APJ"), local ? path : null));
            await dfu.LoadCustomBlWithFirmwareCommand.ExecuteAsync(null);
            Assert.Equal(accepted, dfu.HasLocalDfuFirmware);
            dfu.ClearLocalDfuFirmwareCommand.Execute(null);
            Assert.False(dfu.HasLocalDfuFirmware);
            Assert.Null(dfu.LocalDfuPlatform);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreviewUsesSelectedSourceContextAndExistingResolverWithoutProgramming(bool custom)
    {
        using var services = FirmwarePanelViewModelTests.CreateServices();
        var parent = services.GetRequiredService<InstallFirmwareViewModel>();
        await parent.ActivateAsync();
        services.GetRequiredService<IActiveVehicleContext>().IsOnline.Returns(false);
        var device = new DfuDeviceDescriptor("usb", 0x0483, 0xDF11, DfuDriverState.PresentReady);
        services.GetRequiredService<IDfuDeviceCatalog>().GetDevicesAsync(Arg.Any<CancellationToken>()).Returns(new[] { device });
        services.GetRequiredService<IDfuToolLocator>().LocateAsync(Arg.Any<CancellationToken>())
            .Returns(new DfuToolStatus(DfuToolAvailability.Available));
        await parent.Dfu.RefreshAsync(TestContext.Current.CancellationToken);
        parent.SelectedDfuTabIndex = custom ? (int)Stm32DfuSection.Custom : (int)Stm32DfuSection.Catalogue;
        parent.Dfu.LocalDfuFirmwarePath = "retained_with_bl.hex";
        parent.Dfu.LocalDfuPlatform = "LocalBoard";
        var entry = new FirmwareManifestEntry(new FirmwareVersion("4.6.0"), FirmwareReleaseChannel.Stable,
            new FirmwareBoardTarget(50, "Board", FirmwareVehicleType.Copter),
            new FirmwareArtifact(new Uri("https://firmware.ardupilot.org/Copter/stable/Board/arducopter.apj"), FirmwareImageFormat.Apj));
        parent.Catalogue.SetCatalogue([entry], [], true);
        parent.Catalogue.SelectedFirmware = parent.Catalogue.FirmwareChoices.Single();
        Assert.True(parent.Dfu.CanInstallDfu);
        var artifact = new DfuArtifact("arducopter_with_bl.hex", "prepared.hex",
            new DfuArtifactMetadata(100, 1, 0x08000000, 0x08000000, "hex-hash", [], []),
            Platform: custom ? "LocalBoard" : "Board");
        var resolver = services.GetRequiredService<IDfuArtifactResolver>();
        resolver.ResolveAsync(Arg.Any<DfuInstallationRequest>(), Arg.Any<CancellationToken>()).Returns(artifact);
        await parent.Dfu.PrepareDfuCommand.ExecuteAsync(null);
        Assert.Same(artifact, parent.Dfu.PreparedArtifact);
        Assert.Null(parent.Validated.PreparedFirmware);
        await resolver.Received(1).ResolveAsync(Arg.Is<DfuInstallationRequest>(request => custom
            ? request.LocalHexPath == "retained_with_bl.hex" && request.ManifestEntry == null && request.SelectedPlatform == "LocalBoard"
            : request.LocalHexPath == null && request.ManifestEntry == entry && request.SelectedPlatform == "Board"), Arg.Any<CancellationToken>());
        await services.GetRequiredService<IDfuInstallationService>().DidNotReceiveWithAnyArgs()
            .InstallAsync(default!, default, TestContext.Current.CancellationToken);
        services.GetRequiredService<IDfuToolLocator>().LocateAsync(Arg.Any<CancellationToken>())
            .Returns(new DfuToolStatus(DfuToolAvailability.NotInstalled));
        await parent.Dfu.RefreshAsync(TestContext.Current.CancellationToken);
        Assert.False(parent.Dfu.CanInstallDfu);
        await parent.DeactivateAsync();
    }

    [Fact]
    public async Task AnonymousDfuDoesNotInventAnArduPilotTarget()
    {
        using var services = FirmwarePanelViewModelTests.CreateServices();
        var landing = services.GetRequiredService<FirmwareLandingViewModel>();
        landing.Dfu.SelectedDfuDevice = new(new("usb", 0x0483, 0xDF11, DfuDriverState.PresentReady));
        await landing.ActivateAsync();
        Assert.Contains("No preceding controller identity", landing.DfuIdentitySummary);
        Assert.False(landing.Dfu.HasCorrelatedSource);
        Assert.Null(services.GetRequiredService<FirmwareCatalogViewModel>().SelectedFirmware);
        await landing.DeactivateAsync();
    }
}
