using System.Globalization;
using System.Text;
using System.Text.Json;

namespace MissionPlanner.Core.ConfigTuning.Comparison;

/// <summary>Default reusable parameter comparison engine.</summary>
public sealed class ParameterComparisonService(IParameterValueEquivalence equivalence) : IParameterComparisonService
{
    /// <inheritdoc />
    public ParameterComparisonResult Compare(
        ParameterComparisonSource left,
        IReadOnlyDictionary<string, ParameterComparisonInput> leftValues,
        ParameterComparisonSource right,
        IReadOnlyDictionary<string, ParameterComparisonInput> rightValues,
        IReadOnlyDictionary<string, ParameterFieldMetadata> metadata)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        var names = leftValues.Keys.Concat(rightValues.Keys).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        var rows = names.Select(name => CompareOne(
            name,
            left,
            leftValues.GetValueOrDefault(name),
            right,
            rightValues.GetValueOrDefault(name),
            metadata.GetValueOrDefault(name))).ToArray();
        var warning = left.Firmware is not null && right.Firmware is not null && left.Firmware != right.Firmware
            ? "The sources target different firmware identities. Review compatibility before staging."
            : null;
        return new ParameterComparisonResult(left, right, rows, warning);
    }

    /// <inheritdoc />
    public IReadOnlyList<string> Stage(
        ParameterComparisonResult comparison,
        IParameterEditSession session,
        IReadOnlyCollection<string> selectedNames)
    {
        var selected = selectedNames.ToHashSet(StringComparer.Ordinal);
        var staged = new List<string>();
        using var notifications = session.DeferChangeNotifications();
        foreach (var row in comparison.Rows.Where(row => row.CanStage && row.RightValue.HasValue && selected.Contains(row.Name)))
        {
            if (session.TrySetPending(row.Name, row.RightValue!.Value, out _))
            {
                staged.Add(row.Name);
            }
        }

        return staged;
    }

    /// <inheritdoc />
    public string ExportJson(ParameterComparisonResult comparison) =>
        JsonSerializer.Serialize(comparison, new JsonSerializerOptions { WriteIndented = true });

    /// <inheritdoc />
    public string ExportCsv(ParameterComparisonResult comparison)
    {
        var csv = new StringBuilder("Name,DisplayName,LeftSource,LeftValue,RightSource,RightValue,Difference,Status,Units,CanStage,Message\r\n");
        foreach (var row in comparison.Rows)
        {
            csv.AppendJoin(',', [
                Quote(row.Name), Quote(row.DisplayName), Quote(row.LeftSource), Number(row.LeftValue),
                Quote(row.RightSource), Number(row.RightValue), Number(row.Difference), Quote(row.Status.ToString()),
                Quote(row.Units), row.CanStage ? "true" : "false", Quote(row.Message)]);
            csv.Append("\r\n");
        }

        return csv.ToString();
    }

    private ParameterComparisonRow CompareOne(
        string name,
        ParameterComparisonSource left,
        ParameterComparisonInput? leftInput,
        ParameterComparisonSource right,
        ParameterComparisonInput? rightInput,
        ParameterFieldMetadata? metadata)
    {
        var leftValid = TryParse(leftInput, out var leftValue);
        var rightValid = TryParse(rightInput, out var rightValue);
        ParameterComparisonStatus status;
        string? message = null;

        if (leftInput is null)
        {
            status = rightValid ? ParameterComparisonStatus.OnlyOnRight : ParameterComparisonStatus.InvalidRightValue;
        }
        else if (rightInput is null)
        {
            status = ParameterComparisonStatus.OnlyOnLeft;
        }
        else if (!rightValid)
        {
            status = ParameterComparisonStatus.InvalidRightValue;
            message = $"'{rightInput.Value}' is not a finite parameter value.";
        }
        else if (!leftValid)
        {
            status = ParameterComparisonStatus.OnlyOnRight;
        }
        else if (metadata?.ReadOnly == true && !equivalence.AreEquivalent(leftValue, rightValue, metadata))
        {
            status = ParameterComparisonStatus.ReadOnly;
            message = "The target parameter is read-only.";
        }
        else if (metadata is null)
        {
            status = ParameterComparisonStatus.MetadataMissing;
            message = "Firmware metadata is not available.";
        }
        else
        {
            status = equivalence.AreEquivalent(leftValue, rightValue, metadata)
                ? ParameterComparisonStatus.Equal
                : ParameterComparisonStatus.Different;
        }

        var canStage = leftInput is not null && rightValid && metadata is { ReadOnly: false } &&
                       status is ParameterComparisonStatus.Different;
        return new ParameterComparisonRow(
            name,
            metadata?.DisplayName ?? name,
            left.Name,
            leftValid ? leftValue : null,
            right.Name,
            rightValid ? rightValue : null,
            leftValid && rightValid ? rightValue - leftValue : null,
            status,
            metadata?.Units,
            metadata,
            canStage,
            message);
    }

    private static bool TryParse(ParameterComparisonInput? input, out double value)
    {
        value = default;
        return input is not null &&
               double.TryParse(input.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
               double.IsFinite(value);
    }

    private static string Number(double? value) =>
        value?.ToString("R", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string Quote(string? value) => $"\"{(value ?? string.Empty).Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
