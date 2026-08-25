using System.Collections.ObjectModel;
using System.Globalization;
using FluentAssertions;
using MissionPlanner.App.Views.ConfigTuning;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.Configuration;
using MissionPlanner.Core.Vehicles;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using MissionPlanner.Firmware;
using MissionPlanner.Firmware.Model;
using MissionPlanner.Library.Configuration;
using MissionPlanner.MavLink.Parameters;
using MissionPlanner.Shared.Models.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies safe shared parameter editing and Config navigation behavior.</summary>
public sealed class ParameterEditSessionTests
{
    /// <summary>Verifies metadata validation, dirty tracking, and revert preserve original and live values.</summary>
    [Fact]
    public async Task SessionTracksAndValidatesPendingValues()
    {
        using var fixture = CreateFixture(
            [(Parameter("GAIN", 1), Metadata("GAIN", "0 10", "0.5"))]);
        var session = fixture.Factory.Create(fixture.VehicleId);
        await session.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);

        session.TrySetPending("GAIN", 1.3, out var validationError).Should().BeFalse();
        validationError.Should().Contain("increments of");
        session.IsDirty.Should().BeTrue();
        session.GetField("GAIN").Should().Match<ParameterEditField>(field =>
            field.OriginalValue == 1 && field.LiveValue == 1 && field.PendingValue == 1.3 && !field.IsValid);

        session.Revert("GAIN");

