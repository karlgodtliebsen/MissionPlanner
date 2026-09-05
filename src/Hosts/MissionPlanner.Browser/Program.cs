using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;
using MissionPlanner;

internal sealed partial class Program
{
    //private static Task Main(string[] args) => BuildAvaloniaApp()
    //        .WithInterFont()
    //        .StartBrowserAppAsync("out");


    private static Task private void Main(string[] args)
    {
        MissionPlanner.AvaloniaUI.App.Program.Main(args);

        return Task.CompletedTask;
    }



    //public static AppBuilder BuildAvaloniaApp()
    //    => AppBuilder.Configure<App>();
}
