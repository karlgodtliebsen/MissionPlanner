using System.Text;
using System.Text.RegularExpressions;

namespace MissionPlanner.App.Views.Introduction.Views;

/// <summary>
/// Small native-MAUI Markdown renderer intended for the Introduction content.
/// It deliberately supports only the subset used by the bundled help files:
/// headings, paragraphs, bullet/numbered lists, block quotes, fenced code,
/// bold, italic, and inline code.
/// </summary>
public sealed partial class IntroductionMarkdownView : ContentView
{
    /// <summary>
    /// The Markdown content to be rendered.
    /// </summary>
    public static readonly BindableProperty MarkdownProperty = BindableProperty.Create(
        nameof(Markdown),
        typeof(string),
        typeof(IntroductionMarkdownView),
        string.Empty,
        propertyChanged: static (bindable, _, _) =>
            ((IntroductionMarkdownView)bindable).Render());

    private readonly VerticalStackLayout layout = new() { Spacing = 10 };

    /// <summary>
    /// Initializes a new instance of the <see cref="IntroductionMarkdownView"/> class. 
    /// </summary>
    public IntroductionMarkdownView()
    {
        Content = layout;
    }

    /// <summary>
    /// Sets or gets the Markdown content to be rendered.
    /// </summary>
    public string Markdown
    {
        get => (string)GetValue(MarkdownProperty);
        set => SetValue(MarkdownProperty, value);
    }

    private void Render()
    {
        layout.Children.Clear();

        if (string.IsNullOrWhiteSpace(Markdown))
        {
            return;
        }

        var lines = Markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var paragraph = new StringBuilder();
        var code = new StringBuilder();
        var inCode = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();

            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                FlushParagraph(paragraph);

                if (inCode)
                {
                    AddCodeBlock(code.ToString().TrimEnd());
                    code.Clear();
                    inCode = false;
                }
                else
                {
                    inCode = true;
                }

                continue;
            }

            if (inCode)
            {
                code.AppendLine(rawLine);
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                FlushParagraph(paragraph);
                continue;
            }

            if (IsHeading(line))
            {
                FlushParagraph(paragraph);
                AddHeading(line);
                continue;
            }

            if (line.StartsWith("> ", StringComparison.Ordinal))
            {
                FlushParagraph(paragraph);
                AddQuote(line[2..]);
                continue;
            }

            if (line.StartsWith("- ", StringComparison.Ordinal) ||
                line.StartsWith("* ", StringComparison.Ordinal))
            {
                FlushParagraph(paragraph);
                AddListItem("•", line[2..]);
                continue;
            }

            var numbered = NumberedListRegex().Match(line);
            if (numbered.Success)
            {
                FlushParagraph(paragraph);
                AddListItem(numbered.Groups[1].Value + ".", numbered.Groups[2].Value);
                continue;
            }

            if (line is "---" or "***")
            {
                FlushParagraph(paragraph);
                layout.Children.Add(new BoxView { HeightRequest = 1, Opacity = 0.25, Margin = new Thickness(0, 6) });
                continue;
            }

            if (paragraph.Length > 0)
            {
                paragraph.Append(' ');
            }

            paragraph.Append(line.Trim());
        }

        FlushParagraph(paragraph);

        if (inCode && code.Length > 0)
        {
            AddCodeBlock(code.ToString().TrimEnd());
        }
    }

    private static bool IsHeading(string line)
    {
        var level = 0;
        while (level < line.Length && level < 3 && line[level] == '#')
        {
            level++;
        }

        return level > 0 && level < line.Length && line[level] == ' ';
    }

    private void AddHeading(string line)
    {
        var level = 0;
        while (level < line.Length && level < 3 && line[level] == '#')
        {
            level++;
        }

        var text = line[(level + 1)..].Trim();
        layout.Children.Add(CreateFormattedLabel(
            text,
            level switch
            {
                1 => 28,
                2 => 22,
                var _ => 18
            },
            FontAttributes.Bold,
            new Thickness(0, level == 1 ? 8 : 5, 0, 2)));
    }

    private void FlushParagraph(StringBuilder paragraph)
    {
        if (paragraph.Length == 0)
        {
            return;
        }

        layout.Children.Add(CreateFormattedLabel(paragraph.ToString(), 15, FontAttributes.None));
        paragraph.Clear();
    }

    private void AddListItem(string marker, string text)
    {
        var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 8, Margin = new Thickness(8, 0, 0, 0) };

        grid.Add(new Label { Text = marker, FontSize = 15, VerticalTextAlignment = TextAlignment.Start }, 0, 0);

        grid.Add(CreateFormattedLabel(text, 15, FontAttributes.None), 1, 0);
        layout.Children.Add(grid);
    }

    private void AddQuote(string text)
    {
        var border = new Border
        {
            StrokeThickness = 0,
            Padding = new Thickness(12, 9),
            BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark
                ? Color.FromArgb("#2C3137")
                : Color.FromArgb("#F1F4F7"),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
            Content = CreateFormattedLabel(text, 14, FontAttributes.Italic)
        };

        layout.Children.Add(border);
    }

    private void AddCodeBlock(string text)
    {
        var label = new Label { Text = text, FontFamily = "monospace", FontSize = 13, LineBreakMode = LineBreakMode.WordWrap };

        var border = new Border
        {
            Stroke = new SolidColorBrush(Color.FromArgb("#808080")),
            StrokeThickness = 1,
            Padding = 10,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 },
            Content = label
        };

        layout.Children.Add(border);
    }

    private static Label CreateFormattedLabel(string text, double fontSize, FontAttributes attributes, Thickness? margin = null)
    {
        var label = new Label
        {
            FontSize = fontSize,
            FontAttributes = attributes,
            LineBreakMode = LineBreakMode.WordWrap,
            Margin = margin ?? Thickness.Zero,
            FormattedText = ParseInline(text, attributes)
        };
        return label;
    }

    private static FormattedString ParseInline(string text, FontAttributes baseAttributes)
    {
        var result = new FormattedString();
        var index = 0;

        while (index < text.Length)
        {
            if (text.AsSpan(index).StartsWith("**", StringComparison.Ordinal))
            {
                var end = text.IndexOf("**", index + 2, StringComparison.Ordinal);
                if (end >= 0)
                {
                    result.Spans.Add(new Span { Text = text[(index + 2)..end], FontAttributes = baseAttributes | FontAttributes.Bold });
                    index = end + 2;
                    continue;
                }
            }

            if (text[index] == '`')
            {
                var end = text.IndexOf('`', index + 1);
                if (end >= 0)
                {
                    result.Spans.Add(new Span { Text = text[(index + 1)..end], FontFamily = "monospace", FontAttributes = baseAttributes });
                    index = end + 1;
                    continue;
                }
            }

            if (text[index] == '*')
            {
                var end = text.IndexOf('*', index + 1);
                if (end >= 0)
                {
                    result.Spans.Add(new Span { Text = text[(index + 1)..end], FontAttributes = baseAttributes | FontAttributes.Italic });
                    index = end + 1;
                    continue;
                }
            }

            var next = FindNextToken(text, index + 1);
            result.Spans.Add(new Span { Text = text[index..next], FontAttributes = baseAttributes });
            index = next;
        }

        return result;
    }

    private static int FindNextToken(string text, int start)
    {
        for (var i = start; i < text.Length; i++)
        {
            if (text[i] is '*' or '`')
            {
                return i;
            }
        }

        return text.Length;
    }

    [GeneratedRegex(@"^(\d+)\.\s+(.+)$", RegexOptions.CultureInvariant)]
    private static partial Regex NumberedListRegex();
}
