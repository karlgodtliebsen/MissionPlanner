using CommunityToolkit.Maui.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Configuration;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Views.ConfigTuning;
using MissionPlanner.App.Views.ConfigTuning.Tabs;
using MissionPlanner.Core.ConfigTuning.Planner;
using MissionPlanner.Core.Vehicles.Abstractions;
using MissionPlanner.Core.Vehicles.Models;
using NSubstitute;

namespace MissionPlanner.Core.Tests;

/// <summary>Verifies versioned local Planner application preferences.</summary>
public sealed class PlannerSettingsTests
{
    /// <summary>Verifies first use returns validated safe defaults.</summary>
    [Fact]
    public async Task InitializeUsesSafeDefaultsWhenNoDocumentExists()
    {
        var service = CreateService(new MemoryStore());
        var cancellationToken = TestContext.Current.CancellationToken;

        var result = await service.InitializeAsync(cancellationToken);

        result.WasRecovered.Should().BeFalse();
        result.Settings.SchemaVersion.Should().Be(PlannerSettings.CurrentSchemaVersion);
        service.Validate(result.Settings).Should().BeEmpty();
        result.Settings.Connection.Port.Should().Be(14550);
        result.Settings.Confirmations.ConfirmFirmwareChanges.Should().BeTrue();
    }

    /// <summary>Verifies an older document is migrated and written using the current schema.</summary>
    [Fact]
    public async Task InitializeMigratesOlderSchema()
    {
        var store = new MemoryStore("""{"schemaVersion":1,"units":{"system":"Imperial"}}""");
        var service = CreateService(store);
        var cancellationToken = TestContext.Current.CancellationToken;

        var result = await service.InitializeAsync(cancellationToken);

        result.WasMigrated.Should().BeTrue();
        result.Settings.Units.System.Should().Be(UnitSystem.Imperial);
        result.Settings.Accessibility.TextScale.Should().Be(1);
        store.Document.Should().Contain($"\"schemaVersion\": {PlannerSettings.CurrentSchemaVersion}");
    }

    /// <summary>Verifies invalid ranges and connection values block persistence.</summary>
    [Fact]
    public async Task SaveRejectsInvalidSettings()
    {
        var store = new MemoryStore();
        var service = CreateService(store);
        var cancellationToken = TestContext.Current.CancellationToken;
        await service.InitializeAsync(cancellationToken);
        var invalid = service.Current with { Map = service.Current.Map with { DefaultZoom = 40, Style = PlannerMapStyle.Physical }, Connection = service.Current.Connection with { Port = 0 } };

        var result = await service.SaveAsync(invalid, cancellationToken);

        result.Success.Should().BeFalse();
        result.Errors.Should().Contain(error => error.Property == nameof(PlannerMapSettings.DefaultZoom));
        result.Errors.Should().Contain(error => error.Property == nameof(PlannerMapSettings.Style));
        result.Errors.Should().Contain(error => error.Property == nameof(PlannerConnectionSettings.Port));
        store.Document.Should().BeNull();
    }

    /// <summary>Verifies import/export is versioned and excludes unknown secret material.</summary>
    [Fact]
    public async Task ImportExportExcludesSecretsAndPreservesSettings()
    {
        var service = CreateService(new MemoryStore());
        var cancellationToken = TestContext.Current.CancellationToken;
        await service.InitializeAsync(cancellationToken);
        var source = service.Export();
        var import = source[..source.LastIndexOf('}')]
                     + ",\n  \"password\": \"do-not-export\",\n  \"accessToken\": \"secret-token\"\n}";

        var result = await service.ImportAsync(import, cancellationToken);
        var exported = service.Export();

        result.Success.Should().BeTrue();
        exported.Should().NotContain("do-not-export");
        exported.Should().NotContain("secret-token");
        exported.ToLowerInvariant().Should().NotContain("password");
        exported.ToLowerInvariant().Should().NotContain("accesstoken");
    }

