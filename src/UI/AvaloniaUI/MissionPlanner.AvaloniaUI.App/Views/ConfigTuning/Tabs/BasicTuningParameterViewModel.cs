using MissionPlanner.AvaloniaUI.App.Views.Common;
using MissionPlanner.Core.ConfigTuning;
using MissionPlanner.Core.ConfigTuning.Tuning;

namespace MissionPlanner.AvaloniaUI.App.Views.ConfigTuning.Tabs;

/// <summary>Projects one curated tuning field and its shared-session editor.</summary>
public sealed class BasicTuningParameterViewModel
{
    /// <summary>Initializes a curated tuning field projection.</summary>
    /// <param name="resolved">The resolved field definition.</param>
    /// <param name="session">The shared parameter session.</param>
    public BasicTuningParameterViewModel(ResolvedBasicTuningField resolved, IParameterEditSession session)
    {
        Definition = resolved.Definition;
        ParameterName = resolved.ParameterName;
        Editor = new ParameterItemViewModel(session, session.GetField(ParameterName)!);
    }

    /// <summary>Gets the curated field definition.</summary>
    public BasicTuningFieldDefinition Definition
    {
        get;
    }

    /// <summary>Gets the resolved vehicle parameter name.</summary>
    public string ParameterName
    {
        get;
    }

    /// <summary>Gets the shared-session parameter editor.</summary>
    public ParameterItemViewModel Editor
    {
        get;
    }

    /// <summary>Gets the plain-language title.</summary>
    public string Title => Definition.Title;

    /// <summary>Gets the plain-language explanation.</summary>
    public string Description => Definition.Description;

    /// <summary>Gets metadata units with a curated fallback.</summary>
    public string Units => string.IsNullOrWhiteSpace(Editor.Units) ? Definition.FallbackUnits : Editor.Units;

    /// <summary>Gets the optional field-level stability warning.</summary>
    public string? Warning => Definition.Warning;

    /// <summary>Gets whether a field-level warning is present.</summary>
    public bool HasWarning => !string.IsNullOrWhiteSpace(Warning);

    /// <summary>Gets whether an authoritative recommendation can be shown.</summary>
    public bool HasRecommendation => Definition.HasAuthoritativeRecommendation;

    /// <summary>Gets the sourced recommendation text.</summary>
    public string RecommendationText => HasRecommendation
        ? $"Recommended: {Definition.RecommendedValue} {Units} ({Definition.RecommendationSource})"
        : string.Empty;

    /// <summary>Refreshes the editor projection from the shared session.</summary>
    /// <param name="session">The shared parameter session.</param>
    public void Refresh(IParameterEditSession session)
    {
        if (session.GetField(ParameterName) is { } field)
        {
            Editor.SetField(field);
        }
    }
}

