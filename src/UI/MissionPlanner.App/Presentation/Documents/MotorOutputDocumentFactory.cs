using System.Globalization;
using System.Text.RegularExpressions;

namespace MissionPlanner.App.Presentation.Documents;

/// <summary>Formats existing motor-output diagnostics without discarding any evidence or guidance.</summary>
public static class MotorOutputDocumentFactory
{
    /// <summary>Builds copyable parameter assignments and escaped diagnostic prose.</summary>
    public static UserDocument Create(string configuration, string guidance)
    {
        var document = new UserDocumentBuilder().Heading("Motor output diagnostics");
        var assignments = new List<string>();
        var evidence = new List<string>();
        foreach (var line in configuration.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            var match = Regex.Match(line, @"^([A-Z][A-Z0-9_]*) = (.*)$", RegexOptions.CultureInvariant);
            if (!match.Success)
            {
                evidence.Add(line);
                continue;
            }
            assignments.Add(double.TryParse(match.Groups[2].Value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var value) && double.IsFinite(value)
                ? line : UserDocumentBuilder.ParameterComment(line));
        }
        document.CodeBlock(assignments);
        foreach (var line in evidence)
        {
            document.Paragraph(line);
        }
        if (!string.IsNullOrWhiteSpace(guidance))
        {
            document.Heading("Rotation evidence and troubleshooting", 3);
            foreach (var line in guidance.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                document.Paragraph(line);
            }
        }
        return document.Build("Motor output diagnostics");
    }
}
