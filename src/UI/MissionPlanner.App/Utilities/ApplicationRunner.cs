using System.Diagnostics;
using Avalonia;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace MissionPlanner.App.Utilities;

/// <summary>
/// Provides methods to run the application and handle exceptions in the AppDomain.
/// </summary>
public static class ApplicationRunner
{
    /// <summary>
    /// Sets up handling for unhandled exceptions in the current AppDomain.
    /// </summary>
    /// <param name="title">The title of the application.</param>
    public static void SetAppDomainExceptionHandling(string title)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, ex) => CurrentDomainUnhandledException(title, ex);
    }

    /// <summary>
    /// Handles unhandled exceptions in the current AppDomain.
    /// </summary>
    /// <param name="title">The title of the application.</param>
    /// <param name="e">The unhandled exception event arguments.</param>
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
    /// <summary>
    /// Runs the host and the application concurrently.
    /// </summary>
    /// <param name="host">The host to run.</param>
    /// <param name="app">The Avalonia application builder.</param>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="title">The title of the application.</param>
    /// <param name="cancellationToken"></param>
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

    /// <summary>
    /// Runs the Avalonia application.
    /// </summary>
    /// <param name="app">The Avalonia application builder.</param>
    /// <param name="args">The command-line arguments.</param>
    /// <param name="title">The title of the application.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns></returns>
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

    /// <summary>
    /// Runs the host.
    /// </summary>
    /// <param name="host">The host to run.</param>
    /// <param name="title">The title of the application.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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

    /// <summary>
    /// Runs multiple hosts concurrently.
    /// </summary>
    /// <param name="hosts">The hosts to run.</param>
    /// <param name="title">The title of the application.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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
