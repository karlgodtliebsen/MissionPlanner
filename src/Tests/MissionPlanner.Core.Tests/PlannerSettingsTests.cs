using CommunityToolkit.Maui.Storage;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using MissionPlanner.App.Configuration;
using MissionPlanner.App.Helpers;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Services;
using MissionPlanner.App.Theming;
using MissionPlanner.App.Views.ConfigTuning;
using MissionPlanner.App.Views.ConfigTuning.Tabs;
using MissionPlanner.App.Views.Preferences;
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
        result.Settings.Map.SelectedSourceId.Should().Be("osm-standard");
        result.Settings.Map.HttpCacheLimitBytes.Should().Be(268_435_456);
        store.Document.Should().Contain($"\"schemaVersion\": {PlannerSettings.CurrentSchemaVersion}");
    }

    /// <summary>Verifies every legacy provider/style pair retains the equivalent stable source.</summary>
    [Theory]
    [InlineData("OpenStreetMap", "Standard", "osm-standard")]
    [InlineData("Esri", "Topographic", "esri-world-topo")]
    [InlineData("Esri", "Physical", "esri-world-physical")]
    [InlineData("Esri", "ShadedRelief", "esri-world-shaded-relief")]
    [InlineData("Esri", "DarkGray", "esri-world-dark-gray")]
    public async Task InitializeMigratesLegacyMapSelection(string provider, string style, string expected)
    {
        var document = $"{{\"schemaVersion\":3,\"map\":{{\"provider\":\"{provider}\",\"style\":\"{style}\"}}}}";
        var result = await CreateService(new MemoryStore(document)).InitializeAsync(TestContext.Current.CancellationToken);
        result.Settings.Map.SelectedSourceId.Should().Be(expected);
    }

    /// <summary>Verifies an explicit modern source is never overwritten during schema migration.</summary>
    [Fact]
    public async Task InitializePreservesModernMapSelection()
    {
        const string document = """{"schemaVersion":3,"map":{"selectedSourceId":"custom:user","provider":"Esri","style":"Physical"}}""";
        var result = await CreateService(new MemoryStore(document)).InitializeAsync(TestContext.Current.CancellationToken);
        result.Settings.Map.SelectedSourceId.Should().Be("custom:user");
    }

    /// <summary>Verifies schema-four theme names migrate to stable schema-five identifiers.</summary>
    [Theory]
    [InlineData("System", "system")]
    [InlineData("Light", "mission-light")]
    [InlineData("Dark", "mission-dark")]
    public async Task InitializeMigratesLegacyTheme(string legacyTheme, string expectedThemeId)
    {
        var store = new MemoryStore($"{{\"schemaVersion\":4,\"appearance\":{{\"theme\":\"{legacyTheme}\",\"preferDarkTheme\":true}}}}");

        var result = await CreateService(store).InitializeAsync(TestContext.Current.CancellationToken);

        result.WasMigrated.Should().BeTrue();
        result.Settings.SchemaVersion.Should().Be(5);
        result.Settings.Appearance.ThemeId.Should().Be(expectedThemeId);
        store.WriteCount.Should().Be(1);
        store.Document.Should().Contain($"\"themeId\": \"{expectedThemeId}\"");
        store.Document.Should().NotContain("preferDarkTheme");
    }

    /// <summary>Verifies malformed legacy appearance uses PreferDarkTheme only as fallback.</summary>
    [Theory]
    [InlineData("{}", "system")]
    [InlineData("{\"theme\":\"invalid\",\"preferDarkTheme\":true}", "mission-dark")]
    [InlineData("{\"theme\":42,\"preferDarkTheme\":false}", "system")]
    public async Task InitializeUsesDocumentedLegacyThemeFallback(string appearance, string expectedThemeId)
    {
        var document = $"{{\"schemaVersion\":4,\"appearance\":{appearance}}}";
        var result = await CreateService(new MemoryStore(document)).InitializeAsync(TestContext.Current.CancellationToken);

        result.Settings.Appearance.ThemeId.Should().Be(expectedThemeId);
    }

    /// <summary>Verifies arbitrary valid identifiers, including Mission Blue, survive save and reload.</summary>
    [Theory]
    [InlineData("mission-blue")]
    [InlineData("extension-night-vision")]
    public async Task ThemeIdentifierRoundTripsWithoutFiniteEnum(string themeId)
    {
        var store = new MemoryStore();
        var service = CreateService(store);
        await service.InitializeAsync(TestContext.Current.CancellationToken);
        await service.SaveTheme(service.Current, themeId, TestContext.Current.CancellationToken);

        var reloaded = await CreateService(store).InitializeAsync(TestContext.Current.CancellationToken);

        reloaded.Settings.Appearance.ThemeId.Should().Be(themeId);
        reloaded.WasMigrated.Should().BeFalse();
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
        var updated = service.Current with { Appearance = new PlannerAppearanceSettings { ThemeId = ThemeIds.MissionDark }, Logging = new PlannerLoggingSettings { Level = PlannerLogLevel.Warning, RetentionDays = 14 } };

        var result = await service.SaveAsync(updated, cancellationToken);

        result.Success.Should().BeTrue();
        changed.Should().NotBeNull();
        changed!.Current.Appearance.ThemeId.Should().Be(ThemeIds.MissionDark);
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
        var themeManager = Substitute.For<IThemeManager>();
        themeManager.AvailableThemes.Returns(
        [
            new ThemeOption(ThemeIds.System, "System"),
            new ThemeOption(ThemeIds.MissionLight, "Mission Light"),
            new ThemeOption(ThemeIds.MissionDark, "Mission Dark"),
            new ThemeOption(ThemeIds.MissionBlue, "Mission Blue")
        ]);
        using var runtime = new PlannerSettingsRuntime(
            service,
            applicationState,
            themeManager,
            NullLogger<PlannerSettingsRuntime>.Instance);
        var fileSaver = Substitute.For<IFileSaver>();
        var offlinePacks = Substitute.For<MissionPlanner.Maps.Offline.IOfflineMapPackRepository>();
        offlinePacks.ListAsync(Arg.Any<CancellationToken>()).Returns([]);
        var viewModel = new PreferencesViewModel(
            service,
            themeManager,
            new ParametersFileHandler(fileSaver),
            Substitute.For<IUserConfirmationService>(),
            NullLogger<PreferencesViewModel>.Instance,
            Substitute.For<MissionPlanner.Maps.Credentials.IMapSecretStore>(),
            offlinePacks,
            Substitute.For<MissionPlanner.Maps.Offline.IOfflineMapPackManager>(),
            Substitute.For<MissionPlanner.Maps.Offline.IOfflineMapPackValidator>(),
            new MissionPlanner.Maps.Http.MapHttpDiskCache(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")), 1_048_576));
        await viewModel.ActivateAsync();
        viewModel.ConnectionChannel = "UDP";
        viewModel.ConnectionHost = "192.168.1.20";
        viewModel.ConnectionPort = 14551;
        viewModel.SelectedUnitSystem = UnitSystem.Aviation;
        viewModel.DistanceUnit = "Feet";
        viewModel.SpeedUnit = "Knots";
        viewModel.TrackLength = 500;
        viewModel.ShowAirports = false;
        viewModel.DownloadParametersInBackground = false;
        viewModel.LogDirectory = "logs/custom";

        await viewModel.SaveCommand.ExecuteAsync(null);

        service.Current.Connection.Channel.Should().Be("UDP");
        service.Current.Connection.Host.Should().Be("192.168.1.20");
        service.Current.Connection.Port.Should().Be(14551);
        service.Current.Units.System.Should().Be(UnitSystem.Aviation);
        service.Current.Legacy.DistanceUnit.Should().Be("Feet");
        service.Current.Legacy.SpeedUnit.Should().Be("Knots");
        service.Current.Legacy.TrackLength.Should().Be(500);
        service.Current.Legacy.ShowAirports.Should().BeFalse();
        service.Current.Legacy.DownloadParametersInBackground.Should().BeFalse();
        service.Current.Logging.LogDirectory.Should().Be("logs/custom");
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

        public int WriteCount { get; private set; }

        public ValueTask<string?> ReadAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(Document);
        }

        public ValueTask WriteAsync(string value, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Document = value;
            WriteCount++;
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
