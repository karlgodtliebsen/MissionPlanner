using System.Globalization;
using Avalonia.Data.Converters;

namespace MissionPlanner.App.Presentation.Documents;

/// <summary>Combines editor instructions and live write feedback into one Markdown document.</summary>
public sealed class ParameterEditorDocumentConverter : IMultiValueConverter
{
    /// <summary>Gets the shared presentation converter.</summary>
    public static ParameterEditorDocumentConverter Instance { get; } = new();

    /// <inheritdoc />
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var builder = new UserDocumentBuilder()
            .Heading("Parameter format")
            .Paragraph("Enter one name/value assignment per line.")
            .Paragraph("Accepted separators: = , : ;.")
            .Paragraph("Line endings: CRLF or LF.")
            .Paragraph("Use a decimal point for numeric values.")
            .Paragraph("Comments: // or #")
            .Paragraph("Lines starting with // or # and inline comments using // or # are supported.")
            .Paragraph("Edits are shared across the parameter editor tabs. Review and write them in Write modified parameters.")
            .Paragraph("Unknown names and incomplete values are skipped. Invalid pending values are reported and cannot be written.");
        var headings = new[] { "Write status", "Progress", "Errors", "Input report" };
        for (var index = 0; index < Math.Min(values.Count, headings.Length); index++)
        {
            if (values[index] is not string text || string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            builder.Heading(headings[index], 3);
            foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                builder.Paragraph(line);
            }
        }
        return builder.Build("Parameter editor");
    }
}
