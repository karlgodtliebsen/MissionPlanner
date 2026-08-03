namespace MissionPlanner.Core.ConfigTuning.Comparison;

/// <summary>Compares, stages and exports parameter sources.</summary>
public interface IParameterComparisonService
{
    /// <summary>Compares the union of names from two labelled sources.</summary>
    ParameterComparisonResult Compare(ParameterComparisonSource left, IReadOnlyDictionary<string, ParameterComparisonInput> leftValues, ParameterComparisonSource right,
        IReadOnlyDictionary<string, ParameterComparisonInput> rightValues, IReadOnlyDictionary<string, ParameterFieldMetadata> metadata);

    /// <summary>Stages selected safe right-side differences without writing them.</summary>
    IReadOnlyList<string> Stage(ParameterComparisonResult comparison, IParameterEditSession session, IReadOnlyCollection<string> selectedNames);

    /// <summary>Exports a comparison as JSON.</summary>
    string ExportJson(ParameterComparisonResult comparison);

    /// <summary>Exports a comparison as CSV.</summary>
    string ExportCsv(ParameterComparisonResult comparison);
}
