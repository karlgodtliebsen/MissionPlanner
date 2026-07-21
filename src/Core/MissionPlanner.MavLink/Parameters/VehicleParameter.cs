namespace MissionPlanner.MavLink.Parameters;

/// <summary>
/// Represents a vehicle parameter with its name, value, type, and context.
/// </summary>
/// <param name="Name">The parameter name (max 16 characters).</param>
/// <param name="Value">The parameter value as a float (wire format).</param>
/// <param name="Type">The parameter type.</param>
/// <param name="Index">The parameter index in the list.</param>
/// <param name="Count">The total number of parameters.</param>
public sealed record VehicleParameter(string Name, float Value, MavParamType Type, ushort Index, ushort Count)
{
    /// <inheritdoc />
    public override string ToString()
    {
        return $"VehicleParameter Name = {Name}, Value = {Value}, Type = {Type}, Index = {Index}, Count = {Count} ";
    }
};
