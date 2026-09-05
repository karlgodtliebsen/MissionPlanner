using Avalonia;
using Avalonia.Browser;
using MissionPlanner.App;

internal sealed partial class Program
{

    public static void Main(string[] args)
    {
        MissionPlannerProgram.Main(args);
        /*sc.AddWindowsOnlyServices()*/

        var app = MissionPlannerProgram.BuildAvaloniaApp((sc) => { });

        app
            .With(new Win32PlatformOptions())
            .StartBrowserAppAsync("out");
    }

}
