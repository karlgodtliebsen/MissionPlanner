using Android.Runtime;
using Avalonia;
using Avalonia.Android;

namespace MissionPlanner
{
    [Application]
    public class Application : AvaloniaAndroidApplication<MissionPlanner.App>
    {
        protected Application(nint javaReference, JniHandleOwnership transfer) : base(javaReference, transfer)
        {
        }

        protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        {
            return base.CustomizeAppBuilder(builder)
            .WithInterFont();
        }
    }
}
