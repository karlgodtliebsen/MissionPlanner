namespace MissionPlanner.Core.FlightData.Scripting;

/// <summary>Applies strict schema and bounded-execution rules.</summary>
public sealed class VehicleScriptValidator(IVehicleScriptActionRegistry registry) : IVehicleScriptValidator
{
    /// <inheritdoc />
    public VehicleScriptValidationResult Validate(VehicleScriptDocument document)
    {
        var errors = new List<string>();
        if (document.Version != 1)
        {
            errors.Add($"Unsupported script version {document.Version}; expected 1.");
        }

        if (string.IsNullOrWhiteSpace(document.Name))
        {
            errors.Add("Script name is required.");
        }

        if (document.Steps.Count is 0 or > 100)
        {
            errors.Add("A script must contain 1 to 100 steps.");
        }

        for (var index = 0; index < document.Steps.Count; index++)
        {
            var step = document.Steps[index];
            if (!registry.Actions.Contains(step.Action))
            {
                errors.Add($"Step {index + 1}: action '{step.Action}' is not allowed.");
            }

            if (step.TimeoutSeconds is < 1 or > 300)
            {
                errors.Add($"Step {index + 1}: timeout must be 1 to 300 seconds.");
            }

            if (step.Action.Equals("delay", StringComparison.OrdinalIgnoreCase) &&
                (!step.Arguments.TryGetValue("milliseconds", out var delay) || !int.TryParse(delay, out var milliseconds) || milliseconds is < 0 or > 60000))
            {
                errors.Add($"Step {index + 1}: delay milliseconds must be 0 to 60000.");
            }
        }

        return new VehicleScriptValidationResult(errors.Count == 0, errors);
    }
}
