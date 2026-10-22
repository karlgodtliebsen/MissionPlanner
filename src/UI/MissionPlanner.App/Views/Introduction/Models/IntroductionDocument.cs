namespace MissionPlanner.App.Views.Introduction.Models;

/// <summary>The Introduction content: a title and its topics.</summary>
public sealed class IntroductionDocument
{
    /// <summary>Gets or sets the version of the document schema.</summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Gets or sets the document title.</summary>
    public string Title { get; set; } = "Introduction";

    /// <summary>Gets or sets the document subtitle.</summary>
    public string Subtitle { get; set; } = "Quick guide to MissionPlanner NextGeneration";

    /// <summary>Gets or sets the topics of the document.</summary>
    public List<IntroductionTopic> Topics { get; set; } = [];
}

