using System.Globalization;
using System.Text;
using System.Text.Json;
using MissionPlanner.App.Presentation;
using MissionPlanner.App.Views.Common;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.App.Models;

/// <summary>Imports and exports vehicle parameter files through Avalonia's file-service boundary.</summary>
public sealed class ParametersFileHandler(IFileOpenService fileOpenService, IFileSaveService fileSaveService)
{
    private static readonly JsonSerializerOptions options = new(JsonSerializerDefaults.General);

    /// <summary>Saves parameters as invariant-culture comma-separated name/value pairs.</summary>
    public async Task<string?> SaveParametersToFile(IList<VehicleParameter> parameters, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream();
        await using (var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true))
        {
            foreach (var parameter in parameters)
            {
                await writer.WriteLineAsync($"{parameter.Name},{parameter.Value.ToString("R", CultureInfo.InvariantCulture)}");
            }

            await writer.FlushAsync(cancellationToken);
        }

        stream.Position = 0;
        return await fileSaveService.SaveAsync("ardupilot.params", stream, cancellationToken);
    }

    /// <summary>Saves a UTF-8 text document through the platform file saver.</summary>
    public async Task<string?> SaveTextFileAsync(string fileName, string content, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(content);
        await using var stream = new MemoryStream(new UTF8Encoding(false).GetBytes(content));
        return await fileSaveService.SaveAsync(fileName, stream, cancellationToken);
    }

    /// <summary>Loads a UTF-8 text document selected through the platform file picker.</summary>
    public async Task<string?> LoadTextFileAsync(string pickerTitle, CancellationToken cancellationToken)
    {
        using var file = await fileOpenService.OpenAsync(pickerTitle, cancellationToken: cancellationToken);
        if (file is null)
        {
            return null;
        }

        using var reader = new StreamReader(file.Content, Encoding.UTF8, leaveOpen: true);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    /// <summary>Saves the displayed parameter fields as JSON.</summary>
    public async Task<string?> SaveParametersToJsonFile(IList<ParameterItemViewModel> parameters, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream();
        await JsonSerializer.SerializeAsync(stream, parameters, options, cancellationToken);
        stream.Position = 0;
        return await fileSaveService.SaveAsync("ardupilot.params.json", stream, cancellationToken);
    }

    /// <summary>Loads displayed parameter fields from a JSON file.</summary>
    public async Task<IList<ParameterItemViewModel>> LoadParametersFromJsonFileAsync(CancellationToken cancellationToken)
    {
        using var file = await fileOpenService.OpenAsync("Select a Parameters JSON file", ["*.json"], cancellationToken);
        if (file is null)
        {
            return [];
        }

        return await JsonSerializer.DeserializeAsync<IList<ParameterItemViewModel>>(
            file.Content,
            options,
            cancellationToken) ?? [];
    }

    /// <summary>Loads matching invariant-culture values from an ArduPilot parameter file.</summary>
    public async Task<IList<VehicleParameter>> LoadParametersFromFileAsync(
        IList<VehicleParameter> existingParameters,
        CancellationToken cancellationToken)
    {
        var parameters = new List<VehicleParameter>();
        using var file = await fileOpenService.OpenAsync(
            "Select a Parameters file",
            ["*.params", "*.param", "*.txt"],
            cancellationToken);
        if (file is null)
        {
            return parameters;
        }

        var existingByName = existingParameters.ToDictionary(parameter => parameter.Name, StringComparer.Ordinal);
        using var reader = new StreamReader(file.Content, Encoding.UTF8, leaveOpen: true);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            var parts = line.Split([',', '\t'], 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 ||
                !existingByName.TryGetValue(parts[0], out var existing) ||
                !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                continue;
            }

            parameters.Add(existing with { Value = value });
        }

        return parameters;
    }
}
