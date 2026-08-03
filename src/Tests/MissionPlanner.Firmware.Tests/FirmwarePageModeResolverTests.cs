using FluentAssertions;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Presentation;

namespace MissionPlanner.Firmware.Tests;

/// <summary>Verifies firmware-page modes and command policy.</summary>
public sealed class FirmwarePageModeResolverTests
{
    private readonly FirmwarePageModeResolver resolver = new();

    /// <summary>Verifies normal install actions and catalogue features in disconnected mode.</summary>
    [Fact]
    public void DisconnectedModeEnablesNormalFirmwareWorkflow()
    {
        var state = resolver.Resolve(Context());

        state.Mode.Should().Be(FirmwarePageMode.Disconnected);
        state.ShowCatalogue.Should().BeTrue();
        state.ShowReleaseChannels.Should().BeTrue();
        state.ShowAllOptions.Should().BeTrue();
        state.ShowCustomFirmware.Should().BeTrue();
        state.ShowDeviceStatus.Should().BeTrue();
        state.CanInstallApplicationFirmware.Should().BeTrue();
        state.CanUpdateEmbeddedBootloader.Should().BeFalse();
    }

    /// <summary>Verifies a connection immediately removes every normal flashing action.</summary>
    [Fact]
    public void ConnectedModeDisablesNormalFirmwareWorkflowImmediately()
    {
        var disconnected = resolver.Resolve(Context());
        var connected = resolver.Resolve(Context() with { IsVehicleConnected = true });

        disconnected.Mode.Should().Be(FirmwarePageMode.Disconnected);
        connected.Mode.Should().Be(FirmwarePageMode.Connected);
        connected.ShowConnectionWarning.Should().BeTrue();
        connected.ShowCatalogue.Should().BeFalse();
        connected.ShowCustomFirmware.Should().BeFalse();
        connected.CanInstallApplicationFirmware.Should().BeFalse();
    }

    /// <summary>Verifies bootloader update requires a connected, supported, disarmed ArduPilot vehicle.</summary>
    [Theory]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, true, false)]
    [InlineData(true, false, false, false)]
    [InlineData(false, false, true, false)]
    public void BootloaderUpdateRequiresAllPreconditions(
        bool connected,
        bool armed,
        bool supportedArduPilot,
        bool expected)
    {
        var state = resolver.Resolve(Context() with
        {
            IsVehicleConnected = connected,
            IsVehicleArmed = armed,
            IsSupportedArduPilot = supportedArduPilot
        });

        state.CanUpdateEmbeddedBootloader.Should().Be(expected);
    }

    /// <summary>Verifies an active operation exposes only progress and prevents unsafe navigation.</summary>
    [Fact]
    public void OperationModeSuppressesActionsAndNavigation()
    {
        var state = resolver.Resolve(Context() with
        {
            IsOperationInProgress = true,
            OperationState = FirmwareOperationState.Erasing
        });

        state.Mode.Should().Be(FirmwarePageMode.OperationInProgress);
        state.ShowProgress.Should().BeTrue();
        state.OperationState.Should().Be(FirmwareOperationState.Erasing);
        state.CanInstallApplicationFirmware.Should().BeFalse();
        state.CanUpdateEmbeddedBootloader.Should().BeFalse();
        state.CanNavigateAway.Should().BeFalse();
    }

    /// <summary>Verifies unsupported platforms explain the limitation without offering direct actions.</summary>
    [Fact]
    public void UnsupportedPlatformSuppressesDirectActions()
    {
        var state = resolver.Resolve(Context() with { IsDirectInstallationSupported = false });

        state.Mode.Should().Be(FirmwarePageMode.UnsupportedPlatform);
        state.ShowCatalogue.Should().BeFalse();
        state.ShowDeviceStatus.Should().BeFalse();
        state.CanInstallApplicationFirmware.Should().BeFalse();
        state.CanUpdateEmbeddedBootloader.Should().BeFalse();
        state.CanNavigateAway.Should().BeTrue();
    }

    private static FirmwarePageContext Context() => new(
        IsDirectInstallationSupported: true,
        IsVehicleConnected: false,
        IsVehicleArmed: false,
        IsSupportedArduPilot: true,
        IsOperationInProgress: false);
}
