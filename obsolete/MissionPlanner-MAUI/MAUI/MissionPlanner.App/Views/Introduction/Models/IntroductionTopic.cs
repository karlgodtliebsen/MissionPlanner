using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using Mapsui.Utilities;

namespace MissionPlanner.App.Views.Introduction.Models;

/// <summary>
/// Represents a topic in the Introduction document, including its metadata, associated images, callouts, actions, and the loaded Markdown content.
/// </summary>
public sealed partial class IntroductionTopic : ObservableObject
{
    [ObservableProperty] public partial string Id { get; set; } = string.Empty;

    [ObservableProperty] public partial int Order { get; set; }

    [ObservableProperty] public partial string Title { get; set; } = string.Empty;

    [ObservableProperty] public partial string? ShortTitle { get; set; }

    [ObservableProperty] public partial string MarkdownFile { get; set; } = string.Empty;

    public ObservableRangeCollection<IntroductionImage> Images { get; set; } = [];

    public ObservableRangeCollection<IntroductionCallout> Callouts { get; set; } = [];

    public ObservableRangeCollection<IntroductionAction> Actions { get; set; } = [];

    [JsonIgnore] public string Markdown { get; set; } = string.Empty;

    [JsonIgnore] public string DisplayTitle => string.IsNullOrWhiteSpace(ShortTitle) ? Title : ShortTitle!;
}
