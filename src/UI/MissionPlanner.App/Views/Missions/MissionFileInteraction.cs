using System.Text;
using Microsoft.Extensions.Logging;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Utilities.Dialogs;
using MissionPlanner.Core.Missions.Abstractions;
using MissionPlanner.Core.Missions.Files;
using MissionPlanner.Core.Missions.Models;
using MissionPlanner.Core.Missions.Planning;

namespace MissionPlanner.App.Views.Missions;

/// <summary>The outcome of saving the mission to a file.</summary>
/// <param name="Message">The status message to show, or <see langword="null"/> when the user cancelled silently.</param>
/// <param name="ClearDirtyRows">Whether pending row edits should be discarded because the save ran.</param>
public sealed record MissionSaveResult(string? Message, bool ClearDirtyRows);

/// <summary>The outcome of picking and parsing a mission file.</summary>
/// <param name="Content">The parsed content, or <see langword="null"/> when cancelled or failed.</param>
/// <param name="FileName">The name of the opened file.</param>
/// <param name="Message">The failure message, or <see langword="null"/> when cancelled or loaded.</param>
public sealed record MissionLoadResult(MissionFileContent? Content, string FileName, string? Message)
{
    /// <summary>Gets the status message for a successful load of <see cref="Content"/>.</summary>
    public string SuccessMessage => Content is null
        ? string.Empty
        : Content.SkippedItems == 0
            ? $"Loaded {Content.Items.Count} items from {FileName}."
            : $"Loaded {Content.Items.Count} items from {FileName}; skipped {Content.SkippedItems} unsupported.";
}

/// <summary>A user-selected text file that has been read.</summary>
/// <param name="FileName">The display name of the file.</param>
/// <param name="Text">The decoded file content.</param>
public sealed record OpenedText(string FileName, string Text);

/// <summary>The outcome of picking and importing a geospatial file.</summary>
/// <param name="Imported">The imported features, or <see langword="null"/> when cancelled or failed.</param>
/// <param name="Message">The failure message, or <see langword="null"/> when cancelled or imported.</param>
public sealed record GeospatialFileResult(GeospatialImportResult? Imported, string? Message);

/// <summary>Owns the file pickers, save formats, and confirmation prompts used by the mission editor.</summary>
public interface IMissionFileInteraction
{
    /// <summary>Asks for a save format and file, then writes the mission.</summary>
    Task<MissionSaveResult> SaveMissionAsync(Mission mission, GeoPosition? home, CancellationToken cancellationToken);

    /// <summary>Asks for a mission file and parses it.</summary>
    Task<MissionLoadResult> LoadMissionAsync(CancellationToken cancellationToken);

    /// <summary>Saves text under a suggested name and returns the saved path, or <see langword="null"/> when cancelled.</summary>
    Task<string?> SaveTextAsync(string suggestedFileName, string text, CancellationToken cancellationToken);

    /// <summary>Asks for a text file and reads it, or returns <see langword="null"/> when cancelled.</summary>
    Task<OpenedText?> OpenTextAsync(string title, CancellationToken cancellationToken);

    /// <summary>Asks for a KML/KMZ or shapefile and imports it as WGS84 features.</summary>
    Task<GeospatialFileResult> ImportGeospatialAsync(string title, bool includeCompanions, CancellationToken cancellationToken);
}

/// <summary>Default <see cref="IMissionFileInteraction"/> backed by the platform file services.</summary>
public sealed class MissionFileInteraction : IMissionFileInteraction
{
    private const long MaximumCompanionBytes = 16 * 1024 * 1024;
    private const string Wgs84Projection = "GEOGCS[\"WGS 84\",DATUM[\"WGS_1984\"]]";

    private readonly IFileOpenService fileOpenService;
    private readonly IFileSaveService fileSaveService;
    private readonly IDialogService dialogService;
    private readonly IUserConfirmationService confirmationService;
    private readonly IMissionFileCodec fileCodec;
    private readonly IGeospatialImportService geospatialImportService;
    private readonly ILogger<MissionFileInteraction> logger;

    /// <summary>Initializes a new instance of the <see cref="MissionFileInteraction"/> class.</summary>
    public MissionFileInteraction(
        IFileOpenService fileOpenService,
        IFileSaveService fileSaveService,
        IDialogService dialogService,
        IUserConfirmationService confirmationService,
        IMissionFileCodec fileCodec,
        IGeospatialImportService geospatialImportService,
        ILogger<MissionFileInteraction> logger)
    {
        this.fileOpenService = fileOpenService;
        this.fileSaveService = fileSaveService;
        this.dialogService = dialogService;
        this.confirmationService = confirmationService;
        this.fileCodec = fileCodec;
        this.geospatialImportService = geospatialImportService;
        this.logger = logger;
    }

