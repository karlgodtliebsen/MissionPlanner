namespace MissionPlanner.App.Presentation.Documents;

/// <summary>An immutable application explanation, independent of the rendering library.</summary>
public sealed record UserDocument(string Markdown, string? Title = null);
