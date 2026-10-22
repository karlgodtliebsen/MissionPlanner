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

    /// <summary>Gets or sets the images of the topic.</summary>
    public ObservableRangeCollection<IntroductionImage> Images { get; set; } = [];

    /// <summary>Gets or sets the callouts that explain the images.</summary>
    public ObservableRangeCollection<IntroductionCallout> Callouts { get; set; } = [];

    /// <summary>Gets or sets the actions the topic offers.</summary>
    public ObservableRangeCollection<IntroductionAction> Actions { get; set; } = [];

    /// <summary>Gets or sets the Markdown text loaded from the topic file.</summary>
    [JsonIgnore] public string Markdown { get; set; } = string.Empty;

    /// <summary>Gets the title shown in the topic list: the short title when there is one, otherwise the title.</summary>
    [JsonIgnore] public string DisplayTitle => string.IsNullOrWhiteSpace(ShortTitle) ? Title : ShortTitle!;
}

