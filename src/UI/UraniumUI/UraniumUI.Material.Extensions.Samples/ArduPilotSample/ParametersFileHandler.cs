using System.Text.Json;

namespace UraniumUI.Material.Extensions.Samples.ArduPilotSample;

/// <summary>Imports and exports vehicle parameter files for the Config editing session.</summary>
public sealed class ParametersFileHandler()
{
    private static readonly JsonSerializerOptions options = new();

    /// <summary>Loads displayed parameter fields from a JSON file.</summary>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>The fields loaded from the selected file.</returns>
    public async Task<IList<ParameterItemViewModel>> LoadParametersFromJsonFileAsync(CancellationToken cancellationToken)
    {
        var result = await FilePicker.Default.PickAsync(
            new PickOptions { PickerTitle = "Select a test data file", FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>> { [DevicePlatform.iOS] = ["public.json"], [DevicePlatform.Android] = ["application/json"], [DevicePlatform.WinUI] = [".json"], [DevicePlatform.MacCatalyst] = ["public.json"] }) });
        if (result is null)
        {
            return [];
        }

        await using var stream = await result.OpenReadAsync();
        var data = await JsonSerializer.DeserializeAsync<IList<ParameterItemViewModel>>(stream, options, cancellationToken);
        return data ?? [];
    }
}
