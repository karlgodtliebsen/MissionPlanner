using MissionPlanner.App.Views.Introduction.Models;

namespace MissionPlanner.App.Views.Introduction.Services;

public interface IIntroductionContentLoader
{
    Task<IntroductionDocument> LoadAsync(CancellationToken cancellationToken = default);
}
