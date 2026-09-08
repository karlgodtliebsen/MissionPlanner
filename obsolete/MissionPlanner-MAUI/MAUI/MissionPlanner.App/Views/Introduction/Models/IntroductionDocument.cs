namespace MissionPlanner.App.Views.Introduction.Models;

public sealed class IntroductionDocument
{
    public int SchemaVersion { get; set; } = 1;

    public string Title { get; set; } = "Introduction";

    public string Subtitle { get; set; } = "Quick guide to MissionPlanner NextGeneration";

    public List<IntroductionTopic> Topics { get; set; } = [];
}
