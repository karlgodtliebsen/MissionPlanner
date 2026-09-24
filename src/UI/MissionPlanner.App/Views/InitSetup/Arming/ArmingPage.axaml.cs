using Avalonia.Controls;
using LiveMarkdown.Avalonia;

namespace MissionPlanner.App.Views.InitSetup.Arming;

public partial class ArmingPage : NavigationPage
{
    public ArmingPage()
    {
        InitializeComponent();

        var markdownBuilder = new ObservableStringBuilder();
        MarkdownRenderer.MarkdownBuilder = markdownBuilder;

        // Append each chunk received from the streaming source.
        markdownBuilder.Append("# Hello, Markdown!");
        markdownBuilder.Append("\n\nThis is a **live** Markdown viewer for Avalonia applications.");

        // Clearing or replacing text also triggers a render update.
        //        markdownBuilder.Clear();
    }
}
