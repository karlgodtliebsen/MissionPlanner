using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Views.ConfigTuning.Tabs;
using MissionPlanner.Core.Commands;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Fences;
using MissionPlanner.Core.Missions.Abstractions;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Transfer;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Library.DateTime.Domain;
using MissionPlanner.Library.EventHub.Abstractions;
using MissionPlanner.MavLink.Encoding;
using MissionPlanner.MavLink.Generated;
using MissionPlanner.MavLink.Messages;
using MissionPlanner.MavLink.Missions;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.MavLink.Services;
using MissionPlanner.MavLink.Services.Abstractions;
using MissionPlanner.Transport;
using NSubstitute;
using DomainProtocolCapability = MissionPlanner.Core.Vehicles.Models.MavProtocolCapability;
using MissionItemType = MissionPlanner.MavLink.Missions.MavMissionType;
using ParameterWireType = MissionPlanner.MavLink.Parameters.MavParamType;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies dedicated fence validation, protocol mapping, synchronization, and recovery.</summary>
public sealed class FenceConfigurationTests
{
    /// <summary>Verifies open, undersized, self-intersecting, and invalid circle geometry is rejected.</summary>
    [Fact]
    public void GeometryValidatorRejectsUnsafeShapes()
    {
        var validator = new FenceGeometryValidator();
        var plan = new FencePlan(
            null,
            [
                FenceArea.Polygon(FenceAreaKind.PolygonInclusion, [Position(0, 0), Position(1, 1)], false),
                FenceArea.Polygon(
                    FenceAreaKind.PolygonExclusion,
                    [Position(0, 0), Position(1, 1), Position(0, 1), Position(1, 0)],
                    true),
                FenceArea.Circle(FenceAreaKind.CircleInclusion, Position(2, 2), 0)
            ]);

        var result = validator.Validate(plan);

        result.IsValid.Should().BeFalse();
        result.Issues.Select(issue => issue.Code).Should().Contain(
            "polygon-open",
            "polygon-vertices",
            "polygon-intersection",
            "circle-radius");
    }

    /// <summary>Verifies altitude ordering and fallback radius checks span related parameter fields.</summary>
    [Fact]
    public void ParameterValidatorChecksCrossFieldRelationships()
    {
        var session = Substitute.For<IParameterEditSession>();
        session.Fields.Returns([]);
        session.GetField("FENCE_ALT_MIN").Returns(Field("FENCE_ALT_MIN", 120));
        session.GetField("FENCE_ALT_MAX").Returns(Field("FENCE_ALT_MAX", 80));
        session.GetField("FENCE_RET_ALT").Returns(Field("FENCE_RET_ALT", 130));
        session.GetField("FENCE_RADIUS").Returns(Field("FENCE_RADIUS", -1));

        var result = new FenceGeometryValidator().ValidateParameters(session);

        result.Issues.Select(issue => issue.Code).Should().Contain("altitude-order", "return-altitude", "radius");
    }

    /// <summary>Verifies all fence geometry primitives round-trip without becoming mission waypoints.</summary>
    [Fact]
    public void ProtocolMapperRoundTripsFencePrimitives()
    {
        var mapper = new FenceProtocolMapper();
        var plan = ValidPlan();

        var items = mapper.ToProtocol(plan);
        var parsed = mapper.FromProtocol(items);

        parsed.Success.Should().BeTrue();
        parsed.Plan.ReturnPoint.Should().Be(plan.ReturnPoint);
        parsed.Plan.Areas.Select(area => area.Kind).Should().Equal(plan.Areas.Select(area => area.Kind));
        parsed.Plan.Areas[0].Vertices.Should().Equal(plan.Areas[0].Vertices);
        parsed.Plan.Areas[1].RadiusMeters.Should().BeApproximately(plan.Areas[1].RadiusMeters, 0.001);
        items.Should().OnlyContain(item => item.MissionType == MissionItemType.Fence);
        items.Select(item => (MavCmd)item.Command).Should().Contain(MavCmd.NavFencePolygonVertexInclusion);
        items.Select(item => (MavCmd)item.Command).Should().Contain(MavCmd.NavFenceCircleExclusion);
        items.Select(item => (MavCmd)item.Command).Should().Contain(MavCmd.NavFenceReturnPoint);
    }

