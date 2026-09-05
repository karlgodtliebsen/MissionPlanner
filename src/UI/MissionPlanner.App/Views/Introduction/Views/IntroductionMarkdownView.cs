using System.Text;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace MissionPlanner.App.Views.Introduction.Views;

/// <summary>Renders the bounded Markdown subset used by bundled Introduction content.</summary>
public sealed partial class IntroductionMarkdownView : UserControl
{
    /// <summary>Defines the Markdown content property.</summary>
    public static readonly StyledProperty<string?> MarkdownProperty =
        AvaloniaProperty.Register<IntroductionMarkdownView, string?>(nameof(Markdown));

    private readonly StackPanel layout = new() { Spacing = 10 };

    /// <summary>Initializes the Markdown renderer.</summary>
    public IntroductionMarkdownView()
    {
        Content = layout;
        this.GetObservable(MarkdownProperty).Subscribe(_ => Render());
    }

    /// <summary>Gets or sets the Markdown content.</summary>
    public string? Markdown
    {
        get => GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    private void Render()
    {
        layout.Children.Clear();
        if (string.IsNullOrWhiteSpace(Markdown)) return;
        var paragraph = new StringBuilder();
        var code = new StringBuilder();
        var inCode = false;
        foreach (var rawLine in Markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            var line = rawLine.TrimEnd();
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                FlushParagraph(paragraph);
                if (inCode) { AddCode(code.ToString().TrimEnd()); code.Clear(); }
                inCode = !inCode;
                continue;
            }
            if (inCode) { code.AppendLine(rawLine); continue; }
            if (string.IsNullOrWhiteSpace(line)) { FlushParagraph(paragraph); continue; }
            if (line.StartsWith("# ")) { FlushParagraph(paragraph); AddText(line[2..], 28, FontWeight.Bold); continue; }
            if (line.StartsWith("## ")) { FlushParagraph(paragraph); AddText(line[3..], 22, FontWeight.Bold); continue; }
            if (line.StartsWith("### ")) { FlushParagraph(paragraph); AddText(line[4..], 18, FontWeight.Bold); continue; }
            if (line.StartsWith("> ")) { FlushParagraph(paragraph); AddQuote(line[2..]); continue; }
            if (line.StartsWith("- ") || line.StartsWith("* ")) { FlushParagraph(paragraph); AddText("•  " + line[2..], 15); continue; }
            var numbered = NumberedListRegex().Match(line);
            if (numbered.Success) { FlushParagraph(paragraph); AddText(numbered.Groups[1].Value + ".  " + numbered.Groups[2].Value, 15); continue; }
            if (line is "---" or "***") { FlushParagraph(paragraph); layout.Children.Add(new Separator()); continue; }
            if (paragraph.Length > 0) paragraph.Append(' ');
            paragraph.Append(line.Trim());
        }
        FlushParagraph(paragraph);
        if (inCode && code.Length > 0) AddCode(code.ToString().TrimEnd());
    }

    private void FlushParagraph(StringBuilder paragraph)
    {
        if (paragraph.Length == 0) return;
        AddText(RemoveInlineMarkers(paragraph.ToString()), 15);
        paragraph.Clear();
    }

    private void AddText(string text, double size, FontWeight? weight = null) => layout.Children.Add(new TextBlock
    {
        Text = RemoveInlineMarkers(text), FontSize = size, FontWeight = weight ?? FontWeight.Normal, TextWrapping = TextWrapping.Wrap
    });

    private void AddQuote(string text) => layout.Children.Add(new Border
    {
        Padding = new Thickness(12, 9), CornerRadius = new CornerRadius(6),
        Background = new SolidColorBrush(Color.Parse("#202C3137")),
        Child = new TextBlock { Text = RemoveInlineMarkers(text), FontStyle = FontStyle.Italic, TextWrapping = TextWrapping.Wrap }
    });

    private void AddCode(string text) => layout.Children.Add(new Border
    {
        Padding = new Thickness(10), CornerRadius = new CornerRadius(6), BorderThickness = new Thickness(1),
        BorderBrush = Brushes.Gray,
        Child = new TextBlock { Text = text, FontFamily = FontFamily.Parse("Consolas"), FontSize = 13, TextWrapping = TextWrapping.Wrap }
    });

    // TODO: Restore mixed bold/italic/inline-code spans once the Avalonia renderer has a tested inline parser.
    private static string RemoveInlineMarkers(string text) => text.Replace("**", string.Empty).Replace("`", string.Empty).Replace("*", string.Empty);

    [GeneratedRegex(@"^(\d+)\.\s+(.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex NumberedListRegex();
}