    /// <inheritdoc />
    public async Task<MissionSaveResult> SaveMissionAsync(Mission mission, GeoPosition? home, CancellationToken cancellationToken)
    {
        if (mission.Items.Count == 0)
        {
            return new MissionSaveResult("Mission is empty.", false);
        }

        try
        {
            var (format, extension) = await PickSaveFormatAsync(cancellationToken);
            if (format is null || extension is null)
            {
                return new MissionSaveResult(null, false);
            }

            var path = await SaveTextAsync($"{mission.Name}{extension}", fileCodec.Build(mission, home, format.Value), cancellationToken);
            return new MissionSaveResult(path is not null ? $"Mission saved to {path}." : "Save cancelled.", true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save mission file");
            return new MissionSaveResult($"Save failed: {ex.Message}", true);
        }
    }

    /// <inheritdoc />
    public async Task<MissionLoadResult> LoadMissionAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var file = await fileOpenService.OpenAsync(
                "Select mission file (.waypoints, .txt, .mission)",
                ["*.waypoints", "*.txt", "*.mission"],
                cancellationToken);
            if (file is null)
            {
                return new MissionLoadResult(null, string.Empty, null);
            }

            using var reader = new StreamReader(file.Content, leaveOpen: true);
            var content = await reader.ReadToEndAsync(cancellationToken);
            return new MissionLoadResult(fileCodec.Parse(content), file.FileName, null);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Failed to load mission file");
            return new MissionLoadResult(null, string.Empty, $"Load failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<string?> SaveTextAsync(string suggestedFileName, string text, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        return await fileSaveService.SaveAsync(suggestedFileName, stream, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OpenedText?> OpenTextAsync(string title, CancellationToken cancellationToken)
    {
        using var file = await fileOpenService.OpenAsync(title, cancellationToken: cancellationToken);
        if (file is null)
        {
            return null;
        }

        using var reader = new StreamReader(file.Content, Encoding.UTF8, true, leaveOpen: true);
        return new OpenedText(file.FileName, await reader.ReadToEndAsync(cancellationToken));
    }

    /// <inheritdoc />
    public async Task<GeospatialFileResult> ImportGeospatialAsync(string title, bool includeCompanions, CancellationToken cancellationToken)
    {
        using var file = await fileOpenService.OpenAsync(title, cancellationToken: cancellationToken);
        if (file is null)
        {
            return new GeospatialFileResult(null, null);
        }

        await using var memory = new MemoryStream();
        await file.Content.CopyToAsync(memory, cancellationToken);
        var companions = new Dictionary<string, ReadOnlyMemory<byte>>(StringComparer.OrdinalIgnoreCase);
        if (includeCompanions && file.FullPath is { } fullPath)
        {
            foreach (var extension in new[] { ".prj", ".dbf" })
            {
                var path = Path.ChangeExtension(fullPath, extension);
                if (File.Exists(path) && new FileInfo(path).Length <= MaximumCompanionBytes)
                {
                    companions[extension] = await File.ReadAllBytesAsync(path, cancellationToken);
                }
            }
        }

        if (includeCompanions && !companions.ContainsKey(".prj"))
        {
            var assume = await confirmationService.ConfirmAsync("Missing coordinate system", "No .prj file was found. Treat plausible coordinates as WGS84?", "Use WGS84", cancellationToken);
            if (!assume)
            {
                return new GeospatialFileResult(null, null);
            }

            companions[".prj"] = Encoding.UTF8.GetBytes(Wgs84Projection);
        }

        var result = geospatialImportService.Import(new GeospatialSource(file.FileName, memory.ToArray(), companions));
        return result.Succeeded ? new GeospatialFileResult(result, null) : new GeospatialFileResult(null, result.Message);
    }

    private async Task<(MissionFileFormat? Format, string? Extension)> PickSaveFormatAsync(CancellationToken cancellationToken)
    {
        var options = dialogService.CreateOptions("Save mission as");
        var choice = await dialogService.ChooseAsync(options, ["Waypoints (.waypoints)", "Text (.txt)", "Mission JSON (.mission)"], cancellationToken);
        return choice switch
        {
            "Waypoints (.waypoints)" => (MissionFileFormat.QgcWpl, ".waypoints"),
            "Text (.txt)" => (MissionFileFormat.QgcWpl, ".txt"),
            "Mission JSON (.mission)" => (MissionFileFormat.MissionJson, ".mission"),
            var _ => (null, null)
        };
    }
}