        session.IsDirty.Should().BeFalse();
        session.GetField("GAIN")!.PendingValue.Should().Be(1);
    }

    /// <summary>Verifies grouped writes are deduplicated, partially successful, and confirmed by registry readback.</summary>
    [Fact]
    public async Task ApplyReportsDuplicateAndPartialWriteResults()
    {
        using var fixture = CreateFixture(
        [
            (Parameter("FIRST", 1), Metadata("FIRST", rebootRequired: true)),
            (Parameter("SECOND", 2), Metadata("SECOND")),
            (Parameter("THIRD", 3), Metadata("THIRD"))
        ]);
        fixture.ParameterService.FailingWrites.Add("SECOND");
        var session = fixture.Factory.Create(fixture.VehicleId);
        await session.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);
        session.TrySetPending("FIRST", 10, out var _).Should().BeTrue();
        session.TrySetPending("SECOND", 20, out var _).Should().BeTrue();
        session.TrySetPending("THIRD", 30, out var _).Should().BeTrue();

        var report = await session.ApplyAsync(
            ["FIRST", "FIRST", "SECOND", "THIRD"],
            TestContext.Current.CancellationToken);

        report.Success.Should().BeFalse();
        report.RebootRequired.Should().BeTrue();
        report.Confirmed.Should().BeEquivalentTo(["FIRST", "THIRD"]);
        report.Failed.Should().Equal("SECOND");
        fixture.ParameterService.Writes.Should().Equal("FIRST", "SECOND", "THIRD");
        session.GetField("FIRST")!.WriteStatus.Should().Be(ParameterEditWriteStatus.Confirmed);
        session.GetField("SECOND")!.WriteStatus.Should().Be(ParameterEditWriteStatus.Failed);
        session.GetField("SECOND")!.IsModified.Should().BeTrue();
        session.GetField("THIRD")!.IsModified.Should().BeFalse();
        fixture.Factory.HasUnappliedChanges.Should().BeTrue();
    }

    /// <summary>A write plan snapshots only modified values and aggregates reboot requirements.</summary>
    [Fact]
    public async Task WritePlanCapturesStableModifiedSnapshot()
    {
        using var fixture = CreateFixture(
        [
            (Parameter("FIRST", 1), Metadata("FIRST", rebootRequired: true)),
            (Parameter("SECOND", 2), Metadata("SECOND"))
        ]);
        var session = fixture.Factory.Create(fixture.VehicleId);
        await session.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);
        session.TrySetPending("FIRST", 3, out var _).Should().BeTrue();

        var plan = session.CreateWritePlan();

        plan.Entries.Should().ContainSingle().Which.Should().Match<ParameterWritePlanEntry>(entry => entry.Name == "FIRST" && entry.LiveValue == 1 && entry.PendingValue == 3 && entry.RebootRequired);
    }

    /// <summary>A preview cannot execute after its pending values change.</summary>
    [Fact]
    public async Task StaleWritePlanSendsNoValues()
    {
        using var fixture = CreateFixture([(Parameter("GAIN", 1), Metadata("GAIN"))]);
        var session = fixture.Factory.Create(fixture.VehicleId);
        await session.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);
        session.TrySetPending("GAIN", 2, out var _).Should().BeTrue();
        var plan = session.CreateWritePlan();
        session.TrySetPending("GAIN", 3, out var _).Should().BeTrue();

        var act = () => session.ApplyAsync(plan, cancellationToken: TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*stale*");
        fixture.ParameterService.Writes.Should().BeEmpty();
    }

    /// <summary>Pre-cancellation returns a coherent skipped report and sends nothing.</summary>
    [Fact]
    public async Task CancelledWritePlanSendsNoValues()
    {
        using var fixture = CreateFixture([(Parameter("GAIN", 1), Metadata("GAIN"))]);
        var session = fixture.Factory.Create(fixture.VehicleId);
        await session.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);
        session.TrySetPending("GAIN", 2, out var _).Should().BeTrue();
        var plan = session.CreateWritePlan();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var report = await session.ApplyAsync(plan, cancellationToken: cancellation.Token);

        report.Success.Should().BeFalse();
        report.Results.Should().ContainSingle().Which.Outcome.Should().Be(ParameterWriteOutcome.Skipped);
        fixture.ParameterService.Writes.Should().BeEmpty();
    }

    /// <summary>Progress follows target order and retry excludes already confirmed values.</summary>
    [Fact]
    public async Task RetryWritesOnlyRetryableFailures()
    {
        using var fixture = CreateFixture(
        [
            (Parameter("FIRST", 1), Metadata("FIRST", rebootRequired: true)),
            (Parameter("SECOND", 2), Metadata("SECOND"))
        ]);
        fixture.ParameterService.FailingWrites.Add("SECOND");
        var session = fixture.Factory.Create(fixture.VehicleId);
        await session.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);
        session.TrySetPending("FIRST", 10, out var _).Should().BeTrue();
        session.TrySetPending("SECOND", 20, out var _).Should().BeTrue();
        var progress = new RecordingProgress();

        var initial = await session.ApplyAsync(session.CreateWritePlan(), progress, TestContext.Current.CancellationToken);
        fixture.ParameterService.FailingWrites.Clear();
        var retry = await session.RetryFailedAsync(initial, cancellationToken: TestContext.Current.CancellationToken);

        progress.Values.Where(value => value.Phase == ParameterApplyPhase.Writing)
            .Select(value => value.Name).Should().Equal("FIRST", "SECOND");
        fixture.ParameterService.Writes.Should().Equal("FIRST", "SECOND", "SECOND");
        retry.Success.Should().BeTrue();
        retry.RebootRequired.Should().BeTrue();
    }

    /// <summary>Verifies an unconfirmed write remains visible and retryable.</summary>
    [Fact]
    public async Task ApplyRetainsPendingValueWhenReadbackTimesOut()
    {
        using var fixture = CreateFixture([(Parameter("NO_ACK", 1), Metadata("NO_ACK"))], TimeSpan.FromMilliseconds(20));
        fixture.ParameterService.WritesWithoutReadback.Add("NO_ACK");
        var session = fixture.Factory.Create(fixture.VehicleId);
        await session.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);
        session.TrySetPending("NO_ACK", 4, out var _).Should().BeTrue();

        var report = await session.ApplyAsync(cancellationToken: TestContext.Current.CancellationToken);

        report.Success.Should().BeFalse();
        report.Results.Should().ContainSingle(result => result.Outcome == ParameterWriteOutcome.ReadbackFailed);
        session.GetField("NO_ACK")!.PendingValue.Should().Be(4);
        session.GetField("NO_ACK")!.LiveValue.Should().Be(1);
        session.IsDirty.Should().BeTrue();
    }

    /// <summary>Verifies disconnect invalidates pending edits and prevents stale writes.</summary>
    [Fact]
    public async Task DisconnectPreventsWritesFromStaleSession()
    {
        using var fixture = CreateFixture([(Parameter("GAIN", 1), Metadata("GAIN"))]);
        var session = fixture.Factory.Create(fixture.VehicleId);
        await session.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);
        session.TrySetPending("GAIN", 2, out var _).Should().BeTrue();

        fixture.ActiveVehicle.Set(fixture.ActiveVehicle.State! with
        {
            Connection = fixture.ActiveVehicle.State!.Connection with
            {
                State = VehicleConnectionState.Offline
            }
        });
        var report = await session.ApplyAsync(cancellationToken: TestContext.Current.CancellationToken);

        session.IsValid.Should().BeFalse();
        report.Success.Should().BeFalse();
        report.Results.Should().ContainSingle(result => result.Outcome == ParameterWriteOutcome.Skipped);
        fixture.ParameterService.Writes.Should().BeEmpty();
        session.GetField("GAIN")!.PendingValue.Should().Be(2);
    }

    /// <summary>Verifies switching vehicles invalidates pending edits and prevents writes to the new vehicle.</summary>
    [Fact]
    public async Task VehicleSwitchPreventsWritesFromStaleSession()
    {
        using var fixture = CreateFixture([(Parameter("GAIN", 1), Metadata("GAIN"))]);
        var session = fixture.Factory.Create(fixture.VehicleId);
        await session.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);
        session.TrySetPending("GAIN", 2, out var _).Should().BeTrue();

        fixture.ActiveVehicle.Set(fixture.ActiveVehicle.State! with
        {
            VehicleId = new VehicleId(2, 1)
        });
        var report = await session.ApplyAsync(cancellationToken: TestContext.Current.CancellationToken);

        session.IsValid.Should().BeFalse();
        report.Success.Should().BeFalse();
        report.Results.Should().ContainSingle(result => result.Outcome == ParameterWriteOutcome.Skipped);
        fixture.ParameterService.Writes.Should().BeEmpty();
        session.GetField("GAIN")!.PendingValue.Should().Be(2);
    }

    /// <summary>Verifies explicit aliases resolve only when their presence rule is satisfied.</summary>
    [Fact]
    public async Task AliasDefinitionsUsePresentParametersWithoutGuessing()
    {
        using var fixture = CreateFixture(
        [
            (Parameter("RATE_OLD", 1), Metadata("RATE_OLD")),
            (Parameter("FEATURE_ENABLE", 1), Metadata("FEATURE_ENABLE"))
        ]);
        var session = fixture.Factory.Create(fixture.VehicleId);
        var supported = new ParameterFieldDefinition(
            "Rate",
            ["RATE_NEW", "RATE_OLD"],
            new ParameterPresenceRule(["FEATURE_ENABLE"], []));
        var unsupported = new ParameterFieldDefinition(
            "Other",
            ["OTHER_NEW", "OTHER_OLD"],
            new ParameterPresenceRule(["MISSING_ENABLE"], []));

        await session.LoadDefinitionsAsync([supported, unsupported], TestContext.Current.CancellationToken);

        session.Fields.Should().ContainSingle().Which.Name.Should().Be("RATE_OLD");
    }

    /// <summary>Verifies refresh deduplicates explicit parameter names.</summary>
    [Fact]
    public async Task RefreshRequestsEachLoadedParameterOnce()
    {
        using var fixture = CreateFixture([(Parameter("GAIN", 1), Metadata("GAIN"))]);
        var session = fixture.Factory.Create(fixture.VehicleId);
        await session.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);

        await session.RefreshAsync(["GAIN", "GAIN", "MISSING"], TestContext.Current.CancellationToken);

        fixture.ParameterService.Requests.Should().Equal("GAIN");
    }

    /// <summary>Verifies the Full Parameters row writes through the shared session.</summary>
    [Fact]
    public async Task ParameterItemProjectsPendingSessionState()
    {
        using var fixture = CreateFixture([(Parameter("GAIN", 1), Metadata("GAIN", "0 5"))]);
        var session = fixture.Factory.Create(fixture.VehicleId);
        await session.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);
        var item = new ParameterItemViewModel(session, session.GetField("GAIN")!) { Value = 3 };

        item.IsModified.Should().BeTrue();
        session.GetField("GAIN")!.PendingValue.Should().Be(3);
    }

    /// <summary>Verifies UI text hides float32 expansion without changing the double-backed editing model.</summary>
    [Fact]
    public async Task ParameterItemFormatsFloat32ValuesForDisplay()
    {
        using var fixture = CreateFixture(
            [(Parameter("GAIN", 0.3f), Metadata("GAIN", "0 1", "0.1"))]);
        var session = fixture.Factory.Create(fixture.VehicleId);
        await session.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);
        var item = new ParameterItemViewModel(session, session.GetField("GAIN")!);
        var expected = 0.3f.ToString(CultureInfo.CurrentCulture);

        item.Value.Should().Be((double)0.3f);
        item.OriginalValue.Should().Be((double)0.3f);
        item.ValueText.Should().Be(expected);
        item.OriginalValueText.Should().Be(expected);

        item.ValueText = expected;

        item.Value.Should().Be((double)0.3f);
        session.GetField("GAIN")!.IsModified.Should().BeFalse();
    }

    /// <summary>Verifies editing display text still updates the double value and shared session.</summary>
    [Fact]
    public async Task ParameterItemValueTextUpdatesPendingValue()
    {
        using var fixture = CreateFixture(
            [(Parameter("GAIN", 0.3f), Metadata("GAIN", "0 1", "0.1"))]);
        var session = fixture.Factory.Create(fixture.VehicleId);
        await session.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);
        var item = new ParameterItemViewModel(session, session.GetField("GAIN")!);
        var editedText = 0.4f.ToString(CultureInfo.CurrentCulture);

        item.ValueText = editedText;

        item.Value.Should().Be(0.4);
        item.ValueText.Should().Be(editedText);
        session.GetField("GAIN")!.PendingValue.Should().Be(0.4);
    }

    /// <summary>Verifies increment and decrement remain effective during synchronous session projection.</summary>
    [Fact]
    public async Task NumericEditorPreservesValueDuringSessionSynchronization()
    {
        using var fixture = CreateFixture(
            [(Parameter("GAIN", 1), Metadata("GAIN", "0 5", "0.5"))]);
        var session = fixture.Factory.Create(fixture.VehicleId);
        await session.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);
        var item = new ParameterItemViewModel(session, session.GetField("GAIN")!);
        session.Changed += SynchronizeItem;

        item.IncrementNumberCommand.Execute(null);

        item.Value.Should().Be(1.5f);
        session.GetField("GAIN")!.PendingValue.Should().Be(1.5);

        item.DecrementNumberCommand.Execute(null);

        item.Value.Should().Be(1);
        session.GetField("GAIN")!.PendingValue.Should().Be(1);

        void SynchronizeItem()
        {
            item.SetField(session.GetField("GAIN")!);
        }
    }

    /// <summary>Firmware sentinel values outside metadata bounds are not coerced while the row is realized.</summary>
    [Fact]
    public async Task NumericEditorPreservesLoadedSentinelOutsideMetadataRange()
    {
        using var fixture = CreateFixture(
            [(Parameter("ATC_RAT_PIT_NEF", 0), Metadata("ATC_RAT_PIT_NEF", "1 20", "1"))]);
        var session = fixture.Factory.Create(fixture.VehicleId);
        await session.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);

        var item = new ParameterItemViewModel(session, session.GetField("ATC_RAT_PIT_NEF")!);

        item.Value.Should().Be(0);
        item.Min.Should().Be(0);
        item.Max.Should().Be(20);
        session.GetField("ATC_RAT_PIT_NEF")!.IsModified.Should().BeFalse();
    }

    /// <summary>Verifies repeated decimal increments do not accumulate binary floating-point drift.</summary>
    [Fact]
    public async Task NumericEditorUsesPrecisionSafeDecimalSteps()
    {
        using var fixture = CreateFixture(
            [(Parameter("GAIN", 0), Metadata("GAIN", "0 2", "0.1"))]);
        var session = fixture.Factory.Create(fixture.VehicleId);
        await session.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);
        var item = new ParameterItemViewModel(session, session.GetField("GAIN")!);
        session.Changed += SynchronizeItem;

        for (var index = 0; index < 7; index++)
        {
            item.IncrementNumberCommand.Execute(null);
        }

        item.StepSize.Should().Be(0.1);
        item.Value.Should().Be(0.7);
        Convert.ToDecimal(item.Value).Should().Be(0.7m);

        void SynchronizeItem()
        {
            item.SetField(session.GetField("GAIN")!);
        }
    }

    /// <summary>Verifies a large derived step clamps an overshooting change to the nearest range boundary.</summary>
    [Fact]
    public async Task NumericEditorClampsLargeStepsToRangeBoundaries()
    {
        using var fixture = CreateFixture(
            [(Parameter("RATE", 90), Metadata("RATE", "0 95"))]);
        var session = fixture.Factory.Create(fixture.VehicleId);
        await session.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);
        var item = new ParameterItemViewModel(session, session.GetField("RATE")!);

        item.StepSize.Should().Be(10);
        item.IncrementNumberCommand.Execute(null);

        item.Value.Should().Be(95);
        session.GetField("RATE")!.PendingValue.Should().Be(95);

        item.Value = 5;
        item.DecrementNumberCommand.Execute(null);

        item.Value.Should().Be(0);
        session.GetField("RATE")!.PendingValue.Should().Be(0);
    }

    /// <summary>Verifies range metadata supplies a useful editor step when firmware metadata omits an increment.</summary>
    [Fact]
    public async Task NumericEditorDerivesStepFromRangeAndPreservesMetadata()
    {
        var metadata = new ParameterMetadata(
            "ACRO_RP_RATE",
            "Acro Roll/Pitch Rate",
            "Maximum roll and pitch rate in Acro mode.",
            "deg/s",
            "degrees per second",
            "0 1080",
            null,
            null,
            null,
            "Advanced",
            true,
            false);
        using var fixture = CreateFixture([(Parameter("ACRO_RP_RATE", 360), metadata)]);
        var session = fixture.Factory.Create(fixture.VehicleId);
        await session.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);
        var item = new ParameterItemViewModel(session, session.GetField("ACRO_RP_RATE")!);

        item.DisplayName.Should().Be("Acro Roll/Pitch Rate");
        item.Description.Should().Be("Maximum roll and pitch rate in Acro mode.");
        item.Units.Should().Be("deg/s");
        item.UnitText.Should().Be("degrees per second");
        item.Range.Should().Be("0 1080");
        item.RangeData.Should().Equal("0", "1080");
        item.Increment.Should().BeNull();
        item.UserLevel.Should().Be("Advanced");
        item.HasNumericRangeData.Should().BeTrue();
        item.RebootRequired.Should().BeTrue();

        item.IncrementNumberCommand.Execute(null);

        item.Value.Should().Be(468);
        session.GetField("ACRO_RP_RATE")!.PendingValue.Should().Be(468);
    }

    /// <summary>Verifies enum and bitmask editors publish one stable value during synchronous session projection.</summary>
    [Fact]
    public async Task OptionEditorsPreserveSelectionsDuringSessionSynchronization()
    {
        var enumMetadata = new ParameterMetadata(
            "MODE", "Mode", "Mode", null, "mode selection", null,
            "0:Disabled,2:Automatic", null, null, "Standard", false, false);
        var bitmaskMetadata = new ParameterMetadata(
            "FLAGS", "Flags", "Flags", null, "feature flags", null,
            null, "0:First,2:Third", null, "Advanced", false, false);
        using var fixture = CreateFixture(
        [
            (Parameter("MODE", 0), enumMetadata),
            (Parameter("FLAGS", 0), bitmaskMetadata)
        ]);
        var session = fixture.Factory.Create(fixture.VehicleId);
        await session.LoadAsync(cancellationToken: TestContext.Current.CancellationToken);
        var mode = new ParameterItemViewModel(session, session.GetField("MODE")!);
        var flags = new ParameterItemViewModel(session, session.GetField("FLAGS")!);
        var originalModeOptions = mode.ValuesData;
        var originalFlagOptions = flags.BitmaskOptions;
        session.Changed += SynchronizeItems;

        mode.SelectedValue = "Automatic";
        var selectedFlags = new ObservableCollection<object> { flags.BitmaskOptions![0], flags.BitmaskOptions[1] };
        flags.SelectedValuesChanged.Execute(selectedFlags);

        mode.Value.Should().Be(2);
        session.GetField("MODE")!.PendingValue.Should().Be(2);
        flags.Value.Should().Be(5);
        session.GetField("FLAGS")!.PendingValue.Should().Be(5);
        mode.ValuesData.Should().BeSameAs(originalModeOptions);
        flags.BitmaskOptions.Should().BeSameAs(originalFlagOptions);
        mode.Values.Should().Be("0:Disabled,2:Automatic");
        mode.UnitText.Should().Be("mode selection");
        flags.Bitmask.Should().Be("0:First,2:Third");
        flags.UnitText.Should().Be("feature flags");
        flags.UserLevel.Should().Be("Advanced");
        flags.IsReadOnly.Should().BeTrue();

        void SynchronizeItems()
        {
            mode.SetField(session.GetField("MODE")!);
            flags.SetField(session.GetField("FLAGS")!);
        }
    }

    /// <summary>Verifies navigation within Config is silent and leaving requires explicit discard confirmation.</summary>
    [Fact]
    public async Task NavigationGuardProtectsUnappliedChanges()
    {
        var sessions = Substitute.For<IParameterEditSessionFactory>();
        sessions.HasUnappliedChanges.Returns(true);
        var confirmation = Substitute.For<App.Presentation.IUserConfirmationService>();
        confirmation.ConfirmAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false, true);
        var guard = new ConfigNavigationGuard(sessions, confirmation);

        (await guard.CanNavigateAsync(true, TestContext.Current.CancellationToken)).Should().BeTrue();
        (await guard.CanNavigateAsync(false, TestContext.Current.CancellationToken)).Should().BeFalse();
        (await guard.CanNavigateAsync(false, TestContext.Current.CancellationToken)).Should().BeTrue();

        await confirmation.Received(2).ConfirmAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        sessions.Received(1).DiscardPendingChanges();
    }

    private static Fixture CreateFixture(IReadOnlyList<(VehicleParameter Parameter, ParameterMetadata Metadata)> definitions, TimeSpan? readbackTimeout = null)
    {
        var state = State();
        var activeVehicle = new TestActiveVehicleContext(state);
        var registry = new VehicleParameterRegistry();
        foreach (var definition in definitions)
        {
            registry.StoreParameter(state.VehicleId, definition.Parameter, CancellationToken.None);
        }

        var metadata = definitions.ToDictionary(item => item.Metadata.Name, item => item.Metadata, StringComparer.Ordinal);
        var metadataService = Substitute.For<IVehicleParameterMetadataService>();
        metadataService.GetAllMetadataAsync(state.VehicleId, Arg.Any<CancellationToken>()).Returns(metadata);
        var parameterService = new TestParameterService(registry);
        var services = new ServiceCollection();
        services.AddLibraryServices();
        services.AddLogging();
        services.AddSingleton<IActiveVehicleContext>(activeVehicle);
        services.AddSingleton<IVehicleParameterRegistry>(registry);
        services.AddSingleton<IVehicleParameterService>(parameterService);
        services.AddSingleton(metadataService);
        services.Configure<ParameterEditSessionOptions>(options =>
            options.ReadbackTimeout = readbackTimeout ?? TimeSpan.FromSeconds(1));
        services.AddSingleton<ParameterEditSessionFactory>();

        var serviceProvider = services.BuildServiceProvider();
        serviceProvider.UseDomainServices();
        var factory = serviceProvider.GetRequiredService<ParameterEditSessionFactory>();
        return new Fixture(state.VehicleId, activeVehicle, parameterService, factory, serviceProvider);
    }

    private static VehicleParameter Parameter(string name, float value)
    {
        return new VehicleParameter(name, value, MavParamType.Real32, 0, 1);
    }

    private static ParameterMetadata Metadata(string name, string? range = null, string? increment = null, bool rebootRequired = false)
    {
        return new ParameterMetadata(name, name, $"Description for {name}", null, null, range, null, null, increment, "Standard", rebootRequired, false);
    }

    private static VehicleState State()
    {
        var state = new VehicleState(
            new VehicleId(1, 1),
            0,
            2,
            3,
            0,
            4,
            3,
            VehicleConnectionState.Online,
            DateTimeOffset.UtcNow,
            VehicleMode.Stabilize,
            false,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
        var firmware = new VehicleFirmwareIdentity(
            FirmwareFamily.ArduCopter,
            2,
            3,
            new FirmwareSemanticVersion(4, 6, 0, FirmwareReleaseType.Official),
            "abcdef01",
            0,
            1,
            2,
            3,
            4,
            "vehicle-1");
        return state with
        {
            Identity = state.Identity with
            {
                Firmware = firmware
            }
        };
    }

    private sealed record Fixture(
        VehicleId VehicleId,
        TestActiveVehicleContext ActiveVehicle,
        TestParameterService ParameterService,
        ParameterEditSessionFactory Factory,
        ServiceProvider ServiceProvider) : IDisposable
    {
        public void Dispose()
        {
            ServiceProvider.Dispose();
        }
    }

    private sealed class TestParameterService(IVehicleParameterRegistry registry) : IVehicleParameterService
    {
        public HashSet<string> FailingWrites { get; } = new(StringComparer.Ordinal);

        public HashSet<string> WritesWithoutReadback { get; } = new(StringComparer.Ordinal);

        public List<string> Writes { get; } = [];

        public List<string> Requests { get; } = [];

        public Task<bool> RequestParameterListAsync(VehicleId vehicleId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<bool> RequestParameterAsync(VehicleId vehicleId, string parameterName, CancellationToken cancellationToken = default)
        {
            Requests.Add(parameterName);
            return Task.FromResult(true);
        }

        public Task<bool> RequestParameterByIndexAsync(VehicleId vehicleId, ushort parameterIndex, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public Task<bool> SetParameterAsync(
            VehicleId vehicleId,
            string parameterName,
            float value,
            MavParamType paramType,
            CancellationToken cancellationToken = default)
        {
            Writes.Add(parameterName);
            if (FailingWrites.Contains(parameterName))
            {
                return Task.FromResult(false);
            }

            if (!WritesWithoutReadback.Contains(parameterName))
            {
                registry.StoreParameter(vehicleId, new VehicleParameter(parameterName, value, paramType, 0, 1), cancellationToken);
            }

            return Task.FromResult(true);
        }
    }

    private sealed class TestActiveVehicleContext(VehicleState state) : IActiveVehicleContext
    {
        private CancellationTokenSource lifetime = new();

        public ActiveVehicleSnapshot Current { get; private set; } = new(state.VehicleId, state);

        public VehicleId? VehicleId => Current.VehicleId;

        public VehicleState? State => Current.State;

        public bool IsOnline => Current.IsOnline;

        public CancellationToken ConnectionCancellationToken => lifetime.Token;

        public event Action<ActiveVehicleChangedEventArgs>? Changed;

        public void Set(VehicleState next)
        {
            var previous = Current;
            Current = new ActiveVehicleSnapshot(next.VehicleId, next);
            if (previous.VehicleId != Current.VehicleId || previous.IsOnline != Current.IsOnline)
            {
                lifetime.Cancel();
                lifetime.Dispose();
                lifetime = new CancellationTokenSource();
                if (!Current.IsOnline)
                {
                    lifetime.Cancel();
                }
            }

            Changed?.Invoke(new ActiveVehicleChangedEventArgs(previous, Current));
        }
    }

    private sealed class RecordingProgress : IProgress<ParameterApplyProgress>
    {
        public List<ParameterApplyProgress> Values { get; } = [];

        public void Report(ParameterApplyProgress value)
        {
            Values.Add(value);
        }
    }
}
