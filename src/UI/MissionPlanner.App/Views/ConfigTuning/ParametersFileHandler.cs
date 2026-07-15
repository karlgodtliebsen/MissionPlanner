using System.Text;
using System.Text.Json;
using CommunityToolkit.Maui.Storage;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.App.Views.ConfigTuning;

/// <summary>
/// Handles saving and loading parameters to and from a file in CSV format.
/// </summary>
public class ParametersFileHandler(IFileSaver fileSaver)
{
    private static readonly JsonSerializerOptions options = new() { };


    /// <summary>
    /// Saves the provided list of parameters to a file in CSV format, with each parameter's name and value on a separate line.
    /// </summary>
    /// <param name="parameters">The list of parameters to save.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    public async Task<string?> SaveParametersToFile(IList<VehicleParameter> parameters, CancellationToken cancellationToken)
    {
        var ms = new MemoryStream();
        await using var s = new StreamWriter(ms, Encoding.UTF8, leaveOpen: true);
        foreach (var parameter in parameters)
        {
            await s.WriteLineAsync($"{parameter.Name},{parameter.Value}");
        }

        var result = await fileSaver.SaveAsync("ardupilot.params", s.BaseStream, cancellationToken);
        return result.FilePath;
    }

    /// <summary>
    /// Saves the provided list of parameters to a JSON file.
    /// </summary>
    /// <param name="parameters">The list of parameters to save.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>The file path of the saved JSON file.</returns>
    public async Task<string?> SaveParametersToJsonFile(IList<ParameterItemViewModel> parameters, CancellationToken cancellationToken)
    {
        var ms = new MemoryStream();
        await JsonSerializer.SerializeAsync(ms, parameters, options, cancellationToken);

        var result = await fileSaver.SaveAsync("ardupilot.params.json", ms, cancellationToken);
        return result.FilePath;
    }

    /// <summary>
    /// Loads parameters from a JSON file and returns them as a list of ParameterItemViewModel.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>A list of ParameterItemViewModel loaded from the file.</returns>
    public async Task<IList<ParameterItemViewModel>> LoadParametersFromJsonFileAsync(CancellationToken cancellationToken)
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Select a Parameters file", FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>> { { DevicePlatform.iOS, new[] { "public.json" } }, { DevicePlatform.Android, new[] { "application/json" } }, { DevicePlatform.WinUI, new[] { ".json" } }, { DevicePlatform.MacCatalyst, new[] { "public.json" } } }) });

        if (result == null)
        {
            return [];
        }

        await using var stream = await result.OpenReadAsync();
        var data = await JsonSerializer.DeserializeAsync<IList<ParameterItemViewModel>>(stream, options, cancellationToken);
        return data ?? [];
    }

    /// <summary>
    /// Loads parameters from a file and updates the existing parameters list with the values from the file. If a parameter from the file does not exist in the existing list, it is ignored.
    /// </summary>
    /// <param name="existingParameters">The list of existing parameters to update.</param>
    /// <param name="cancellationToken"></param>
    /// <returns>A list of updated parameters.</returns>
    public async Task<IList<VehicleParameter>> LoadParametersFromFileAsync(IList<VehicleParameter> existingParameters, CancellationToken cancellationToken)
    {
        IList<VehicleParameter> parameters = [];

        var result = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Select a Parameters file" });

        if (result == null)
        {
            return [];
        }

        await using var stream = await result.OpenReadAsync();
        using var reader = new StreamReader(stream);
        //var content = await reader.ReadToEndAsync(cancellationToken);
        var line = string.Empty;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            var parts = line.Split(',');
            if (parts.Length == 2)
            {
                var existingParameter = existingParameters.FirstOrDefault(p => p.Name == parts[0]);
                if (existingParameter != null)
                {
                    var name = parts[0];
                    var value = float.TryParse(parts[1], out var parsedValue) ? parsedValue : 0;

                    parameters.Add(new VehicleParameter(
                        name,
                        value,
                        existingParameter.Type,
                        existingParameter.Index,
                        existingParameter.Count)
                    );
                }
                else
                {
                    //parameters.Add(new ParameterItemViewModel
                    //{
                    //    Name = parts[0],
                    //    Value = parts[1]
                    //});
                }
            }
        }

        return parameters;
    }
}
