using MissionPlanner.MavLink.Parameters;

namespace MissionPlanner.Core.Setup.OptionalHardware.Motor;

/// <summary>Resolves supported matrix layouts from active frame parameters.</summary>
public sealed class MotorLayoutResolver
{
    /// <summary>Resolves a layout or returns null for missing/custom/unsupported frames.</summary>
    public MotorLayout? Resolve(IReadOnlyDictionary<string, VehicleParameter> parameters)
    {
        var className = parameters.ContainsKey("Q_FRAME_CLASS") ? "Q_FRAME_CLASS" : "FRAME_CLASS";
        var typeName = className[0] == 'Q' ? "Q_FRAME_TYPE" : "FRAME_TYPE";
        if (!parameters.TryGetValue(className, out var frameClass))
        {
            return null;
        }

        var value = (int)Math.Round(frameClass.Value);
        var count = value switch { 1 => 4, 2 => 6, 3 => 8, 4 => 8, 5 => 6, 7 => 3, 12 => 12, 13 => 10, var _ => 0 };
        if (count == 0)
        {
            return null;
        }

        var type = parameters.TryGetValue(typeName, out var frameType) ? (int)Math.Round(frameType.Value) : 0;
        var motors = Enumerable.Range(1, count)
            .Select(order => new MotorLayoutMotor(order, order, $"Test {(char)('A' + order - 1)} — Motor {order}")).ToArray();
        return new MotorLayout(value, type, $"{ClassName(value)} / type {type}", motors);
    }

    private static string ClassName(int value)
    {
        return value switch
        {
            1 => "Quad", //
            2 => "Hexa",
            3 => "Octa",
            4 => "OctaQuad",
            5 => "Y6",
            7 => "Tri",
            12 => "DodecaHexa",
            13 => "Deca",
            var _ => "Unsupported"
        };
    }
}