    /// <summary>Verifies confirmed parameters precede geometry and synchronize local and vehicle revisions.</summary>
    [Fact]
    public async Task ApplyWritesParametersBeforeGeometryAndTracksRevision()
    {
        var fixture = CreateFixture();
        var session = Session(fixture.VehicleId, success: true);
        fixture.Service.SetLocalPlan(fixture.VehicleId, ValidPlan());

        var report = await fixture.Service.ApplyAsync(fixture.VehicleId, session, cancellationToken: TestContext.Current.CancellationToken);

        report.Success.Should().BeTrue();
        report.Snapshot.IsDirty.Should().BeFalse();
        report.Snapshot.VehicleRevision.Should().Be(1);
        fixture.Transfer.Uploaded.Should().NotBeEmpty();
        fixture.Transfer.Uploaded.Should().OnlyContain(item => item.MissionType == MissionItemType.Fence);
        await session.Received(1).ApplyAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>());
    }

    /// <summary>Verifies parameter failure leaves geometry local and prevents a partial plan replacement.</summary>
    [Fact]
    public async Task ParameterFailurePreventsGeometryUpload()
    {
        var fixture = CreateFixture();
        var session = Session(fixture.VehicleId, success: false);
        fixture.Service.SetLocalPlan(fixture.VehicleId, ValidPlan());

        var report = await fixture.Service.ApplyAsync(fixture.VehicleId, session, cancellationToken: TestContext.Current.CancellationToken);

        report.Success.Should().BeFalse();
        report.Snapshot.IsDirty.Should().BeTrue();
        fixture.Transfer.Uploaded.Should().BeEmpty();
    }

    /// <summary>Verifies replace and clear preserve a restorable local backup.</summary>
    [Fact]
    public async Task DownloadAndClearPreserveRecoverableBackup()
    {
        var fixture = CreateFixture();
        var original = ValidPlan();
        var vehicle = new FencePlan(null, [FenceArea.Circle(FenceAreaKind.CircleInclusion, Position(4, 4), 75)]);
        fixture.Service.SetLocalPlan(fixture.VehicleId, original);
        fixture.Transfer.DownloadResult = new MissionDownloadResult(true, fixture.Mapper.ToProtocol(vehicle), null);

        var download = await fixture.Service.DownloadAsync(
            fixture.VehicleId,
            true,
            cancellationToken: TestContext.Current.CancellationToken);
        var clear = await fixture.Service.ClearAsync(fixture.VehicleId, TestContext.Current.CancellationToken);
        var restored = fixture.Service.RestoreBackup(fixture.VehicleId);

        download.Success.Should().BeTrue();
        download.Snapshot.BackupPlan!.Areas.Should().HaveCount(original.Areas.Count);
        clear.Success.Should().BeTrue();
        clear.Snapshot.LocalPlan.Areas.Should().BeEmpty();
        restored.LocalPlan.Areas.Should().ContainSingle().Which.Kind.Should().Be(FenceAreaKind.CircleInclusion);
        restored.IsDirty.Should().BeTrue();
    }

    /// <summary>Verifies partial download and reconnect loss preserve local edits and suppress writes.</summary>
    [Fact]
    public async Task PartialDownloadAndDisconnectPreserveLocalEdits()
    {
        var fixture = CreateFixture();
        var local = ValidPlan();
        fixture.Service.SetLocalPlan(fixture.VehicleId, local);
        fixture.Transfer.DownloadResult = new MissionDownloadResult(false, fixture.Mapper.ToProtocol(local).Take(2).ToArray(), "Interrupted");

        var download = await fixture.Service.DownloadAsync(
            fixture.VehicleId,
            true,
            cancellationToken: TestContext.Current.CancellationToken);
        fixture.ActiveVehicle.Set(fixture.ActiveVehicle.State! with
        {
            Connection = fixture.ActiveVehicle.State!.Connection with { State = VehicleConnectionState.Offline }
        });
        var apply = await fixture.Service.ApplyAsync(
            fixture.VehicleId,
            Session(fixture.VehicleId, success: true),
            cancellationToken: TestContext.Current.CancellationToken);

        download.Success.Should().BeFalse();
        download.Snapshot.LocalPlan.Areas.Should().HaveCount(local.Areas.Count);
        download.Message.Should().Contain("partial items were ignored");
        apply.Success.Should().BeFalse();
        fixture.Transfer.Uploaded.Should().BeEmpty();

        fixture.ActiveVehicle.Set(State());
        var recovered = await fixture.Service.ApplyAsync(
            fixture.VehicleId,
            Session(fixture.VehicleId, success: true),
            cancellationToken: TestContext.Current.CancellationToken);

        recovered.Success.Should().BeTrue();
        recovered.Snapshot.LocalPlan.Areas.Should().HaveCount(local.Areas.Count);
        fixture.Transfer.Uploaded.Should().NotBeEmpty();
    }

    /// <summary>Verifies explicit map modes create closed polygons, circles, and return points in the fence plan.</summary>
    [Fact]
    public void MapViewModelEditsDedicatedFenceGeometry()
    {
        var vehicleId = new VehicleId(1, 1);
        var active = Substitute.For<IActiveVehicleContext>();
        active.VehicleId.Returns(vehicleId);
        var service = Substitute.For<IFenceConfigurationService>();
        var current = new FenceConfigurationSnapshot(vehicleId, FencePlan.Empty, null, null, 0, null, false);
        service.GetSnapshot(vehicleId).Returns(_ => current);
        service.SetLocalPlan(vehicleId, Arg.Any<FencePlan>()).Returns(call =>
        {
            current = current with
            {
                LocalPlan = call.ArgAt<FencePlan>(1),
                LocalRevision = current.LocalRevision + 1,
                IsDirty = true
            };
            return current;
        });
        var viewModel = new GeoFenceTabViewModel(
            active,
            service,
            Substitute.For<IUserConfirmationService>(),
            NullLogger<GeoFenceTabViewModel>.Instance);

        viewModel.BeginPolygonInclusionCommand.Execute(null);
        viewModel.HandleMapClick(0, 0);
        viewModel.HandleMapClick(0, 1);
        viewModel.HandleMapClick(1, 1);
        viewModel.FinishPolygonCommand.Execute(null);
        viewModel.CircleRadiusMeters = 75;
        viewModel.BeginCircleExclusionCommand.Execute(null);
        viewModel.HandleMapClick(2, 2);
        viewModel.BeginReturnPointCommand.Execute(null);
        viewModel.HandleMapClick(0.5, 0.5);

        viewModel.LocalPlan.Areas.Should().HaveCount(2);
        viewModel.LocalPlan.Areas[0].Should().Match<FenceArea>(area =>
            area.Kind == FenceAreaKind.PolygonInclusion && area.IsClosed && area.Vertices.Count == 3);
        viewModel.LocalPlan.Areas[1].Should().Match<FenceArea>(area =>
            area.Kind == FenceAreaKind.CircleExclusion && area.RadiusMeters == 75);
        viewModel.LocalPlan.ReturnPoint.Should().Be(Position(0.5, 0.5));
        viewModel.IsGeometryDirty.Should().BeTrue();
    }

    /// <summary>Verifies the real mission handshake carries typed fence upload, download, and acknowledged clear.</summary>
    [Fact]
    public async Task MissionTransferRoundTripsFenceItemsWithFakeVehicle()
    {
        var vehicleId = new VehicleId(1, 1);
        var endpoint = new TransportEndPoint("test");
        var registry = Substitute.For<IVehicleRegistry>();
        registry.GetRequired(vehicleId).Returns(new VehicleSession(State(), endpoint, Substitute.For<IDateTimeProvider>()));
        var connection = Substitute.For<IMavLinkConnection>();
        connection.SendRawAsync(Arg.Any<ReadOnlyMemory<byte>>(), endpoint, Arg.Any<CancellationToken>()).Returns(ValueTask.CompletedTask);
        var encoder = Substitute.For<IMavLinkMissionEncoder>();
        var eventHub = Substitute.For<IEventHub>();
        Func<MavLinkMessage, CancellationToken, Task>? receiver = null;
        eventHub.SubscribeAsync<MavLinkMessage>(
                MavLinkEventTopics.ReceivedMessage,
                Arg.Do<Func<MavLinkMessage, CancellationToken, Task>>(callback => receiver = callback))
            .Returns(Substitute.For<IDisposable>());
        var service = new MissionTransferService(
            registry,
            connection,
            encoder,
            Substitute.For<IMissionProtocolMapper>(),
            eventHub);
        var item = new FenceProtocolMapper().ToProtocol(
            new FencePlan(null, [FenceArea.Circle(FenceAreaKind.CircleInclusion, Position(1, 2), 60)])).Single();

        encoder.EncodeMissionCount(vehicleId.SystemId, vehicleId.ComponentId, 1, MissionItemType.Fence).Returns(_ =>
        {
            Publish(new MissionRequestIntMessage(1, 1, endpoint, 0, 255, 0, (byte)MissionItemType.Fence, DateTimeOffset.UtcNow));
            return [1];
        });
        encoder.EncodeMissionItemInt(vehicleId.SystemId, vehicleId.ComponentId, item).Returns(_ =>
        {
            Publish(Acknowledgement());
            return [2];
        });

        var upload = await service.UploadItemsAsync(
            vehicleId,
            [item],
            MissionPlanType.Geofence,
            cancellationToken: TestContext.Current.CancellationToken);

        encoder.EncodeMissionRequestList(vehicleId.SystemId, vehicleId.ComponentId, MissionItemType.Fence).Returns(_ =>
        {
            Publish(new MissionCountMessage(1, 1, endpoint, 1, 255, 0, (byte)MissionItemType.Fence, DateTimeOffset.UtcNow));
            return [3];
        });
        encoder.EncodeMissionRequestInt(vehicleId.SystemId, vehicleId.ComponentId, 0, MissionItemType.Fence).Returns(_ =>
        {
            Publish(new MissionItemIntMessage(
                1, 1, endpoint,
                item.Param1, item.Param2, item.Param3, item.Param4,
                item.X, item.Y, item.Z, item.Sequence, item.Command,
                255, 0, item.Frame, 0, 1, (byte)MissionItemType.Fence, DateTimeOffset.UtcNow));
            return [4];
        });

        var download = await service.DownloadAsync(
            vehicleId,
            MissionPlanType.Geofence,
            cancellationToken: TestContext.Current.CancellationToken);

        encoder.EncodeMissionClearAll(vehicleId.SystemId, vehicleId.ComponentId, MissionItemType.Fence).Returns(_ =>
        {
            Publish(Acknowledgement());
            return [5];
        });
        var clear = await service.ClearAsync(vehicleId, MissionPlanType.Geofence, TestContext.Current.CancellationToken);

        upload.Success.Should().BeTrue();
        download.Success.Should().BeTrue();
        download.Items.Should().ContainSingle().Which.Should().Be(item);
        clear.Success.Should().BeTrue();

        void Publish(MavLinkMessage message) => receiver!(message, CancellationToken.None).GetAwaiter().GetResult();
        MissionAckMessage Acknowledgement() => new(
            1, 1, endpoint, 255, 0, 0, (byte)MissionItemType.Fence, DateTimeOffset.UtcNow);
    }

    private static ParameterEditField Field(string name, double value) => new(
        name,
        ParameterWireType.Real32,
        value,
        value,
        value,
        ParameterFieldMetadata.Empty,
        null);

    private static IParameterEditSession Session(VehicleId vehicleId, bool success)
    {
        var session = Substitute.For<IParameterEditSession>();
        session.VehicleId.Returns(vehicleId);
        session.IsValid.Returns(true);
        session.Fields.Returns([]);
        session.ApplyAsync(Arg.Any<IReadOnlyList<string>>(), Arg.Any<CancellationToken>()).Returns(
            new ParameterApplyReport(
                success,
                success ? [] : [new ParameterWriteResult("FENCE_ENABLE", ParameterWriteOutcome.WriteFailed, "Rejected")],
                false));
        return session;
    }

    private static FencePlan ValidPlan() => new(
        Position(0.5, 0.5),
        [
            FenceArea.Polygon(
                FenceAreaKind.PolygonInclusion,
                [Position(0, 0), Position(0, 1), Position(1, 1), Position(1, 0)],
                true),
            FenceArea.Circle(FenceAreaKind.CircleExclusion, Position(2, 2), 50)
        ]);

    private static GeoPosition Position(double latitude, double longitude) => new(latitude, longitude);

    private static Fixture CreateFixture()
    {
        var state = State();
        var active = new TestActiveVehicleContext(state);
        var registry = new VehicleParameterRegistry();
        var transfer = new FakeMissionTransferService();
        var mapper = new FenceProtocolMapper();
        var service = new FenceConfigurationService(
            active,
            registry,
            Substitute.For<IParameterEditSessionFactory>(),
            transfer,
            mapper,
            new FenceGeometryValidator(),
            new VehicleOperationGate(),
            NullLogger<FenceConfigurationService>.Instance);
        return new Fixture(state.VehicleId, active, transfer, mapper, service);
    }

    private static VehicleState State()
    {
        var state = new VehicleState(
            new VehicleId(1, 1), 0, 2, 3, 0, 4, 3,
            VehicleConnectionState.Online, DateTimeOffset.UtcNow, VehicleMode.Stabilize, false,
            null, null, null, null, null, null, null, null);
        return state with
        {
            Identity = state.Identity with
            {
                Firmware = state.Identity.Firmware with
                {
                    Family = FirmwareFamily.ArduCopter,
                    Capabilities = (ulong)DomainProtocolCapability.MissionFence
                }
            }
        };
    }

    private sealed record Fixture(
        VehicleId VehicleId,
        TestActiveVehicleContext ActiveVehicle,
        FakeMissionTransferService Transfer,
        FenceProtocolMapper Mapper,
        FenceConfigurationService Service);

    private sealed class FakeMissionTransferService : IMissionTransferService
    {
        public MissionDownloadResult DownloadResult { get; set; } = new(true, [], null);

        public MissionUploadResult UploadResult { get; set; } = new(true, 0, null);

        public MissionUploadResult ClearResult { get; set; } = new(true, 0, null);

        public IReadOnlyList<MavLinkMissionItem> Uploaded { get; private set; } = [];

        public Task<MissionUploadResult> UploadAsync(
            VehicleId vehicleId,
            Mission mission,
            IProgress<MissionUploadProgress>? progress = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<MissionUploadResult> UploadItemsAsync(
            VehicleId vehicleId,
            IReadOnlyList<MavLinkMissionItem> items,
            MissionPlanType missionType,
            IProgress<MissionUploadProgress>? progress = null,
            CancellationToken cancellationToken = default)
        {
            Uploaded = items.ToArray();
            progress?.Report(new MissionUploadProgress(items.Count, items.Count, items.Count == 0 ? null : (ushort)(items.Count - 1)));
            return Task.FromResult(UploadResult);
        }

        public Task<MissionDownloadResult> DownloadAsync(
            VehicleId vehicleId,
            MissionPlanType missionType = MissionPlanType.FlightMission,
            CancellationToken cancellationToken = default) => Task.FromResult(DownloadResult);

        public Task<MissionDownloadResult> DownloadAsync(
            VehicleId vehicleId,
            MissionPlanType missionType,
            IProgress<MissionDownloadProgress>? progress,
            CancellationToken cancellationToken = default)
        {
            progress?.Report(new MissionDownloadProgress(DownloadResult.Items.Count, DownloadResult.Items.Count, null));
            return Task.FromResult(DownloadResult);
        }

        public Task<MissionUploadResult> ClearAsync(
            VehicleId vehicleId,
            MissionPlanType missionType = MissionPlanType.FlightMission,
            CancellationToken cancellationToken = default) => Task.FromResult(ClearResult);
    }

    private sealed class TestActiveVehicleContext(VehicleState state) : IActiveVehicleContext
    {
        private CancellationTokenSource lifetime = new();

        public ActiveVehicleSnapshot Current { get; private set; } = new(state.VehicleId, state);

        public VehicleId? VehicleId => Current.VehicleId;

        public VehicleState? State => Current.State;

        public bool IsOnline => Current.IsOnline;

        public CancellationToken ConnectionCancellationToken => lifetime.Token;

        public event EventHandler<ActiveVehicleChangedEventArgs>? Changed;

        public void Set(VehicleState next)
        {
            var previous = Current;
            Current = new ActiveVehicleSnapshot(next.VehicleId, next);
            lifetime.Cancel();
            lifetime.Dispose();
            lifetime = new CancellationTokenSource();
            if (!Current.IsOnline)
            {
                lifetime.Cancel();
            }

            Changed?.Invoke(this, new ActiveVehicleChangedEventArgs(previous, Current));
        }
    }
}
