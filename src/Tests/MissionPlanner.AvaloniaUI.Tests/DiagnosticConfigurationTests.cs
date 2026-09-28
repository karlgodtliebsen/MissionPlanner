using MissionPlanner.App.Models;
using MissionPlanner.App.Views.FlightData.Hud;
using MissionPlanner.App.Views.InitSetup.OptionalHardware.Sections;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.Setup.MandatoryHardware;
using MissionPlanner.Core.Setup.OptionalHardware.Motor;
using MissionPlanner.MavLink.Parameters;
using NSubstitute;
using System.Reactive.Subjects;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Utilities.Dispatching;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.Shared.Models.Vehicles.Models;

namespace MissionPlanner.AvaloniaUI.Tests;

public sealed class DiagnosticConfigurationTests
{
    [Fact]
    public void HudResetPreservesCombinedRollPitchAndCompassAndExpiresWithSession()
    {
        var now = DateTimeOffset.UtcNow;
        var id = new VehicleId(1, 1);
        var active = Substitute.For<IActiveVehicleContext>();
        active.VehicleId.Returns(id);
        active.IsOnline.Returns(true);
        var state = new VehicleState(id, 0, 2, 3, 0, 4, 3, VehicleConnectionState.Online, now,
            VehicleMode.Stabilize, false, null, null, null, null, null, null, null, null);
        state = state with { Motion = state.Motion with { YawRadians = Math.PI / 2, AttitudeObservedAt = now } };
        active.State.Returns(state);
        using var stream = new Subject<VehicleHudData>();
        var data = Substitute.For<IVehicleHudDataService>();
        data.ObservePrimaryVehicleHudData().Returns(stream);
        var dispatcher = Substitute.For<IUiDispatcher>();
        dispatcher.When(d => d.Dispatch(Arg.Any<Action>())).Do(call => call.Arg<Action>()!());
        using var model = new HudViewModel(data, dispatcher, active, TimeProvider.System,
            NullLogger<HudViewModel>.Instance, Substitute.For<IDomainEventHub>());
        stream.OnNext(VehicleHudData.CreateDefault(id) with { Roll = 25, Pitch = -17, Heading = 90, Yaw = 90 });
        model.RefreshHeadingReference();
        Assert.True(model.CanResetModelHeading);
        model.ResetModelHeadingCommand.Execute(null);
        Assert.Equal(0, model.ModelHeading, 6);
        Assert.Equal(25, model.Roll);
        Assert.Equal(-17, model.Pitch);
        Assert.Equal(90, model.Heading);
        Assert.Contains("offset active", model.HeadingReferenceStatus);
        model.RestoreModelHeadingCommand.Execute(null);
        Assert.Equal(90, model.ModelHeading, 6);
        model.ResetModelHeadingCommand.Execute(null);
        using var reconnected = new CancellationTokenSource();
        active.ConnectionCancellationToken.Returns(reconnected.Token);
        model.RefreshHeadingReference();
        Assert.Equal(90, model.ModelHeading, 6);
        model.ResetModelHeadingCommand.Execute(null);
        active.VehicleId.Returns(new VehicleId(2, 1));
        model.RefreshHeadingReference();
        Assert.Equal(90, model.ModelHeading, 6);
        Assert.Contains("Unadjusted", model.HeadingReferenceStatus);
        active.State.Returns(state with { Motion = state.Motion with { AttitudeObservedAt = now.AddSeconds(-5) } });
        model.RefreshHeadingReference();
        Assert.False(model.CanResetModelHeading);
        Assert.False(model.ResetModelHeadingCommand.CanExecute(null));
    }
    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(179)]
    [InlineData(-179)]
    [InlineData(359)]
    public void HeadingReferenceResetsAndRestoresWithoutChangingAttitude(double yaw)
    {
        var reference = new HudHeadingReference();
        reference.Reset(yaw, false);
        Assert.Null(reference.Offset);
        reference.Reset(yaw, true);
        Assert.Equal(0, reference.Project(yaw), 8);
        Assert.Equal(2, reference.Project(yaw + 2 - 360), 8);
        reference.Restore();
        Assert.Null(reference.Offset);
        Assert.Equal((yaw + 360) % 360, reference.Project(yaw), 8);
    }

    [Fact]
    public void ParameterEditorAcroAndUnknownValuesAreNumericAndBaselineIsNotDefault()
    {
        var session = Substitute.For<IParameterEditSession>();
        var metadata = ParameterFieldMetadata.Empty with
        {
            Options = [new ParameterValueOption(0, "Stabilize"), new ParameterValueOption(1, "Acro")]
        };
        var field = new ParameterEditField("FLTMODE6", MavParamType.Int32, 1, 1, 1, metadata, null);
        var item = new ParameterItemViewModel(session, field);
        Assert.Equal("Acro", item.SelectedValue);
        Assert.Equal(1, item.Value);
        Assert.False(item.IsModified);
        Assert.Empty(session.ReceivedCalls());
        item.SetField(field with { OriginalValue = 123, LiveValue = 123, PendingValue = 123 });
        Assert.Contains("123", item.SelectedValue!);
        Assert.Equal(123, item.Value);
        Assert.Empty(session.ReceivedCalls());
        item.SelectedValue = "Acro";
        Assert.Equal(1, item.Value);
        Assert.Contains(session.ReceivedCalls(), call => call.GetMethodInfo().Name == "TrySetPending" &&
            (string)call.GetArguments()[0]! == "FLTMODE6" && (double)call.GetArguments()[1]! == 1);

        var trim = new ParameterEditField("AHRS_TRIM_X", MavParamType.Real32, 0.12, 0.12, 0.12, ParameterFieldMetadata.Empty, null);
        var calibration = new ParameterItemViewModel(session, trim);
        calibration.SetField(trim with { LiveValue = 0.02, PendingValue = 0.02 });
        Assert.Equal(0.12, calibration.OriginalValue);
        Assert.False(calibration.IsModified);
        Assert.Contains("Metadata default: Unknown", calibration.ValueProvenance);
        Assert.Contains("Baseline is not a reset-to-default", calibration.ValueProvenance);
        calibration.SetField(trim with { Metadata = ParameterFieldMetadata.Empty with { DefaultValue = 0 } });
        Assert.Contains("Metadata default: 0", calibration.ValueProvenance);
        var otherVehicle = new ParameterItemViewModel(session, trim with { OriginalValue = 0.5, LiveValue = 0.5, PendingValue = 0.5 });
        Assert.Equal(0.5, otherVehicle.OriginalValue);
        Assert.Equal(0.12, calibration.OriginalValue);
    }

    [Fact]
    public void MotorDiagramKeepsLogicalSequenceAndPhysicalAssignmentsSeparate()
    {
        var point = new MotorDiagramPoint(new MotorLayoutMotor(2, 1, MotorRotation.CounterClockwise, -.5, .5),
            new MotorOutputResolution(2, MotorOutputResolutionStatus.Resolved, [7]));
        Assert.True(point.X > 0 && point.Y < 0);
        Assert.Contains("A → Motor 2", point.Label);
        Assert.Contains("7", point.OutputLabel);
        var duplicate = point with { Output = new MotorOutputResolution(2, MotorOutputResolutionStatus.Ambiguous, [1, 7]) };
        Assert.Contains("Ambiguous", duplicate.OutputLabel);
        Assert.Contains("unknown", (point with { Output = null }).OutputLabel);
    }
}
