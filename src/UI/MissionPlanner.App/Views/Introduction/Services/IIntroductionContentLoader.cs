using MissionPlanner.App.Views.Introduction.Models;

namespace MissionPlanner.App.Views.Introduction.Services;

/// <summary>Loads the Introduction content.</summary>
public interface IIntroductionContentLoader
{
    /// <summary>Loads the Introduction document and the Markdown of each topic.</summary>
    Task<IntroductionDocument> LoadAsync(CancellationToken cancellationToken = default);
}

