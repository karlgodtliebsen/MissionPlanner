using MissionPlanner.AvaloniaUI.App.Views.Introduction.Models;

namespace MissionPlanner.AvaloniaUI.App.Views.Introduction.Services;

public interface IIntroductionContentLoader
{
    Task<IntroductionDocument> LoadAsync(CancellationToken cancellationToken = default);
}