    /// <summary>Verifies subscribers receive validated live option changes and restart metadata.</summary>
    [Fact]
    public async Task SavePublishesLiveSettingsChange()
    {
        var service = CreateService(new MemoryStore());
        var cancellationToken = TestContext.Current.CancellationToken;
        await service.InitializeAsync(cancellationToken);
        PlannerSettingsChangedEventArgs? changed = null;
        service.SettingsChanged += (_, args) => changed = args;
        var updated = service.Current with { Appearance = new PlannerAppearanceSettings { Theme = PlannerTheme.Dark }, Logging = new PlannerLoggingSettings { Level = PlannerLogLevel.Warning, RetentionDays = 14 } };

        var result = await service.SaveAsync(updated, cancellationToken);

        result.Success.Should().BeTrue();
        changed.Should().NotBeNull();
        changed!.Current.Appearance.Theme.Should().Be(PlannerTheme.Dark);
        result.RestartRequiredSections.Should().Contain(PlannerSettingsSection.Logging);
        result.RestartRequiredSections.Should().NotContain(PlannerSettingsSection.Appearance);
    }

    /// <summary>Verifies corrupt persisted JSON is replaced by safe defaults.</summary>
    [Fact]
    public async Task InitializeRecoversCorruptSettings()
    {
        var store = new MemoryStore("{ definitely-not-json");
        var service = CreateService(store);
        var cancellationToken = TestContext.Current.CancellationToken;

        var result = await service.InitializeAsync(cancellationToken);

        result.WasRecovered.Should().BeTrue();
        service.Validate(result.Settings).Should().BeEmpty();
        store.Document.Should().Contain($"\"schemaVersion\": {PlannerSettings.CurrentSchemaVersion}");
    }

    /// <summary>Verifies section and all-settings reset operations persist defaults.</summary>
    [Fact]
    public async Task ResetSectionAndAllRestoreDefaults()
    {
        var service = CreateService(new MemoryStore());
        var cancellationToken = TestContext.Current.CancellationToken;
        await service.InitializeAsync(cancellationToken);
        await service.SaveAsync(service.Current with { Units = new PlannerUnitSettings { System = UnitSystem.Aviation }, Map = service.Current.Map with { DefaultZoom = 8 } }, cancellationToken);

        await service.ResetSectionAsync(PlannerSettingsSection.Units, cancellationToken);
        service.Current.Units.Should().Be(new PlannerUnitSettings());
        service.Current.Map.DefaultZoom.Should().Be(8);

        await service.ResetAllAsync(cancellationToken);
        service.Current.Should().Be(new PlannerSettings());
    }

    /// <summary>Verifies the view model loads and saves local settings through the typed service.</summary>
    [Fact]
    public async Task ViewModelLoadsAndSavesTypedSettings()
    {
        var service = CreateService(new MemoryStore());
        var context = Substitute.For<IActiveVehicleContext>();
        context.Current.Returns(new ActiveVehicleSnapshot(null, null));
        var applicationState = new ApplicationStateService(context);
        using var runtime = new PlannerSettingsRuntime(service, applicationState);
        var fileSaver = Substitute.For<IFileSaver>();
        var viewModel = new PlannerTabViewModel(
            service,
            runtime,
            new ParametersFileHandler(fileSaver),
            Substitute.For<IUserConfirmationService>(),
            NullLogger<PlannerTabViewModel>.Instance);
        await viewModel.ActivateAsync();
        viewModel.ConnectionChannel = "UDP";
        viewModel.ConnectionHost = "192.168.1.20";
        viewModel.ConnectionPort = 14551;
        viewModel.SelectedUnitSystem = UnitSystem.Aviation;

        await viewModel.SaveCommand.ExecuteAsync(null);

        service.Current.Connection.Channel.Should().Be("UDP");
        service.Current.Connection.Host.Should().Be("192.168.1.20");
        service.Current.Connection.Port.Should().Be(14551);
        service.Current.Units.System.Should().Be(UnitSystem.Aviation);
        applicationState.SelectedPort.Should().Be("14551");
        viewModel.StatusMessage.Should().Contain("saved");
    }

    private static PlannerSettingsService CreateService(IPlannerSettingsStore store)
    {
        return new PlannerSettingsService(store, NullLogger<PlannerSettingsService>.Instance);
    }

    private sealed class MemoryStore(string? document = null) : IPlannerSettingsStore
    {
        public string? Document { get; private set; } = document;

        public ValueTask<string?> ReadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Document);
        }

        public ValueTask WriteAsync(string value, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Document = value;
            return ValueTask.CompletedTask;
        }

        public ValueTask ClearAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Document = null;
            return ValueTask.CompletedTask;
        }
    }
}
