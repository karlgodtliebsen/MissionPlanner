using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace MissionPlanner.Core.Configuration.Planner;

/// <summary>Default implementation of the versioned local Planner settings service.</summary>
public sealed class PlannerSettingsService : IPlannerSettingsService
{
    private static readonly JsonSerializerOptions jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
    };

    private static readonly HashSet<string> allowedConnectionChannels =
        new(StringComparer.OrdinalIgnoreCase) { "AUTO", "TCP", "UDP", "UDPCI", "WS" };

    private static readonly HashSet<string> allowedUpdateChannels =
        new(StringComparer.OrdinalIgnoreCase) { "Stable", "Beta", "Development" };

    private readonly IPlannerSettingsStore store;
    private readonly ILogger<PlannerSettingsService> logger;
    private readonly SemaphoreSlim initializationGate = new(1, 1);
    private bool initialized;

    /// <summary>Initializes the settings service.</summary>
    /// <param name="store">The platform settings store.</param>
    /// <param name="logger">The logger.</param>
    public PlannerSettingsService(IPlannerSettingsStore store, ILogger<PlannerSettingsService> logger)
    {
        this.store = store;
        this.logger = logger;
    }

    /// <inheritdoc />
    public PlannerSettings Current { get; private set; } = new();

    /// <inheritdoc />
    public event EventHandler<PlannerSettingsChangedEventArgs>? SettingsChanged;

    /// <inheritdoc />
    public async ValueTask<PlannerSettingsLoadResult> InitializeAsync(CancellationToken cancellationToken = default)
    {
        await initializationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (initialized)
            {
                return new PlannerSettingsLoadResult(Current, false, false, null);
            }

            var document = await store.ReadAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(document))
            {
                Current = new PlannerSettings();
                initialized = true;
                return new PlannerSettingsLoadResult(Current, false, false, null);
            }

            try
            {
                var (settings, migrated) = ParseAndMigrate(document);
                var errors = Validate(settings);
                if (errors.Count != 0)
                {
                    throw new InvalidDataException(string.Join(" ", errors.Select(error => error.Message)));
                }

                Current = settings;
                initialized = true;
                if (migrated)
                {
                    await PersistAsync(Current, cancellationToken).ConfigureAwait(false);
                    logger.LogInformation(
                        "Migrated Planner settings to schema {SchemaVersion}.",
                        PlannerSettings.CurrentSchemaVersion);
                }

                return new PlannerSettingsLoadResult(
                    Current,
                    migrated,
                    false,
                    migrated ? $"Settings migrated to schema {PlannerSettings.CurrentSchemaVersion}." : null);
            }
            catch (Exception exception) when (exception is JsonException or InvalidDataException or NotSupportedException)
            {
                Current = new PlannerSettings();
                initialized = true;
                await PersistAsync(Current, cancellationToken).ConfigureAwait(false);
                logger.LogWarning(exception, "Recovered corrupt or unsupported Planner settings using defaults.");
                return new PlannerSettingsLoadResult(
                    Current,
                    false,
                    true,
                    "Stored settings were invalid and have been replaced with safe defaults.");
            }
        }
        finally
        {
            initializationGate.Release();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<PlannerSettingsValidationError> Validate(PlannerSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var errors = new List<PlannerSettingsValidationError>();

        ValidateEnum(settings.Units.System, PlannerSettingsSection.Units, nameof(settings.Units.System), errors);
        ValidateEnum(settings.Map.Provider, PlannerSettingsSection.Map, nameof(settings.Map.Provider), errors);
        ValidateEnum(settings.Map.Style, PlannerSettingsSection.Map, nameof(settings.Map.Style), errors);
        if ((settings.Map.Provider == PlannerMapProvider.OpenStreetMap) !=
            (settings.Map.Style == PlannerMapStyle.Standard))
        {
            errors.Add(new PlannerSettingsValidationError(
                PlannerSettingsSection.Map,
                nameof(settings.Map.Style),
                "OpenStreetMap supports Standard style; Esri supports Topographic, Physical, ShadedRelief, or DarkGray."));
        }

        ValidateRange(settings.Map.DefaultZoom, 1, 22, PlannerSettingsSection.Map, nameof(settings.Map.DefaultZoom), errors);
        ValidateRange(settings.Telemetry.DisplayRateHz, 1, 30, PlannerSettingsSection.Telemetry, nameof(settings.Telemetry.DisplayRateHz), errors);
        ValidateRange(settings.Telemetry.ChartHistorySeconds, 10, 3600, PlannerSettingsSection.Telemetry, nameof(settings.Telemetry.ChartHistorySeconds), errors);
        ValidateEnum(settings.Appearance.Theme, PlannerSettingsSection.Appearance, nameof(settings.Appearance.Theme), errors);
        ValidateEnum(settings.Logging.Level, PlannerSettingsSection.Logging, nameof(settings.Logging.Level), errors);
        ValidateRange(settings.Logging.RetentionDays, 1, 90, PlannerSettingsSection.Logging, nameof(settings.Logging.RetentionDays), errors);

        if (!allowedConnectionChannels.Contains(settings.Connection.Channel))
        {
            errors.Add(new PlannerSettingsValidationError(
                PlannerSettingsSection.Connection,
                nameof(settings.Connection.Channel),
                "Connection channel must be AUTO, TCP, UDP, UDPCI, or WS."));
        }

        if (string.IsNullOrWhiteSpace(settings.Connection.Host) || settings.Connection.Host.Length > 255)
        {
            errors.Add(new PlannerSettingsValidationError(
                PlannerSettingsSection.Connection,
                nameof(settings.Connection.Host),
                "Connection host must contain 1 to 255 characters."));
        }

        ValidateRange(settings.Connection.Port, 1, 65535, PlannerSettingsSection.Connection, nameof(settings.Connection.Port), errors);
        ValidateRange(settings.Connection.BaudRate, 1200, 4_000_000, PlannerSettingsSection.Connection, nameof(settings.Connection.BaudRate), errors);
        ValidateEnum(settings.ParameterCache.Policy, PlannerSettingsSection.ParameterCache, nameof(settings.ParameterCache.Policy), errors);
        ValidateRange(settings.ParameterCache.MaximumAgeMinutes, 1, 1440, PlannerSettingsSection.ParameterCache, nameof(settings.ParameterCache.MaximumAgeMinutes), errors);
        ValidateRange(settings.Updates.CheckIntervalDays, 1, 30, PlannerSettingsSection.Updates, nameof(settings.Updates.CheckIntervalDays), errors);

        if (!allowedUpdateChannels.Contains(settings.Updates.Channel))
        {
            errors.Add(new PlannerSettingsValidationError(
                PlannerSettingsSection.Updates,
                nameof(settings.Updates.Channel),
                "Update channel must be Stable, Beta, or Development."));
        }

        ValidateRange(settings.Accessibility.TextScale, 0.8, 2, PlannerSettingsSection.Accessibility, nameof(settings.Accessibility.TextScale), errors);
        return errors;
    }

    /// <inheritdoc />
    public async ValueTask<PlannerSettingsSaveResult> SaveAsync(
        PlannerSettings settings,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        var normalized = settings with
        {
            SchemaVersion = PlannerSettings.CurrentSchemaVersion,
            Connection = settings.Connection with
            {
                Channel = settings.Connection.Channel.Trim().ToUpperInvariant(),
                Host = settings.Connection.Host.Trim()
            },
            Updates = settings.Updates with { Channel = NormalizeUpdateChannel(settings.Updates.Channel) }
        };
        var errors = Validate(normalized);
        if (errors.Count != 0)
        {
            return new PlannerSettingsSaveResult(false, errors, []);
        }

        var restartSections = GetRestartRequiredSections(Current, normalized);
        var previous = Current;
        await PersistAsync(normalized, cancellationToken).ConfigureAwait(false);
        Current = normalized;
        SettingsChanged?.Invoke(this, new PlannerSettingsChangedEventArgs(previous, Current, restartSections));
        logger.LogInformation("Saved Planner application preferences.");
        return new PlannerSettingsSaveResult(true, [], restartSections);
    }

    /// <inheritdoc />
    public ValueTask<PlannerSettingsSaveResult> ResetSectionAsync(
        PlannerSettingsSection section,
        CancellationToken cancellationToken = default)
    {
        var defaults = new PlannerSettings();
        var reset = section switch
        {
            PlannerSettingsSection.Units => Current with { Units = defaults.Units },
            PlannerSettingsSection.Map => Current with { Map = defaults.Map },
            PlannerSettingsSection.Telemetry => Current with { Telemetry = defaults.Telemetry },
            PlannerSettingsSection.Appearance => Current with { Appearance = defaults.Appearance },
            PlannerSettingsSection.Logging => Current with { Logging = defaults.Logging },
            PlannerSettingsSection.Connection => Current with { Connection = defaults.Connection },
            PlannerSettingsSection.ParameterCache => Current with { ParameterCache = defaults.ParameterCache },
            PlannerSettingsSection.Confirmations => Current with { Confirmations = defaults.Confirmations },
            PlannerSettingsSection.Updates => Current with { Updates = defaults.Updates },
            PlannerSettingsSection.Accessibility => Current with { Accessibility = defaults.Accessibility },
            _ => throw new ArgumentOutOfRangeException(nameof(section), section, null)
        };
        return SaveAsync(reset, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<PlannerSettingsSaveResult> ResetAllAsync(CancellationToken cancellationToken = default) =>
        SaveAsync(new PlannerSettings(), cancellationToken);

    /// <inheritdoc />
    public string Export() => JsonSerializer.Serialize(Current, jsonOptions);

    /// <inheritdoc />
    public async ValueTask<PlannerSettingsImportResult> ImportAsync(
        string document,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(document))
        {
            return ImportFailure("The settings document is empty.");
        }

        try
        {
            var (settings, migrated) = ParseAndMigrate(document);
            var result = await SaveAsync(settings, cancellationToken).ConfigureAwait(false);
            return new PlannerSettingsImportResult(
                result.Success,
                migrated,
                result.Errors,
                result.RestartRequiredSections);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or NotSupportedException)
        {
            logger.LogWarning(exception, "Rejected an invalid Planner settings import.");
            return ImportFailure("The settings document is corrupt or uses an unsupported schema.");
        }
    }

    private static PlannerSettingsImportResult ImportFailure(string message) =>
        new(
            false,
            false,
            [new PlannerSettingsValidationError(PlannerSettingsSection.Appearance, "Document", message)],
            []);

    private static (PlannerSettings Settings, bool Migrated) ParseAndMigrate(string document)
    {
        var settings = JsonSerializer.Deserialize<PlannerSettings>(document, jsonOptions)
            ?? throw new JsonException("The settings document did not contain an object.");
        if (settings.SchemaVersion <= 0 || settings.SchemaVersion > PlannerSettings.CurrentSchemaVersion)
        {
            throw new NotSupportedException($"Settings schema {settings.SchemaVersion} is not supported.");
        }

        var migrated = settings.SchemaVersion < PlannerSettings.CurrentSchemaVersion;
        return (settings with { SchemaVersion = PlannerSettings.CurrentSchemaVersion }, migrated);
    }

    private async ValueTask PersistAsync(PlannerSettings settings, CancellationToken cancellationToken)
    {
        var document = JsonSerializer.Serialize(settings, jsonOptions);
        await store.WriteAsync(document, cancellationToken).ConfigureAwait(false);
    }

    private static IReadOnlyList<PlannerSettingsSection> GetRestartRequiredSections(
        PlannerSettings previous,
        PlannerSettings current)
    {
        var sections = new List<PlannerSettingsSection>();
        if (previous.Map.Provider != current.Map.Provider || previous.Map.Style != current.Map.Style)
        {
            sections.Add(PlannerSettingsSection.Map);
        }

        if (previous.Logging != current.Logging)
        {
            sections.Add(PlannerSettingsSection.Logging);
        }

        if (previous.Updates.Channel != current.Updates.Channel)
        {
            sections.Add(PlannerSettingsSection.Updates);
        }

        return sections;
    }

    private static string NormalizeUpdateChannel(string channel)
    {
        var trimmed = channel.Trim();
        return allowedUpdateChannels.FirstOrDefault(item => item.Equals(trimmed, StringComparison.OrdinalIgnoreCase))
               ?? trimmed;
    }

    private static void ValidateEnum<TEnum>(
        TEnum value,
        PlannerSettingsSection section,
        string property,
        ICollection<PlannerSettingsValidationError> errors)
        where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            errors.Add(new PlannerSettingsValidationError(section, property, $"{property} has an unsupported value."));
        }
    }

    private static void ValidateRange<T>(
        T value,
        T minimum,
        T maximum,
        PlannerSettingsSection section,
        string property,
        ICollection<PlannerSettingsValidationError> errors)
        where T : IComparable<T>
    {
        if (value.CompareTo(minimum) < 0 || value.CompareTo(maximum) > 0)
        {
            errors.Add(new PlannerSettingsValidationError(
                section,
                property,
                $"{property} must be between {minimum} and {maximum}."));
        }
    }
}
