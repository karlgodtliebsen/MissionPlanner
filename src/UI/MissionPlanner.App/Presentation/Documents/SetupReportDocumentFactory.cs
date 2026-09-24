using MissionPlanner.Core.Setup.Reporting;

namespace MissionPlanner.App.Presentation.Documents;

/// <summary>Formats plain subsystem evidence as safe, selectable Markdown documents.</summary>
public sealed class SetupReportDocumentFactory
{
    /// <summary>Builds the overview, keeping operation messages distinct from confirmed configuration.</summary>
    public UserDocument CreateOverview(SetupReport report, string? workflowStatus, string? workflowError)
    {
        var document = new UserDocumentBuilder().Heading(report.Title).Paragraph(report.Overview)
            .Heading("Current state", 3).Paragraph(report.Status);
        foreach (var fact in report.Facts)
        {
            document.Bullet($"{fact.Name}: {fact.Value}");
        }
        document.Heading("Setup guidance", 3);
        foreach (var guidance in report.Guidance)
        {
            document.Paragraph(guidance);
        }
        if (!string.IsNullOrWhiteSpace(workflowStatus) || !string.IsNullOrWhiteSpace(workflowError))
        {
            document.Heading("Latest workflow message", 3);
            foreach (var line in (workflowStatus ?? string.Empty).Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
            {
                document.Paragraph(line);
            }
            document.Paragraph(workflowError);
        }
        return document.Build(report.Title);
    }

    /// <summary>Builds copyable assignments without promoting missing observations to numeric values.</summary>
    public UserDocument? CreateParameters(SetupReport report)
    {
        if (report.Parameters.Count == 0)
        {
            return null;
        }
        return new UserDocumentBuilder().Heading("Relevant parameters — confirmed vehicle state", 3)
            .CodeBlock(report.Parameters.Select(parameter => parameter.Value is { } value
                ? UserDocumentBuilder.ParameterAssignment(parameter.Name, value, parameter.Comment)
                : UserDocumentBuilder.ParameterComment($"{parameter.Name}: value unavailable")))
            .Build("Confirmed parameters");
    }
}
