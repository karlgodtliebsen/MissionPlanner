using System.Globalization;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Maui.Storage;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.App.Views.ConfigTuning;

/// <summary>Imports and exports vehicle parameter files for the Config editing session.</summary>
public sealed class ParametersFileHandler(IFileSaver fileSaver)
{
    private static readonly JsonSerializerOptions options = new();

    /// <summary>Saves parameters as invariant-culture comma-separated name/value pairs.</summary>
    /// <param name="parameters">The parameters to save.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>The saved file path, when supplied by the platform.</returns>
    public async Task<string?> SaveParametersToFile(IList<VehicleParameter> parameters, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream();
        await using var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true);
        foreach (var parameter in parameters)
        {
            await writer.WriteLineAsync($"{parameter.Name},{parameter.Value.ToString("R", CultureInfo.InvariantCulture)}");
        }

        await writer.FlushAsync(cancellationToken);
        stream.Position = 0;
        var result = await fileSaver.SaveAsync("ardupilot.params", stream, cancellationToken);
        return result.FilePath;
    }

    /// <summary>Saves the displayed parameter fields as JSON.</summary>
    /// <param name="parameters">The displayed parameter fields.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>The saved file path, when supplied by the platform.</returns>
    public async Task<string?> SaveParametersToJsonFile(IList<ParameterItemViewModel> parameters, CancellationToken cancellationToken)
    {
        await using var stream = new MemoryStream();
        await JsonSerializer.SerializeAsync(stream, parameters, options, cancellationToken);
        stream.Position = 0;
        var result = await fileSaver.SaveAsync("ardupilot.params.json", stream, cancellationToken);
        return result.FilePath;
    }

    /// <summary>Loads displayed parameter fields from a JSON file.</summary>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>The fields loaded from the selected file.</returns>
    public async Task<IList<ParameterItemViewModel>> LoadParametersFromJsonFileAsync(CancellationToken cancellationToken)
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Select a Parameters file",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                [DevicePlatform.iOS] = ["public.json"],
                [DevicePlatform.Android] = ["application/json"],
                [DevicePlatform.WinUI] = [".json"],
                [DevicePlatform.MacCatalyst] = ["public.json"]
            })
        });
        if (result is null)
        {
            return [];
        }

        await using var stream = await result.OpenReadAsync();
        var data = await JsonSerializer.DeserializeAsync<IList<ParameterItemViewModel>>(stream, options, cancellationToken);
        return data ?? [];
    }

    /// <summary>Loads matching invariant-culture values from an ArduPilot parameter file.</summary>
    /// <param name="existingParameters">The live parameters that imported values are allowed to target.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>The matching imported parameter values.</returns>
    public async Task<IList<VehicleParameter>> LoadParametersFromFileAsync(
        IList<VehicleParameter> existingParameters,
        CancellationToken cancellationToken)
    {
        var parameters = new List<VehicleParameter>();
        var result = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Select a Parameters file" });
        if (result is null)
        {
            return parameters;
        }

        var existingByName = existingParameters.ToDictionary(parameter => parameter.Name, StringComparer.Ordinal);
        await using var stream = await result.OpenReadAsync();
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
            {
                continue;
            }

            var parts = line.Split([',', '\t'], 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2 || !existingByName.TryGetValue(parts[0], out var existing) ||
                !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                continue;
            }

            parameters.Add(existing with { Value = value });
        }

        return parameters;
    }
}
