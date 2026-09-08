using Foundation;

namespace MissionPlanner.Mac
{
    /// <summary>
    /// Provides the public API for AppDelegate.
    /// </summary>
    [Register(nameof(AppDelegate))]
    public class AppDelegate : MauiUIApplicationDelegate
    {
        /// <summary>
        /// Provides the public API for CreateMauiApp.
        /// </summary>
        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
    }
}
