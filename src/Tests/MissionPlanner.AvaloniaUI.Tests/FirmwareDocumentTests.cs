using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using MissionPlanner.App.Presentation.Documents;

namespace MissionPlanner.AvaloniaUI.Tests;

public sealed class FirmwareDocumentTests
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<Application>()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions());

    [Fact]
    public void PreservesMultilineEvidenceAndEscapesUntrustedContent()
    {
        var document = Assert.IsType<UserDocument>(FirmwareDocumentConverter.SectionInstance.Convert(
            ["Controller [identity]", "USB device\nSecond device", "<script> [link](https://example.com)",
                AvaloniaProperty.UnsetValue, null], typeof(UserDocument), null, CultureInfo.InvariantCulture));
        Assert.Contains("## Controller \\[identity\\]", document.Markdown);
        Assert.Contains("USB device\n\nSecond device", document.Markdown);
        Assert.Contains(UserDocumentBuilder.Escape("<script> [link](https://example.com)"), document.Markdown);
        Assert.DoesNotContain("UnsetValue", document.Markdown);
    }

    [Fact]
    public async Task FormattedNumericAndLiveBindingsReachTheDocument()
    {
        var session = HeadlessUnitTestSession.StartNew(typeof(FirmwareDocumentTests));
        try
        {
            await session.Dispatch(() =>
            {
                var evidence = new Evidence();
                var control = new ContentControl { DataContext = evidence };
                var binding = new MultiBinding { Converter = FirmwareDocumentConverter.Instance };
                binding.Bindings.Add(new Binding(nameof(Evidence.Size)) { StringFormat = "Image bytes: {0}" });
                binding.Bindings.Add(new Binding(nameof(Evidence.Status)));
                using var subscription = control.Bind(ContentControl.ContentProperty, binding);
                var window = new Window { Content = control };
                window.Show();
                Dispatcher.UIThread.RunJobs();
                var initial = Assert.IsType<UserDocument>(control.Content);
                Assert.Contains("Image bytes: 42", initial.Markdown);
                Assert.Contains("Waiting", initial.Markdown);
                evidence.Status = "Verified";
                Dispatcher.UIThread.RunJobs();
                Assert.Contains("Verified", Assert.IsType<UserDocument>(control.Content).Markdown);
                window.Close();
            }, TestContext.Current.CancellationToken);
        }
        finally
        {
            await Task.Run(session.Dispose, CancellationToken.None);
        }
    }

    public sealed class Evidence : ObservableObject
    {
        private string status = "Waiting";
        public int Size => 42;
        public string Status { get => status; set => SetProperty(ref status, value); }
    }
}
