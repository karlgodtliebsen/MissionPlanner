using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.App.Views.ConfigTuning;

/// <summary>
/// Handles saving and loading parameters to and from a file in CSV format.
/// </summary>
public class ParametersFileHandler
{
    /// <summary>
    /// Saves the provided list of parameters to a file in CSV format, with each parameter's name and value on a separate line.
    /// </summary>
    /// <param name="parameters">The list of parameters to save.</param>
    /// <param name="fileName">The file name to save the parameters to.</param>
    public void SaveParametersToFile(IList<VehicleParameter> parameters, string fileName)
    {
        using var s = File.CreateText(fileName);

        foreach (var parameter in parameters)
        {
            s.WriteLine($"{parameter.Name},{parameter.Value}");
        }
    }

    /// <summary>
    /// Loads parameters from a file and updates the existing parameters list with the values from the file. If a parameter from the file does not exist in the existing list, it is ignored.
    /// </summary>
    /// <param name="existingParameters">The list of existing parameters to update.</param>
    /// <param name="fileName">The file name to load parameters from.</param>
    /// <returns>A list of updated parameters.</returns>
    public IList<VehicleParameter> LoadParametersFromFile(IList<VehicleParameter> existingParameters, string fileName)
    {
        IList<VehicleParameter> parameters = [];
        using var s = File.OpenText(fileName);
        while (!s.EndOfStream)
        {
            var line = s.ReadLine();
            if (line != null)
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
        }

        return parameters;
    }
}
