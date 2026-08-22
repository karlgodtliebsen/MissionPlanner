using System.Text.Json;
using System.Text.Json.Serialization;
using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup.OptionalHardware.Motor;

/// <summary>
/// Resolves supported matrix layouts from active frame parameters.
/// </summary>
public sealed class MotorLayoutResolver
{
    private const string LayoutResourceName = "MissionPlanner.Core.Setup.OptionalHardware.Motor.APMotorLayout.json";
    private static readonly IReadOnlyDictionary<(int FrameClass, int FrameType), LayoutDefinition> layouts = LoadLayouts();

    /// <summary>Resolves a layout or returns null for missing/custom/unsupported frames.</summary>
    public MotorLayout? Resolve(IReadOnlyDictionary<string, VehicleParameter> parameters)
    {
        var className = parameters.ContainsKey("Q_FRAME_CLASS") ? "Q_FRAME_CLASS" : "FRAME_CLASS";
        var typeName = className[0] == 'Q' ? "Q_FRAME_TYPE" : "FRAME_TYPE";
        if (!parameters.TryGetValue(className, out var frameClass))
        {
            return null;
        }

        var frameClassValue = (int)Math.Round(frameClass.Value);
        var frameTypeValue = parameters.TryGetValue(typeName, out var frameType)
            ? (int)Math.Round(frameType.Value)
            : 0;

        if (!layouts.TryGetValue((frameClassValue, frameTypeValue), out var definition))
        {
            return null;
        }

        var motors = definition.Motors
            .Select(motor => new MotorLayoutMotor(
                motor.Number,
                motor.TestOrder,
                ParseRotation(motor.Rotation),
                motor.Roll,
                motor.Pitch))
            .ToArray();

        return new MotorLayout(
            definition.FrameClass,
            definition.FrameType,
            $"{definition.ClassName} / {definition.TypeName}",
            motors);
    }

    private static IReadOnlyDictionary<(int FrameClass, int FrameType), LayoutDefinition> LoadLayouts()
    {
        using var stream = typeof(MotorLayoutResolver).Assembly.GetManifestResourceStream(LayoutResourceName)
            ?? throw new InvalidOperationException($"Embedded motor layout resource '{LayoutResourceName}' was not found.");
        var catalog = JsonSerializer.Deserialize<LayoutCatalog>(stream)
            ?? throw new InvalidOperationException("The embedded motor layout catalog is invalid.");
        return catalog.Layouts.ToDictionary(layout => (layout.FrameClass, layout.FrameType));
    }

    private static MotorRotation ParseRotation(string? rotation)
    {
        return rotation switch
        {
            "CW" => MotorRotation.Clockwise,
            "CCW" => MotorRotation.CounterClockwise,
            var _ => MotorRotation.Unknown
        };
    }

    private sealed record LayoutCatalog(
        [property: JsonPropertyName("layouts")] IReadOnlyList<LayoutDefinition> Layouts);

    private sealed record LayoutDefinition(
        [property: JsonPropertyName("Class")] int FrameClass,
        [property: JsonPropertyName("ClassName")] string ClassName,
        [property: JsonPropertyName("Type")] int FrameType,
        [property: JsonPropertyName("TypeName")] string TypeName,
        [property: JsonPropertyName("motors")] IReadOnlyList<MotorDefinition> Motors);

    private sealed record MotorDefinition(
        [property: JsonPropertyName("Number")] int Number,
        [property: JsonPropertyName("TestOrder")] int TestOrder,
        [property: JsonPropertyName("Rotation")] string? Rotation,
        [property: JsonPropertyName("Roll")] double Roll,
        [property: JsonPropertyName("Pitch")] double Pitch);
}
