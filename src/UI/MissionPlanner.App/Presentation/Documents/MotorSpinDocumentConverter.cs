using System.Globalization;
using Avalonia.Data.Converters;

namespace MissionPlanner.App.Presentation.Documents;

/// <summary>Formats live motor-spin calculations without changing the motor-test workflow.</summary>
public sealed class MotorSpinDocumentConverter : IMultiValueConverter
{
    /// <summary>Gets the shared converter for both spin sections.</summary>
    public static MotorSpinDocumentConverter Instance { get; } = new();

    /// <inheritdoc />
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var minimum = parameter as string == "MOT_SPIN_MIN";
        var name = minimum ? "MOT_SPIN_MIN" : "MOT_SPIN_ARM";
        var builder = new UserDocumentBuilder().Heading(name)
            .Paragraph(minimum ? "Set the minimum motor output while flying."
                : "Set the minimum output at which motors reliably spin while armed.")
            .Paragraph(minimum ? "Proposed armed output + offset = minimum flying output."
                : "Motor test throttle + offset = armed output.");
        if (values.Count >= 4 && values[1] is int basis && values[2] is int offset && values[3] is int sum)
        {
            builder.Paragraph($"{basis}% + {offset}% = {sum}%")
                .CodeBlock([
                    UserDocumentBuilder.ParameterComment(values[0] is string current ? $"Current: {current}" : "Current value unavailable"),
                    UserDocumentBuilder.ParameterAssignment(name, sum / 100d, "proposed value; use Set to write")
                ]);
        }
        return builder.Build(name);
    }
}
