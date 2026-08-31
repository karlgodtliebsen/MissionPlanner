using System.Diagnostics;
using Avalonia;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace MissionPlanner.AvaloniaUI.App;

public static class ApplicationRunner
{
    public static void SetAppDomainExceptionHandling(string title)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, ex) => CurrentDomainUnhandledException(title, ex);
    }

    public static void CurrentDomainUnhandledException(string title, UnhandledExceptionEventArgs e)
    {
        // ReSharper disable once LocalizableElement
        Console.WriteLine($"{title} Unhandled Exception");
        Console.WriteLine(e.ExceptionObject);
        Debug.Print($"{title} Unhandled Exception");
        Debug.Print(e.ExceptionObject.ToString());
        Log.Logger.Fatal(e.ExceptionObject as Exception, "{title} Unhandled Exception", title);
        Log.CloseAndFlush();
    }
    public static async Task RunAllAsync(IHost host, AppBuilder app, string[] args, string title, CancellationToken cancellationToken)
    {
        try
        {
            await Task.WhenAll(
                RunHostAsync(host, title, cancellationToken),
                RunAsync(app, args, title, cancellationToken)
            );
        }
        catch (Exception ex)
        {
            if (ex.Message == "A task was canceled.")
            {
                Log.Logger.Information("Task was cancelled for {title}", title);
                return;
            }

            Log.Logger.Fatal(ex, "Error starting Desktop Application And Host for {title}", title);
        }
    }

    public static Task RunAsync(AppBuilder app, string[] args, string title, CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            try
            {
                app.StartWithClassicDesktopLifetime(args);
            }
            catch (Exception ex)
            {
                Log.Logger.Fatal(ex, "Error starting Desktop Application for {title}", title);
                throw;
            }
        }, cancellationToken);
    }

    public static Task RunHostAsync(IHost host, string title, CancellationToken cancellationToken)
    {
        try
        {
            return host.RunAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Log.Logger.Fatal(ex, "Error starting Host for {title}", title);
            throw;
        }
    }

    public static Task RunHostsAsync(IHost[] hosts, string title, CancellationToken cancellationToken)
    {
        try
        {
            IList<Task> tasks = new List<Task>(hosts.Length);
            foreach (var host in hosts)
            {
                tasks.Add(host.RunAsync(cancellationToken));
            }

            return Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            Log.Logger.Fatal(ex, "Error starting Host for {title}", title);
            throw;
        }
    }
}
