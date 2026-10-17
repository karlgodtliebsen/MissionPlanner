using System.Globalization;
using System.Text.RegularExpressions;
using MissionPlanner.Core.ConfigTuning;

namespace MissionPlanner.App.Views.ConfigTuning.Sections.ParametersEditor;

public partial class ParametersEditorViewModel
{
    private IParameterEditSession? editSession;
    private readonly Dictionary<string, double> pendingValues = new(StringComparer.OrdinalIgnoreCase);
    private bool synchronizingText;
    private bool stagingText;
    private bool canEditSession;

    /// <summary>Projects the owning editor's shared session without writing values to the vehicle.</summary>
    /// <remarks>The owner calls this on the UI thread when the session changes.</remarks>
    public void SetEditSession(IParameterEditSession? session, bool canEdit)
    {
        if (!ReferenceEquals(editSession, session))
        {
            editSession = session;
            pendingValues.Clear();
            synchronizingText = true;
            try
            {
                Text = string.Empty;
                SearchText = string.Empty;
                FeedbackReport = string.Empty;
            }
            finally
            {
                synchronizingText = false;
            }
        }

        canEditSession = canEdit && session is { IsValid: true };
        var fields = session is { IsValid: true } ? session.Fields : [];
        SetCurrentParameters(fields.Select(field => new KeyValuePair<string, double>(field.Name, field.LiveValue)));
        ApplyModifiedCommand.NotifyCanExecuteChanged();
        if (stagingText)
        {
            return;
        }

        // Only rewrite values changed elsewhere. This preserves comments, whitespace and
        // incomplete input while the user is typing or a live-value notification arrives.
        var changed = fields.Where(field => !pendingValues.TryGetValue(field.Name, out var previous) ||
                !previous.Equals(field.PendingValue))
            .ToDictionary(field => field.Name, StringComparer.OrdinalIgnoreCase);
        var text = Regex.Replace(Text, @"[^\r\n]+", match =>
        {
            var line = match.Value;
            var comment = line.IndexOf("//", StringComparison.Ordinal);
            var end = comment < 0 ? line.Length : comment;
            var separator = line[..end].IndexOfAny(['=', ',', ':', ';']);
            if (separator < 0 || !changed.TryGetValue(line[..separator].Trim(), out var field))
            {
                return line;
            }
            var start = separator + 1;
            while (start < end && char.IsWhiteSpace(line[start]))
            {
                start++;
            }
            while (end > start && char.IsWhiteSpace(line[end - 1]))
            {
                end--;
            }
            var commentSpacing = start == end && comment == end ? " " : string.Empty;
            return line[..start] + FormatValue(field.PendingValue) + commentSpacing + line[end..];
        });
        var listed = ReadParameterNames(text).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var field in changed.Values.Where(field => field.IsModified && !listed.Contains(field.Name)))
        {
            var separator = text.Length == 0 || text.EndsWith('\n') || text.EndsWith('\r') ? string.Empty : Environment.NewLine;
            text += separator + $"{field.Name} = {FormatValue(field.PendingValue)}" + Environment.NewLine;
        }

        var textChanged = Text != text;
        synchronizingText = true;
        try
        {
            Text = text;
        }
        finally
        {
            synchronizingText = false;
        }
        RememberPendingValues(fields);
        if (textChanged)
        {
            var assignments = ParseAssignments(fields.Select(field => field.Name));
            ReportSkippedParameters(fields.Where(field => assignments.ContainsKey(field.Name) && !field.IsValid)
                .Select(field => $"{field.Name}: {field.ValidationError}"));
        }
    }

    /// <summary>Validates the text and returns the entered names eligible for an explicit write.</summary>
    public IReadOnlyList<string> StageTextChanges()
    {
        if (synchronizingText || stagingText || !canEditSession || editSession is not { IsValid: true } session)
        {
            return [];
        }

        var fields = session.Fields.ToDictionary(field => field.Name, StringComparer.OrdinalIgnoreCase);
        var assignments = ParseAssignments(fields.Keys);
        var accepted = new List<string>();
        var errors = new List<string>();
        stagingText = true;
        try
        {
            using var notifications = session.DeferChangeNotifications();
            foreach (var (name, value) in assignments)
            {
                var field = fields[name];
                var error = field.ValidationError;
                var valid = field.PendingValue.Equals(value)
                    ? field.IsValid
                    : session.TrySetPending(name, value, out error);
                if (valid)
                {
                    accepted.Add(name);
                }
                else
                {
                    errors.Add($"{name}: {error}");
                }
            }
        }
        finally
        {
            stagingText = false;
        }
        RememberPendingValues(session.Fields);
        ReportSkippedParameters(errors);
        return accepted;
    }

    private void RememberPendingValues(IReadOnlyList<ParameterEditField> fields)
    {
        pendingValues.Clear();
        foreach (var field in fields)
        {
            pendingValues[field.Name] = field.PendingValue;
        }
    }

    private static string FormatValue(double value)
    {
        // Round-trip REAL32 values without exposing the double conversion's binary tail.
        return value.Equals((double)(float)value)
            ? ((float)value).ToString("R", CultureInfo.InvariantCulture)
            : value.ToString("R", CultureInfo.InvariantCulture);
    }
}
