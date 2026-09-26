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
            .Paragraph("Enter one name/value assignment per line. Accepted separators: = , : ;. Line endings: CRLF or LF. Use a decimal point for numeric values.")
            .Paragraph("Lines starting with // and inline // comments are supported. Unknown names and invalid values are skipped and reported.");
        var headings = new[] { "Write status", "Progress", "Errors", "Input report" };
        for (var index = 0; index < Math.Min(values.Count, headings.Length); index++)
        {
            if (values[index] is not string text || string.IsNullOrWhiteSpace(text)) continue;
            builder.Heading(headings[index], 3);
            foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
                builder.Paragraph(line);
        }
        return builder.Build("Parameter editor");
    }
}
