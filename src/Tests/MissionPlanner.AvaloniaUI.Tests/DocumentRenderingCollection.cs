namespace MissionPlanner.AvaloniaUI.Tests;

/// <summary>Serializes real application rendering because Avalonia's platform registry is process-global.</summary>
[CollectionDefinition("Document rendering", DisableParallelization = true)]
public sealed class DocumentRenderingCollection
{
}
