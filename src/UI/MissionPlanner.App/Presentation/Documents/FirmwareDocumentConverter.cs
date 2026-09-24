using System.Globalization;
using Avalonia.Data.Converters;

namespace MissionPlanner.App.Presentation.Documents;

/// <summary>Composes firmware reports from observable presentation text without changing workflow state.</summary>
public sealed class FirmwareDocumentConverter : IMultiValueConverter
{
    /// <summary>Gets the shared stateless binding converter.</summary>
    public static FirmwareDocumentConverter Instance { get; } = new();

    /// <summary>Gets the formatter for sections whose first binding supplies their heading.</summary>
    public static FirmwareDocumentConverter SectionInstance { get; } = new() { FirstValueIsHeading = true };

    private bool FirstValueIsHeading { get; init; }

    /// <summary>Formats an optional heading and escaped paragraphs, retaining multiline diagnostics.</summary>
    public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var title = parameter as string;
        var builder = new UserDocumentBuilder();
        if (!string.IsNullOrWhiteSpace(title))
        {
            builder.Heading(title);
        }
        for (var index = 0; index < values.Count; index++)
        {
            // Unresolved bindings are not evidence and must not appear in the report.
            if (values[index] is not string text)
            {
                continue;
            }
            if (index == 0 && FirstValueIsHeading)
            {
                builder.Heading(text);
                continue;
            }
            foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                builder.Paragraph(line);
            }
        }
        return builder.Build(title);
    }
}
