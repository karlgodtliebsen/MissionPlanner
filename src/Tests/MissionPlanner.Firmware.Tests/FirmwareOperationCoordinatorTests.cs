using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.Firmware.Exceptions;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Firmware.Operations;

namespace MissionPlanner.Firmware.Tests;

public sealed class FirmwareOperationCoordinatorTests
{
    [Fact]
    public void InstallPathPublishesProgressInTransitionOrder()
    {
        var coordinator = CreateCoordinator();
        using var operation = coordinator.Begin(FirmwareOperationKind.InstallApplicationFirmware);
        var observed = new List<FirmwareOperationState>();
        operation.ProgressChanged += (_, progress) => observed.Add(progress.State);

        foreach (var state in InstallPath)
        {
            operation.Transition(Progress(state));
        }

        operation.State.Should().Be(FirmwareOperationState.Completed);
        observed.Should().Equal(InstallPath);
        operation.OperationId.Should().NotBeEmpty();
    }

    [Fact]
    public void IllegalTransitionThrowsTypedExceptionAndPreservesState()
    {
        var coordinator = CreateCoordinator();
        var operation = coordinator.Begin(FirmwareOperationKind.InstallApplicationFirmware);

        var act = () => operation.Transition(Progress(FirmwareOperationState.Programming));

        act.Should().Throw<FirmwareStateTransitionException>();
        operation.State.Should().Be(FirmwareOperationState.Idle);
        operation.RequestCancellation().Should().BeTrue();
        operation.Dispose();
    }

    [Fact]
    public void ConcurrentOperationFailsUntilTerminalOperationIsReleased()
    {
        var coordinator = CreateCoordinator();
        var first = coordinator.Begin(FirmwareOperationKind.InstallApplicationFirmware);

        var act = () => coordinator.Begin(FirmwareOperationKind.UpdateEmbeddedBootloader);

        act.Should().Throw<FirmwareBusyException>();
        first.RequestCancellation().Should().BeTrue();
        first.Dispose();
        using var second = coordinator.Begin(FirmwareOperationKind.UpdateEmbeddedBootloader);
        second.OperationId.Should().NotBe(first.OperationId);
        second.RequestCancellation().Should().BeTrue();
    }

    [Fact]
    public void CancellationDuringProgrammingIsDeferred()
    {
        var coordinator = CreateCoordinator();
        using var operation = coordinator.Begin(FirmwareOperationKind.InstallApplicationFirmware);
        foreach (var state in InstallPath.TakeWhile(state => state != FirmwareOperationState.Verifying))
        {
            operation.Transition(Progress(state));
        }

        operation.RequestCancellation().Should().BeFalse();
        operation.CancellationRequested.Should().BeTrue();
        operation.State.Should().Be(FirmwareOperationState.Programming);
        operation.Transition(Progress(FirmwareOperationState.Verifying));
        operation.Transition(Progress(FirmwareOperationState.Rebooting));
        operation.Transition(Progress(FirmwareOperationState.WaitingForApplication));
        operation.Transition(Progress(FirmwareOperationState.Completed));
    }

    [Fact]
    public void DeferredCancellationCompletesAtWaitingForApplicationBoundary()
    {
        var coordinator = CreateCoordinator();
        using var operation = coordinator.Begin(FirmwareOperationKind.InstallApplicationFirmware);
        foreach (var state in InstallPath.TakeWhile(state => state != FirmwareOperationState.WaitingForApplication))
            operation.Transition(Progress(state));

        operation.RequestCancellation().Should().BeFalse();
        operation.Transition(Progress(FirmwareOperationState.WaitingForApplication));

        operation.RequestCancellation().Should().BeTrue();
        operation.State.Should().Be(FirmwareOperationState.Cancelled);
    }

    [Fact]
    public void TerminalStateCannotTransitionAgain()
    {
        var coordinator = CreateCoordinator();
        using var operation = coordinator.Begin(FirmwareOperationKind.InstallApplicationFirmware);
        operation.Transition(Progress(FirmwareOperationState.Failed));

        var act = () => operation.Transition(Progress(FirmwareOperationState.Completed));

        act.Should().Throw<FirmwareStateTransitionException>();
    }

    private static readonly FirmwareOperationState[] InstallPath =
    [
        FirmwareOperationState.Downloading,
        FirmwareOperationState.ValidatingPackage,
        FirmwareOperationState.WaitingForDevice,
        FirmwareOperationState.IdentifyingBootloader,
        FirmwareOperationState.CheckingCompatibility,
        FirmwareOperationState.Erasing,
        FirmwareOperationState.Programming,
        FirmwareOperationState.Verifying,
        FirmwareOperationState.Rebooting,
        FirmwareOperationState.WaitingForApplication,
        FirmwareOperationState.Completed
    ];

    private static FirmwareOperationCoordinator CreateCoordinator() => new(NullLogger<FirmwareOperationCoordinator>.Instance);

    private static FirmwareProgress Progress(FirmwareOperationState state) => new(state, null, $"state.{state}");
}
