using System.Text.Json.Serialization;

namespace MissionPlanner.App.Views.Introduction.Models;

public sealed class IntroductionTopic
{
    public string Id { get; set; } = string.Empty;

    public int Order { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? ShortTitle { get; set; }

    public string MarkdownFile { get; set; } = string.Empty;

    public List<IntroductionImage> Images { get; set; } = [];

    public List<IntroductionCallout> Callouts { get; set; } = [];

    public List<IntroductionAction> Actions { get; set; } = [];

    [JsonIgnore]
    public string Markdown { get; set; } = string.Empty;

    [JsonIgnore]
    public string DisplayTitle => string.IsNullOrWhiteSpace(ShortTitle) ? Title : ShortTitle!;
}
