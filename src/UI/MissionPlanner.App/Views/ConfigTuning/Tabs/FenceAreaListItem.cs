using MissionPlanner.Core.ConfigTuning.Fences;

namespace MissionPlanner.App.Views.ConfigTuning.Tabs;

/// <summary>Projects one fence area into the geometry list.</summary>
/// <param name="Id">The area identifier.</param>
/// <param name="Kind">The area kind.</param>
/// <param name="Summary">A short geometry summary.</param>
/// <param name="IsClosed">Whether polygon editing is complete.</param>
public sealed record FenceAreaListItem(Guid Id, FenceAreaKind Kind, string Summary, bool IsClosed);
